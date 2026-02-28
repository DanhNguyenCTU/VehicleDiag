using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using System.Security.Claims;
using VehicleDiag.Api.Data;

namespace VehicleDiag.Api.Controllers;

[Authorize(Roles = "Technician,Admin,Viewer")]
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
    public async Task<IActionResult> Status()
    {
        var role = User.FindFirst(ClaimTypes.Role)!.Value;
        var userId = int.Parse(
            User.FindFirst(ClaimTypes.NameIdentifier)!.Value);

        IQueryable<Device> query;

        if (role == "Viewer")
        {
            query =
                from d in _db.Devices
                join v in _db.Vehicles on d.DeviceId equals v.DeviceId
                join uv in _db.UserVehicles on v.VehicleId equals uv.VehicleId
                where uv.UserId == userId
                select d;
        }
        else
        {
            // Admin / Technician thấy tất cả
            query = _db.Devices;
        }

        var device = await query
            .OrderByDescending(d => d.LastSeenAt)
            .FirstOrDefaultAsync();

        if (device == null)
            return NotFound("No device found");

        var online = device.LastSeenAt.HasValue &&
                     device.LastSeenAt > DateTime.UtcNow.AddSeconds(-30);

        return Ok(new
        {
            deviceId = device.DeviceId,
            status = online ? "connected" : "disconnected",
            lastSeenUtc = device.LastSeenAt
        });
    }
}