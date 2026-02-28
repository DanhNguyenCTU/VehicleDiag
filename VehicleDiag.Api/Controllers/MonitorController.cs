using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using System.Security.Claims;
using VehicleDiag.Api.Data;
using VehicleDiag.Api.Models;

namespace VehicleDiag.Api.Controllers;

[Authorize(Roles = "Viewer,Admin")]
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
                vehicle.PlateNumber,
                Name = $"{vehicle.VehicleModel.Brand} {vehicle.VehicleModel.Model}",
                latest.Lat,
                latest.Lng,
                latest.EngineOn,
                latest.CreatedAt
            });
        }

        return Ok(result);
    }

    [HttpGet("vehicle/{vehicleId:int}/dtc")]
    public async Task<IActionResult> GetCurrentDtcs(int vehicleId)
    {
        // 1️⃣ Lấy vehicle + model
        var vehicle = await _db.Vehicles
            .Include(v => v.VehicleModel)
            .FirstOrDefaultAsync(v => v.VehicleId == vehicleId && v.IsActive);

        if (vehicle == null || vehicle.VehicleModel == null)
            return NotFound("Vehicle not found");

        // 2️⃣ Kiểm tra quyền Viewer
        var role = User.FindFirst(ClaimTypes.Role)?.Value;

        if (role == "Viewer")
        {
            var userId = int.Parse(User.FindFirst(ClaimTypes.NameIdentifier)!.Value);

            var hasAccess = await _db.UserVehicles
                .AnyAsync(x => x.UserId == userId && x.VehicleId == vehicleId);

            if (!hasAccess)
                return Forbid();
        }

        // 3️⃣ Lấy GroupCode theo Brand
        var groupCode = await _db.ManufacturerBrands
            .Where(x => x.Brand == vehicle.VehicleModel.Brand)
            .Select(x => x.GroupCode)
            .FirstOrDefaultAsync();

        // 4️⃣ Lấy danh sách DTC hiện tại
        var currentCodes = await _db.EcuDtcCurrent
            .Where(x => x.VehicleId == vehicleId)
            .Select(x => x.DtcCode)
            .ToListAsync();

        if (!currentCodes.Any())
            return Ok(new List<object>());

        // 5️⃣ Lấy dictionary match (1 query duy nhất)
        var dictMatches = await _db.DtcDictionary
            .Where(d =>
                currentCodes.Contains(d.DtcCode) &&
                (
                    d.Scope == "Generic" ||
                    (groupCode != null &&
                     d.Scope == "Manufacturer" &&
                     d.GroupCode == groupCode)
                )
            )
            .ToListAsync();

        // 6️⃣ Ưu tiên Manufacturer nếu có
        var result = currentCodes.Select(code =>
        {
            var description = dictMatches
                .Where(d => d.DtcCode == code)
                .OrderBy(d => d.Scope == "Manufacturer" ? 0 : 1)
                .Select(d => d.Description)
                .FirstOrDefault();

            return new
            {
                DtcCode = code,
                Description = description ?? "Unknown fault"
            };
        }).ToList();

        return Ok(result);
    }
}