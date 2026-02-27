using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using System.Security.Claims;
using VehicleDiag.Api.Data;

namespace VehicleDiag.Api.Controllers;

[Authorize(Roles = "Viewer,Technician,Admin")]
[ApiController]
[Route("api/monitor")]
public class MonitorController : ControllerBase
{
    private readonly AppDbContext _db;

    public MonitorController(AppDbContext db)
    {
        _db = db;
    }

    // ==============================
    // LẤY VỊ TRÍ HIỆN TẠI
    // ==============================
    [HttpGet("vehicle/{vehicleId:int}/location")]
    public async Task<IActionResult> GetLocation(int vehicleId)
    {
        var vehicle = await _db.Vehicles
            .FirstOrDefaultAsync(v => v.VehicleId == vehicleId && v.IsActive);

        if (vehicle == null)
            return NotFound("Vehicle not found");

        var role = User.FindFirst(ClaimTypes.Role)?.Value;

        if (role == "Viewer")
        {
            var userId = int.Parse(User.FindFirst(ClaimTypes.NameIdentifier)!.Value);

            var hasAccess = await _db.UserVehicles
                .AnyAsync(x => x.UserId == userId &&
                               x.VehicleId == vehicleId);

            if (!hasAccess)
                return Forbid();
        }

        if (string.IsNullOrWhiteSpace(vehicle.DeviceId))
            return BadRequest("No device assigned");

        var latest = await _db.Telemetry
            .Where(t => t.DeviceId == vehicle.DeviceId)
            .OrderByDescending(t => t.CreatedAt)
            .FirstOrDefaultAsync();

        if (latest == null)
            return NoContent();

        return Ok(new
        {
            latest.Lat,
            latest.Lng,
            latest.EngineOn,
            latest.CreatedAt
        });
    }

    // ==============================
    // LẤY DTC HIỆN TẠI
    // ==============================
    [HttpGet("vehicle/{vehicleId:int}/dtc")]
    public async Task<IActionResult> GetCurrentDtcs(int vehicleId)
    {
        var vehicle = await _db.Vehicles
            .FirstOrDefaultAsync(v => v.VehicleId == vehicleId && v.IsActive);

        if (vehicle == null)
            return NotFound("Vehicle not found");

        var role = User.FindFirst(ClaimTypes.Role)?.Value;

        if (role == "Viewer")
        {
            var userId = int.Parse(User.FindFirst(ClaimTypes.NameIdentifier)!.Value);

            var hasAccess = await _db.UserVehicles
                .AnyAsync(x => x.UserId == userId &&
                               x.VehicleId == vehicleId);

            if (!hasAccess)
                return Forbid();
        }

        var dtcs = await (
            from cur in _db.EcuDtcCurrent
            join dict in _db.DtcDictionary
                on cur.DtcCode equals dict.DtcCode into gj
            from dict in gj.DefaultIfEmpty()
            where cur.VehicleId == vehicleId
            select new
            {
                cur.DtcCode,
                Description = dict != null ? dict.Description : "Unknown fault"
            }
        ).ToListAsync();

        return Ok(dtcs);
    }
}