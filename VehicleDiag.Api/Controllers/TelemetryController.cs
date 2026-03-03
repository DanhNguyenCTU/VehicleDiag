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

  
        public record TelemetryRequest(
            double Lat,
            double Lng
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

            // 1️⃣ Xác thực device bằng DeviceKey
            var device = await ValidateDeviceAsync();
            if (device == null)
                return Unauthorized();

            // 2️⃣ Lưu telemetry
            _db.Telemetry.Add(new Telemetry
            {
                DeviceId = device.DeviceId,   
                Lat = req.Lat,
                Lng = req.Lng,
                CreatedAt = DateTime.UtcNow
            });

            
            device.LastSeenAt = DateTime.UtcNow;

            await _db.SaveChangesAsync();

          
            return NoContent();
        }
    }
}