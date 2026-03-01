using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using System.Security.Claims;
using VehicleDiag.Api.Data;

namespace VehicleDiag.Api.Controllers;

[Authorize(Roles = "Technician,Admin,Viewer")]
[ApiController]
[Route("api/devices")]
public class DevicesController : ControllerBase
{
    private readonly AppDbContext _db;

    public DevicesController(AppDbContext db)
    {
        _db = db;
    }

    // ================= ONLINE DEVICES =================
    [HttpGet("online")]
    public async Task<IActionResult> GetOnlineDevices()
    {
        var role = User.FindFirst(ClaimTypes.Role)?.Value;
        var userIdClaim = User.FindFirst(ClaimTypes.NameIdentifier)?.Value;

        if (role == null || userIdClaim == null)
            return Unauthorized();

        int userId = int.Parse(userIdClaim);

        var threshold = DateTime.UtcNow.AddSeconds(-30);

        if (role == "Viewer")
            return await GetViewerOnlineDevices(userId, threshold);

        return await GetAdminTechOnlineDevices(threshold);
    }

    // ================= VIEWER =================
    private async Task<IActionResult> GetViewerOnlineDevices(int userId, DateTime threshold)
    {
        var devices = await (
            from uv in _db.UserVehicles.AsNoTracking()
            join v in _db.Vehicles.AsNoTracking()
                on uv.VehicleId equals v.VehicleId
            join d in _db.Devices.AsNoTracking()
                on v.DeviceId equals d.DeviceId
            where uv.UserId == userId
               && d.IsActive
               && d.LastSeenAt.HasValue
               && d.LastSeenAt > threshold
            select new
            {
                deviceId = d.DeviceId,
                vehicleName = v.PlateNumber,
                lastSeenAt = d.LastSeenAt
            }
        ).ToListAsync();

        return Ok(devices);
    }

    // ================= ADMIN / TECH =================
    private async Task<IActionResult> GetAdminTechOnlineDevices(DateTime threshold)
    {
        var devices = await (
            from d in _db.Devices.AsNoTracking()
            join v in _db.Vehicles.AsNoTracking()
                on d.DeviceId equals v.DeviceId
            where d.IsActive                      
               && d.LastSeenAt.HasValue
               && d.LastSeenAt > threshold
            select new
            {
                deviceId = d.DeviceId,
                vehicleName = v.PlateNumber,
                lastSeenAt = d.LastSeenAt
            }
        ).ToListAsync();

        return Ok(devices);
    }
}