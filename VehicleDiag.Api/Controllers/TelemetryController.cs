using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using System.Globalization;
using VehicleDiag.Api.Data;
using VehicleDiag.Api.Models;

namespace VehicleDiag.Api.Controllers
{
    [ApiController]
    [Route("api/telemetry")]
    public class TelemetryController : ControllerBase
    {
        private readonly AppDbContext _db;

        public TelemetryController(AppDbContext db)
        {
            _db = db;
        }


        public record DtcDto(
            string DtcCode,
            int StatusByte
        );

        public record TelemetryRequest(
            double Lat,
            double Lng,
            List<DtcDto>? Dtcs,
            string? MqttTime
        );

        // ===== Lấy Device từ DeviceKey header =====
        private async Task<Device?> ValidateDeviceAsync()
        {
            if (!Request.Headers.TryGetValue("DeviceKey", out var deviceKeyValues))
                return null;

            var deviceKey = deviceKeyValues.ToString();

            return await _db.Devices
                .FirstOrDefaultAsync(x =>
                    x.DeviceKey == deviceKey &&
                    x.IsActive);
        }

        private static DateTime? ParsePayloadTimeUtc(string? raw)
        {
            if (string.IsNullOrWhiteSpace(raw))
                return null;

            if (!DateTimeOffset.TryParse(
                    raw,
                    CultureInfo.InvariantCulture,
                    DateTimeStyles.AllowWhiteSpaces | DateTimeStyles.AssumeUniversal,
                    out var dto))
            {
                return null;
            }

            return dto.UtcDateTime;
        }

        [HttpPost]
        public async Task<IActionResult> Submit([FromBody] TelemetryRequest req)
        {
            if (req == null)
                return BadRequest();

            var device = await ValidateDeviceAsync();
            if (device == null)
                return Unauthorized();

            var vehicle = await _db.Vehicles
                .FirstOrDefaultAsync(v => v.DeviceId == device.DeviceId);

            if (vehicle == null)
                return BadRequest("Device not bound to vehicle");

            var now = DateTime.UtcNow;
            var sentAtUtc = ParsePayloadTimeUtc(req.MqttTime);

            // ================== 1. Lưu Telemetry ==================
            _db.Telemetry.Add(new Telemetry
            {
                DeviceId = device.DeviceId,
                Lat = req.Lat,
                Lng = req.Lng,
                SentAtUtc = sentAtUtc,
                CreatedAt = now
            });

            device.LastSeenAt = now;

            // ================== 2. Xử lý DTC Snapshot ==================

            var incomingDtcs = req.Dtcs ?? new List<DtcDto>();

            var currentDbDtcs = await _db.EcuDtcCurrent
                .Where(x => x.VehicleId == vehicle.VehicleId)
                .ToListAsync();

            var openHistories = await _db.EcuDtcHistory
                .Where(h => h.VehicleId == vehicle.VehicleId &&
                            h.ClearedAt == null)
                .ToListAsync();

            var currentDict = currentDbDtcs.ToDictionary(x => x.DtcCode);
            var historyDict = openHistories.ToDictionary(x => x.DtcCode);

            var incomingCodes = incomingDtcs
                .Select(x => x.DtcCode)
                .ToHashSet();

            // ===== A. Lỗi mới & update =====
            foreach (var dtc in incomingDtcs)
            {
                if (!currentDict.TryGetValue(dtc.DtcCode, out var existing))
                {
                    // 🔥 Lỗi mới
                    _db.EcuDtcCurrent.Add(new EcuDtcCurrent
                    {
                        VehicleId = vehicle.VehicleId,
                        DtcCode = dtc.DtcCode,
                        StatusByte = dtc.StatusByte,
                        LastSeenAt = now,
                        SentAtUtc = sentAtUtc
                    });

                    _db.EcuDtcHistory.Add(new EcuDtcHistory
                    {
                        VehicleId = vehicle.VehicleId,
                        DtcCode = dtc.DtcCode,
                        StatusByte = dtc.StatusByte,
                        FirstSeenAt = now,
                        LastSeenAt = now,
                        SentAtUtc = sentAtUtc
                    });
                }
                else
                {
                    // 🔥 Vẫn còn lỗi
                    existing.StatusByte = dtc.StatusByte;
                    existing.LastSeenAt = now;
                    existing.SentAtUtc = sentAtUtc;

                    if (historyDict.TryGetValue(dtc.DtcCode, out var history))
                    {
                        history.LastSeenAt = now;
                        history.SentAtUtc = sentAtUtc;
                    }
                }
            }

            // ===== B. Lỗi đã clear =====
            foreach (var dbDtc in currentDbDtcs)
            {
                if (!incomingCodes.Contains(dbDtc.DtcCode))
                {
                    if (historyDict.TryGetValue(dbDtc.DtcCode, out var history))
                    {
                        history.ClearedAt = now;
                    }

                    _db.EcuDtcCurrent.Remove(dbDtc);
                }
            }

            await _db.SaveChangesAsync();

            return NoContent();
        }
    }
}
