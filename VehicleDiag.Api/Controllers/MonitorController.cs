using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using System.Security.Claims;
using VehicleDiag.Api.Data;
using VehicleDiag.Api.Models;

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
    // LẤY VỊ TRÍ TẤT CẢ XE CỦA USER
    // ==============================
    [HttpGet("my-vehicles/location")]
    public async Task<IActionResult> GetMyVehiclesLocation()
    {
        var role = User.FindFirst(ClaimTypes.Role)?.Value;
        var userId = int.Parse(User.FindFirst(ClaimTypes.NameIdentifier)!.Value);

        IQueryable<Vehicle> query = _db.Vehicles
            .Where(v => v.IsActive);

        if (role == "Viewer")
        {
            var myVehicleIds = await _db.UserVehicles
                .Where(x => x.UserId == userId)
                .Select(x => x.VehicleId)
                .ToListAsync();

            query = query.Where(v => myVehicleIds.Contains(v.VehicleId));
        }

        var vehicles = await query
            .Include(v => v.VehicleModel)   // 🔥 QUAN TRỌNG
            .ToListAsync();

        var result = new List<object>();

        foreach (var vehicle in vehicles)
        {
            if (string.IsNullOrWhiteSpace(vehicle.DeviceId))
                continue;

            var latest = await _db.Telemetry
                .Where(t => t.DeviceId == vehicle.DeviceId)
                .OrderByDescending(t => t.CreatedAt)
                .FirstOrDefaultAsync();

            if (latest == null)
                continue;

            result.Add(new
            {
                vehicle.VehicleId,
                Name = $"{vehicle.VehicleModel.Brand} {vehicle.VehicleModel.Model}",
                latest.Lat,
                latest.Lng,
                latest.EngineOn,
                latest.CreatedAt
            });
        }

        return Ok(result);
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