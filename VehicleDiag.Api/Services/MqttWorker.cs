using MQTTnet;
using MQTTnet.Client;
using System.Text;
using System.Text.Json;
using Microsoft.EntityFrameworkCore;
using VehicleDiag.Api.Data;
using VehicleDiag.Api.Models;

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
                .WithTcpServer(
                    "dc3f111e040f4e8d9b48df7616b1b80e.s1.eu.hivemq.cloud",
                    8883)
                .WithCredentials("ds_backend", "YOUR_PASSWORD")
                .WithTls()
                .Build();

            await _client.ConnectAsync(options, stoppingToken);

            Console.WriteLine("MQTT connected");

            await _client.SubscribeAsync("vehicle/+/telemetry");

            Console.WriteLine("Subscribed vehicle telemetry");

            await Task.Delay(Timeout.Infinite, stoppingToken);
        }

        private async Task HandleMessage(MqttApplicationMessageReceivedEventArgs e)
        {
            try
            {
                if (e.ApplicationMessage.PayloadSegment.Count == 0)
                    return;

                var topic = e.ApplicationMessage.Topic;

                var payload = Encoding.UTF8.GetString(
                    e.ApplicationMessage.PayloadSegment);

                Console.WriteLine($"MQTT {topic} -> {payload}");

                var data = JsonSerializer.Deserialize<TelemetryPayload>(payload);

                if (data == null)
                    return;

                var parts = topic.Split('/');

                if (parts.Length < 3)
                    return;

                var deviceId = parts[1];

                using var scope = _scopeFactory.CreateScope();

                var db = scope.ServiceProvider
                    .GetRequiredService<AppDbContext>();

                var device = await db.Devices
                    .FirstOrDefaultAsync(x =>
                        x.DeviceId == deviceId &&
                        x.IsActive);

                if (device == null)
                    return;

                var vehicle = await db.Vehicles
                    .FirstOrDefaultAsync(v =>
                        v.DeviceId == deviceId);

                if (vehicle == null)
                    return;

                var now = DateTime.UtcNow;

                // ================= TELEMETRY =================

                db.Telemetry.Add(new Telemetry
                {
                    DeviceId = deviceId,
                    Lat = data.Lat,
                    Lng = data.Lng,
                    CreatedAt = now
                });

                device.LastSeenAt = now;

                // ================= DTC SNAPSHOT =================

                var incomingDtcs = data.Dtcs ?? new List<DtcPayload>();

                var currentDbDtcs = await db.EcuDtcCurrent
                    .Where(x => x.VehicleId == vehicle.VehicleId)
                    .ToListAsync();

                var openHistories = await db.EcuDtcHistory
                    .Where(h =>
                        h.VehicleId == vehicle.VehicleId &&
                        h.ClearedAt == null)
                    .ToListAsync();

                var currentDict = currentDbDtcs
                    .ToDictionary(x => x.DtcCode);

                var historyDict = openHistories
                    .ToDictionary(x => x.DtcCode);

                var incomingCodes = incomingDtcs
                    .Select(x => x.DtcCode)
                    .ToHashSet();

                // ===== NEW / UPDATE =====

                foreach (var dtc in incomingDtcs)
                {
                    if (!currentDict.TryGetValue(dtc.DtcCode, out var existing))
                    {
                        db.EcuDtcCurrent.Add(new EcuDtcCurrent
                        {
                            VehicleId = vehicle.VehicleId,
                            DtcCode = dtc.DtcCode,
                            StatusByte = dtc.StatusByte,
                            LastSeenAt = now
                        });

                        db.EcuDtcHistory.Add(new EcuDtcHistory
                        {
                            VehicleId = vehicle.VehicleId,
                            DtcCode = dtc.DtcCode,
                            StatusByte = dtc.StatusByte,
                            FirstSeenAt = now,
                            LastSeenAt = now
                        });
                    }
                    else
                    {
                        existing.StatusByte = dtc.StatusByte;
                        existing.LastSeenAt = now;

                        if (historyDict.TryGetValue(dtc.DtcCode, out var history))
                        {
                            history.LastSeenAt = now;
                        }
                    }
                }

                // ===== CLEARED DTC =====

                foreach (var dbDtc in currentDbDtcs)
                {
                    if (!incomingCodes.Contains(dbDtc.DtcCode))
                    {
                        if (historyDict.TryGetValue(dbDtc.DtcCode, out var history))
                        {
                            history.ClearedAt = now;
                        }

                        db.EcuDtcCurrent.Remove(dbDtc);
                    }
                }

                await db.SaveChangesAsync();
            }
            catch (Exception ex)
            {
                Console.WriteLine(ex.Message);
            }
        }

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
    }
}