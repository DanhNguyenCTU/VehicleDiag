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
    [HttpGet("all")]
    public async Task<IActionResult> GetAllDevices()
    {
        var role = User.FindFirst(ClaimTypes.Role)?.Value;
        var userIdClaim = User.FindFirst(ClaimTypes.NameIdentifier)?.Value;

        if (role == null || userIdClaim == null)
            return Unauthorized();

        int userId = int.Parse(userIdClaim);

        var threshold = DateTime.UtcNow.AddSeconds(-30);

        if (role == "Viewer")
            return await GetViewerDevices(userId, threshold);

        return await GetAdminTechDevices(threshold);
    }

    // ================= VIEWER =================
    private async Task<IActionResult> GetViewerDevices(int userId, DateTime threshold)
    {
        var devices = await (
            from uv in _db.UserVehicles.AsNoTracking()
            join v in _db.Vehicles.AsNoTracking()
                on uv.VehicleId equals v.VehicleId
            join d in _db.Devices.AsNoTracking()
                on v.DeviceId equals d.DeviceId
            join vm in _db.VehicleModels.AsNoTracking()
                on v.ModelId equals vm.ModelId into vmJoin
            from vm in vmJoin.DefaultIfEmpty()

            where uv.UserId == userId
               && d.IsActive
               && v.IsActive

            select new
            {
                deviceId = d.DeviceId,

                vehicleName =
                    (vm != null
                        ? vm.Brand + " " + vm.Model + " " + vm.Year
                        : "")
                    + " - " + v.PlateNumber,

                lastSeenAt = d.LastSeenAt,

                isOnline = d.LastSeenAt.HasValue
                           && d.LastSeenAt > threshold
            }
        ).ToListAsync();

        return Ok(devices);
    }

    // ================= ADMIN / TECH =================
    private async Task<IActionResult> GetAdminTechDevices(DateTime threshold)
    {
        var devices = await (
            from d in _db.Devices.AsNoTracking()
            join v in _db.Vehicles.AsNoTracking()
                on d.DeviceId equals v.DeviceId
            join vm in _db.VehicleModels.AsNoTracking()
                on v.ModelId equals vm.ModelId into vmJoin
            from vm in vmJoin.DefaultIfEmpty()

            where d.IsActive
               && v.IsActive

            select new
            {
                deviceId = d.DeviceId,

                vehicleName =
                    (vm != null
                        ? vm.Brand + " " + vm.Model + " " + vm.Year
                        : "")
                    + " - " + v.PlateNumber,

                lastSeenAt = d.LastSeenAt,

                isOnline = d.LastSeenAt.HasValue
                           && d.LastSeenAt > threshold
            }
        ).ToListAsync();

        return Ok(devices);
    }
    // ================= DEVICE STATUS BY VEHICLE =================
    [Authorize(Roles = "Technician,Admin")]
    [HttpGet("vehicle/{vehicleId:int}/status")]
    public async Task<IActionResult> GetVehicleDeviceStatus(int vehicleId)
    {
        var threshold = DateTime.UtcNow.AddSeconds(-30);

        var result = await (
            from v in _db.Vehicles.AsNoTracking()
            join d in _db.Devices.AsNoTracking()
                on v.DeviceId equals d.DeviceId
            where v.VehicleId == vehicleId
               && v.IsActive
               && d.IsActive
            select new
            {
                vehicleId = v.VehicleId,
                deviceId = d.DeviceId,
                lastSeenAt = d.LastSeenAt,
                isOnline = d.LastSeenAt.HasValue &&
                           d.LastSeenAt > threshold
            }
        ).FirstOrDefaultAsync();

        if (result == null)
            return NotFound("Vehicle or device not found");

        return Ok(result);
    }
}