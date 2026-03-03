using Microsoft.AspNetCore.Mvc;
using VehicleDiag.Api.Data;
using Microsoft.EntityFrameworkCore;

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

        public record TelemetryRequest(
            string DeviceId,
            double Lat,
            double Lng
        );

        private async Task<bool> ValidateDeviceKeyAsync(string deviceId)
        {
            if (!Request.Headers.TryGetValue("DeviceKey", out var deviceKey))
                return false;

            var device = await _db.Devices
                .FirstOrDefaultAsync(x => x.DeviceId == deviceId && x.IsActive);

            if (device == null)
                return false;

            return device.DeviceKey == deviceKey;
        }

        [HttpPost]
        public async Task<IActionResult> Submit([FromBody] TelemetryRequest req)
        {
            if (req == null || string.IsNullOrWhiteSpace(req.DeviceId))
                return BadRequest();

            if (!await ValidateDeviceKeyAsync(req.DeviceId))
                return Unauthorized();

            var entity = new Telemetry
            {
                DeviceId = req.DeviceId,
                Lat = req.Lat,
                Lng = req.Lng,
                CreatedAt = DateTime.UtcNow
            };

            _db.Telemetry.Add(entity);

            var device = await _db.Devices
                .FirstOrDefaultAsync(d => d.DeviceId == req.DeviceId);

            if (device != null)
                device.LastSeenAt = DateTime.UtcNow;

            await _db.SaveChangesAsync();

            return Ok(new { ok = true });
        }
    }
}
