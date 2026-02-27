using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using VehicleDiag.Api.Data;

namespace VehicleDiag.Api.Controllers;

[Authorize(Roles = "Technician,Admin")]
[ApiController]
[Route("api/system")]
public class SystemController : ControllerBase
{
    private readonly AppDbContext _db;

    public SystemController(AppDbContext db)
    {
        _db = db;
    }

    [HttpGet("health")]
    public IActionResult Health()
    {
        return Ok(new { status = "ok" });
    }

    [HttpGet("status")]
    public async Task<IActionResult> Status(string deviceId)
    {
        if (string.IsNullOrWhiteSpace(deviceId))
            return BadRequest("deviceId is required");

        var device = await _db.Devices
            .FirstOrDefaultAsync(x => x.DeviceId == deviceId);

        if (device == null)
            return NotFound("Device not found");

        var online = device.LastSeenAt.HasValue &&
                     device.LastSeenAt > DateTime.UtcNow.AddSeconds(-30);

        return Ok(new
        {
            deviceId,
            status = online ? "connected" : "disconnected",
            lastSeenUtc = device.LastSeenAt
        });
    }
}