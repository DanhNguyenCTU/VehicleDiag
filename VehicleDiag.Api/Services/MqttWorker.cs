using Microsoft.EntityFrameworkCore;
using MQTTnet;
using MQTTnet.Client;
using System.Text;
using System.Text.Json;
using VehicleDiag.Api.Data;
using VehicleDiag.Api.Models;
using VehicleDiag.Api.Constants;

namespace VehicleDiag.Api.Services
{
    public class MqttWorker : BackgroundService
    {
        private readonly IServiceScopeFactory _scopeFactory;
        private IMqttClient? _client;

        public MqttWorker(IServiceScopeFactory scopeFactory)
        {
            _scopeFactory = scopeFactory;
        }

        protected override async Task ExecuteAsync(CancellationToken stoppingToken)
        {
            var factory = new MqttFactory();
            _client = factory.CreateMqttClient();

            _client.ApplicationMessageReceivedAsync += HandleMessage;

            var options = new MqttClientOptionsBuilder()
                .WithTcpServer("dc3f111e040f4e8d9b48df7616b1b80e.s1.eu.hivemq.cloud", 8883)
                .WithCredentials("ds_backend", "DSbackend32265")
                .WithTlsOptions(o =>
                {
                    o.UseTls();
                })
                .Build();

            await _client.ConnectAsync(options, stoppingToken);

            Console.WriteLine("MQTT connected");

            await _client.SubscribeAsync("vehicle/+/telemetry");
            await _client.SubscribeAsync("vehicle/+/status");
            await _client.SubscribeAsync("vehicle/+/session/+/dtc");
            await _client.SubscribeAsync("vehicle/+/session/+/info");
            await _client.SubscribeAsync("vehicle/+/session/+/freeze");
            await _client.SubscribeAsync("vehicle/+/session/+/done");

            Console.WriteLine("Subscribed MQTT topics");

            await Task.Delay(Timeout.Infinite, stoppingToken);
        }

        private async Task HandleMessage(MqttApplicationMessageReceivedEventArgs e)
        {
            try
            {
                if (e.ApplicationMessage.PayloadSegment.Count == 0)
                    return;

                var topic = e.ApplicationMessage.Topic;
                var payload = Encoding.UTF8.GetString(e.ApplicationMessage.PayloadSegment);

                Console.WriteLine($"MQTT {topic} -> {payload}");

                var parts = topic.Split('/');

                if (parts.Length < 3)
                    return;

                var deviceId = parts[1];

                using var scope = _scopeFactory.CreateScope();
                var db = scope.ServiceProvider.GetRequiredService<AppDbContext>();

                if (topic.Contains("/telemetry"))
                    await HandleTelemetry(deviceId, payload, db);

                else if (topic.EndsWith("/status"))
                    await HandleHeartbeat(deviceId, db);

                else if (topic.EndsWith("/dtc"))
                    await HandleSessionDtc(topic, payload, db);

                else if (topic.EndsWith("/info"))
                    await HandleSessionInfo(topic, payload, db);

                else if (topic.EndsWith("/freeze"))
                    await HandleSessionFreezeFrame(topic, payload, db);

                else if (topic.EndsWith("/done"))
                    await HandleSessionComplete(topic, db);
            }
            catch (Exception ex)
            {
                Console.WriteLine("MQTT ERROR:");
                Console.WriteLine(ex.ToString());
            }
        }

        // =========================================================
        // AUTO MODE
        // =========================================================

        private async Task HandleTelemetry(string deviceId, string payload, AppDbContext db)
        {
            Console.WriteLine($"[Telemetry] Device={deviceId}");

            var data = JsonSerializer.Deserialize<TelemetryPayload>(
                payload,
                new JsonSerializerOptions
                {
                    PropertyNameCaseInsensitive = true
                });

            if (data == null)
            {
                Console.WriteLine("[Telemetry] JSON parse failed");
                return;
            }

            Console.WriteLine($"[Telemetry] Lat={data.Lat} Lng={data.Lng}");

            var device = await db.Devices
                .FirstOrDefaultAsync(x => x.DeviceId == deviceId && x.IsActive);

            if (device == null)
            {
                Console.WriteLine($"[Telemetry] Device {deviceId} not found");
                return;
            }

            var vehicle = await db.Vehicles
                .FirstOrDefaultAsync(v => v.DeviceId == deviceId);

            if (vehicle == null)
            {
                Console.WriteLine($"[Telemetry] Vehicle mapping missing");
                return;
            }

            var now = DateTime.UtcNow;

            // =========================
            // SAVE GPS TELEMETRY
            // =========================

            db.Telemetry.Add(new Telemetry
            {
                DeviceId = deviceId,
                Lat = data.Lat,
                Lng = data.Lng,
                CreatedAt = now
            });

            device.LastSeenAt = now;

            // =========================
            // DTC SNAPSHOT
            // =========================

            var incomingDtcs = data.Dtcs ?? new List<DtcPayload>();

            Console.WriteLine($"[Telemetry] Snapshot DTC count={incomingDtcs.Count}");

            foreach (var d in incomingDtcs)
                Console.WriteLine($"   -> {d.DtcCode} status={d.StatusByte}");

            // =========================
            // OVERWRITE CURRENT
            // =========================

            var oldCurrent = await db.EcuDtcCurrent
                .Where(x => x.VehicleId == vehicle.VehicleId)
                .ToListAsync();

            if (oldCurrent.Count > 0)
            {
                Console.WriteLine($"[Telemetry] Clearing {oldCurrent.Count} old current DTC");
                db.EcuDtcCurrent.RemoveRange(oldCurrent);
            }

            foreach (var d in incomingDtcs)
            {
                db.EcuDtcCurrent.Add(new EcuDtcCurrent
                {
                    VehicleId = vehicle.VehicleId,
                    DtcCode = d.DtcCode,
                    StatusByte = (byte)d.StatusByte,
                    LastSeenAt = now
                });
            }

            // =========================
            // APPEND HISTORY SNAPSHOT
            // =========================

            foreach (var d in incomingDtcs)
            {
                db.EcuDtcHistory.Add(new EcuDtcHistory
                {
                    VehicleId = vehicle.VehicleId,
                    DtcCode = d.DtcCode,
                    StatusByte = (byte)d.StatusByte,
                    FirstSeenAt = now,
                    LastSeenAt = now
                });
            }

            Console.WriteLine("[Telemetry] Saving DB changes...");

            await db.SaveChangesAsync();

            Console.WriteLine("[Telemetry] DB updated");
        }
        private async Task HandleSessionFreezeFrame(string topic, string payload, AppDbContext db)
        {
            var parts = topic.Split('/');

            if (parts.Length < 5)
                return;

            if (!int.TryParse(parts[3], out int sessionId))
                return;

            Console.WriteLine($"[Session] FREEZE FRAME session={sessionId}");

            var data = JsonSerializer.Deserialize<FreezeFramePayload>(
                payload,
                new JsonSerializerOptions
                {
                    PropertyNameCaseInsensitive = true
                });

            if (data == null)
                return;

            db.FreezeFrames.Add(new FreezeFrame
            {
                SessionId = sessionId,
                Dtc = data.Dtc,
                Rpm = data.Rpm,
                Speed = data.Speed,
                Coolant = data.Coolant
            });

            await db.SaveChangesAsync();
        }
        private async Task HandleHeartbeat(string deviceId, AppDbContext db)
        {
            Console.WriteLine($"[Heartbeat] Device={deviceId}");

            var device = await db.Devices
                .FirstOrDefaultAsync(x => x.DeviceId == deviceId && x.IsActive);

            if (device == null)
            {
                Console.WriteLine($"[Heartbeat] Device {deviceId} not found");
                return;
            }

            device.LastSeenAt = DateTime.UtcNow;

            await db.SaveChangesAsync();
        }
        // =========================================================
        // SESSION RESULT
        // =========================================================

        private async Task HandleSessionDtc(string topic, string payload, AppDbContext db)
        {
            var parts = topic.Split('/');

            if (parts.Length < 5)
                return;

            if (!int.TryParse(parts[3], out int sessionId))
                return;

            Console.WriteLine($"[Session] DTC result session={sessionId}");

            var data = JsonSerializer.Deserialize<SessionResultPayload>(
                payload,
                new JsonSerializerOptions
                {
                    PropertyNameCaseInsensitive = true
                });

            if (data?.Dtcs == null || data.Dtcs.Count == 0)
            {
                Console.WriteLine("[Session] No DTC returned");
                return;
            }

            foreach (var d in data.Dtcs)
            {
                Console.WriteLine($"   -> {d.DtcCode}");

                db.EcuDtcResults.Add(new EcuDtcResult
                {
                    SessionId = sessionId,
                    DtcCode = d.DtcCode,
                    StatusByte = (byte)d.StatusByte,
                    Protocol = data.Protocol ?? "OBDII"
                });
            }

            await db.SaveChangesAsync();
        }

        // =========================================================
        // SESSION INFO
        // =========================================================

        private async Task HandleSessionInfo(string topic, string payload, AppDbContext db)
        {
            var parts = topic.Split('/');

            if (parts.Length < 5)
                return;

            if (!int.TryParse(parts[3], out int sessionId))
                return;

            Console.WriteLine($"[Session] INFO session={sessionId}");

            var data = JsonSerializer.Deserialize<SessionInfoPayload>(
                payload,
                new JsonSerializerOptions
                {
                    PropertyNameCaseInsensitive = true
                });

            if (data == null)
            {
                Console.WriteLine("[Session] INFO payload parse failed");
                return;
            }

            var session = await db.EcuReadSessions
                .FirstOrDefaultAsync(x => x.SessionId == sessionId);

            if (session == null)
            {
                Console.WriteLine($"[Session] session {sessionId} not found");
                return;
            }

            void Add(string key, string? val)
            {
                if (string.IsNullOrWhiteSpace(val))
                    return;

                Console.WriteLine($"   -> {key} = {val}");

                db.EcuInfoResults.Add(new EcuInfoResult
                {
                    SessionId = sessionId,
                    InfoKey = key,
                    InfoLabel = key,
                    InfoValue = val
                });
            }

            Add("VIN", data.Vin);
            Add("CALID", data.CalId);
            Add("CVN", data.Cvn);
            Add("HW", data.Hardware);

            await db.SaveChangesAsync();

            Console.WriteLine("[Session] INFO stored");
        }

        // =========================================================
        // SESSION COMPLETE
        // =========================================================

        private async Task HandleSessionComplete(string topic, AppDbContext db)
        {
            var parts = topic.Split('/');

            if (parts.Length < 5)
                return;

            if (!int.TryParse(parts[3], out int sessionId))
                return;

            Console.WriteLine($"[Session] COMPLETE session={sessionId}");

            var session = await db.EcuReadSessions
                .FirstOrDefaultAsync(x => x.SessionId == sessionId);

            if (session == null)
            {
                Console.WriteLine($"[Session] session {sessionId} not found");
                return;
            }

            session.Status = SessionStatus.Completed;
            session.CompletedAt = DateTime.UtcNow;

            await db.SaveChangesAsync();

            Console.WriteLine("[Session] status updated → COMPLETED");
        }

        // =========================================================
        // PAYLOAD MODELS
        // =========================================================

        public class TelemetryPayload
        {
            public double Lat { get; set; }
            public double Lng { get; set; }
            public List<DtcPayload>? Dtcs { get; set; }
        }

        public class DtcPayload
        {
            public string DtcCode { get; set; } = "";
            public int StatusByte { get; set; }
        }

        public class SessionResultPayload
        {
            public string? Protocol { get; set; }
            public List<DtcPayload>? Dtcs { get; set; }
        }

        public class SessionInfoPayload
        {
            public string? Vin { get; set; }
            public string? CalId { get; set; }
            public string? Cvn { get; set; }
            public string? Hardware { get; set; }
        }
        public class FreezeFramePayload
        {
            public string Dtc { get; set; } = "";
            public int Rpm { get; set; }
            public int Speed { get; set; }
            public int Coolant { get; set; }
        }
    }
}