using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
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
            List<DtcDto>? Dtcs
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

        [HttpPost]
        public async Task<IActionResult> Submit([FromBody] TelemetryRequest req)
        {
            if (req == null)
                return BadRequest();

            var device = await ValidateDeviceAsync();
            if (device == null)
                return Unauthorized();

            var now = DateTime.UtcNow;

            // Lưu telemetry
            _db.Telemetry.Add(new Telemetry
            {
                DeviceId = device.DeviceId,
                Lat = req.Lat,
                Lng = req.Lng,
                CreatedAt = now
            });

            // Lưu DTC nếu có
            if (req.Dtcs != null && req.Dtcs.Any())
            {
                foreach (var d in req.Dtcs)
                {
                    _db.DtcLogs.Add(new DtcLog
                    {
                        DeviceId = device.DeviceId,
                        DtcCode = d.DtcCode,
                        StatusByte = d.StatusByte,
                        Source = "AUTO",
                        CreatedAt = now
                    });
                }
            }

            device.LastSeenAt = now;

            await _db.SaveChangesAsync();

            return NoContent();
        }
    }
}