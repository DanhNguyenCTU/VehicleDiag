using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using System.Security.Claims;
using VehicleDiag.Api.Data;
using VehicleDiag.Api.Models;

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

    // ================= HEALTH =================
    [AllowAnonymous]
    [HttpGet("health")]
    public IActionResult Health()
    {
        return Ok(new { status = "ok" });
    }

    // ================= STATUS =================
    [HttpGet("status")]
    public async Task<IActionResult> Status()
    {
        var role = User.FindFirst(ClaimTypes.Role)?.Value;
        var userIdClaim = User.FindFirst(ClaimTypes.NameIdentifier)?.Value;

        if (role == null || userIdClaim == null)
            return Unauthorized();

        int userId = int.Parse(userIdClaim);

        if (role == "Viewer")
            return await GetViewerDevice(userId);

        return await GetAdminOrTechnicianDevice();
    }

    // ================= VIEWER =================
    private async Task<IActionResult> GetViewerDevice(int userId)
    {
        var vehicle = await _db.UserVehicles
            .Where(uv => uv.UserId == userId)
            .Select(uv => uv.Vehicle)
            .Include(v => v.Device)
            .FirstOrDefaultAsync();

        if (vehicle == null || vehicle.Device == null)
            return NotFound("No device assigned to this user.");

        var device = vehicle.Device;

        var online = device.LastSeenAt.HasValue &&
                     device.LastSeenAt > DateTime.UtcNow.AddSeconds(-30);

        return Ok(new
        {
            deviceId = device.DeviceId,
            status = online ? "connected" : "disconnected",
            lastSeenUtc = device.LastSeenAt
        });
    }

    // ================= ADMIN / TECH =================
    private async Task<IActionResult> GetAdminOrTechnicianDevice()
    {
        var device = await _db.Devices
            .OrderByDescending(d => d.LastSeenAt)
            .FirstOrDefaultAsync();

        if (device == null)
            return NotFound("No device found.");

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