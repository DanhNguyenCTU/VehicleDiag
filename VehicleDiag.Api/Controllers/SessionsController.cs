using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using System.Security.Claims;
using VehicleDiag.Api.Data;
using VehicleDiag.Api.Services;
using VehicleDiag.Api.Dtos.Requests;
using VehicleDiag.Api.Models;
using VehicleDiag.Api.Constants;
using System.Linq;
namespace VehicleDiag.Api.Controllers;

[Authorize(Roles = "Technician,Admin")]
[ApiController]
[Route("api/sessions")]
public class SessionsController : ControllerBase
{
    private readonly AppDbContext _db;
    private readonly IConfiguration _cfg;
    public SessionsController(AppDbContext db, IConfiguration cfg)
    {
        _db = db;
        _cfg = cfg;
    }

    // =========================
    // ESP32 DTOs
    // =========================
    public record Esp32DtcItem(string DtcCode, byte StatusByte);
    public record Esp32SubmitDtcsReq(
    string Protocol,
    List<Esp32DtcItem>? Dtcs
);


    public record Esp32SubmitInfoReq(
        string? Protocol,
        string? Vin,
        string? CalId,
        string? Cvn,
        string? Hardware
    );

    public record Esp32SubmitInfoKvReq(
        string Protocol,
        string InfoKey,
        string InfoValue
    );

    // =========================================================
    // UI → CREATE SESSION (VEHICLE-FIRST)
    // =========================================================
    [Authorize]
    [HttpPost]
    public async Task<IActionResult> Create([FromBody] CreateSessionReq req)
    {
        var userIdStr = User.FindFirst(ClaimTypes.NameIdentifier)?.Value;
        if (userIdStr == null)
            return Unauthorized();

        var vehicle = await _db.Vehicles
            .Include(v => v.VehicleModel)
            .FirstOrDefaultAsync(v => v.VehicleId == req.VehicleId && v.IsActive);

        if (vehicle == null)
            return BadRequest("Invalid vehicle");

        if (string.IsNullOrWhiteSpace(vehicle.DeviceId))
            return BadRequest("No device assigned to this vehicle");

        string action = req.ActionType?.Trim().ToUpper() ?? "";

        if (action == "READ_INFO") action = SessionActionType.READ_INFO;
        else if (action == "READ_DTC") action = SessionActionType.READ_DTC;
        else if (action == "CLEAR_DTC") action = SessionActionType.CLEAR_DTC;

        if (action != SessionActionType.READ_INFO &&
            action != SessionActionType.READ_DTC &&
            action != SessionActionType.CLEAR_DTC)
            return BadRequest("Invalid ActionType");

        string protocol = string.IsNullOrWhiteSpace(req.Protocol)
            ? "OBDII"
            : req.Protocol.Trim().ToUpper();

        if (protocol != "OBDII" && protocol != "KWP")
            return BadRequest("Invalid protocol");

        var session = new EcuReadSession
        {
            CreatedAt = DateTime.UtcNow,
            ActionType = action,
            DeviceId = vehicle.DeviceId!,
            VehicleId = vehicle.VehicleId,
            Protocol = protocol,
            CreatedByUserId = int.Parse(userIdStr),
            Status = SessionStatus.Pending
        };

        _db.EcuReadSessions.Add(session);
        await _db.SaveChangesAsync();

        return Ok(new { sessionId = session.SessionId });
    }

    public record Esp32PendingReq(
        string DeviceId,
        string Firmware
    );
    private bool ValidateDeviceKey(string deviceId)
    {
        if (!Request.Headers.TryGetValue("DeviceKey", out var deviceKey))
            return false;

        var expectedKey = _cfg[$"DeviceKeys:{deviceId}"];

        if (string.IsNullOrWhiteSpace(expectedKey))
            return false;

        return expectedKey == deviceKey;
    }

    // =========================================================
    // ESP32 → SUBMIT DTCs
    // =========================================================
    [AllowAnonymous]
    [HttpPost("{sessionId:int}/dtcs")]
    public async Task<IActionResult> SubmitDtcsFromEsp32(
       int sessionId,
       [FromBody] Esp32SubmitDtcsReq req)
    {

        if (req == null || string.IsNullOrWhiteSpace(req.Protocol))
            return BadRequest("Invalid payload");

        if (req.Protocol != "OBDII" && req.Protocol != "KWP")
            return BadRequest("Invalid protocol");

        var session = await _db.EcuReadSessions
            .FirstOrDefaultAsync(x => x.SessionId == sessionId);

        if (session == null)
            return NotFound("Session not found");

        if (!ValidateDeviceKey(session.DeviceId))
            return Unauthorized("Invalid device key");

        if (session.Status != SessionStatus.Processing)
            return BadRequest("Invalid session state");

        if (!string.Equals(session.Protocol, req.Protocol, StringComparison.OrdinalIgnoreCase))
            return BadRequest("Protocol mismatch");

        int vehicleId = session.VehicleId;
        var now = DateTime.UtcNow;

        var currentDbDtcs = await _db.EcuDtcCurrent
            .Where(x => x.VehicleId == vehicleId)
            .ToListAsync();

        var incomingDtcs = req.Dtcs ?? new List<Esp32DtcItem>();

        var incomingCodes = incomingDtcs
            .Where(x => !string.IsNullOrWhiteSpace(x.DtcCode))
            .Select(x => x.DtcCode.Trim())
            .ToHashSet();

        // Remove DTC không còn tồn tại
        var toRemove = currentDbDtcs
            .Where(db => !incomingCodes.Contains(db.DtcCode))
            .ToList();

        if (toRemove.Any())
            _db.EcuDtcCurrent.RemoveRange(toRemove);

        foreach (var d in incomingDtcs)
        {
            if (string.IsNullOrWhiteSpace(d.DtcCode))
                continue;

            var code = d.DtcCode.Trim();

            _db.EcuDtcResults.Add(new EcuDtcResult
            {
                SessionId = sessionId,
                DtcCode = code,
                StatusByte = d.StatusByte,
                Protocol = req.Protocol
            });

            var existing = currentDbDtcs
                .FirstOrDefault(x => x.DtcCode == code);

            if (existing == null)
            {
                _db.EcuDtcCurrent.Add(new EcuDtcCurrent
                {
                    VehicleId = vehicleId,
                    DtcCode = code,
                    StatusByte = d.StatusByte,
                    LastSeenAt = now
                });
            }
            else
            {
                existing.StatusByte = d.StatusByte;
                existing.LastSeenAt = now;
            }
        }

        await _db.SaveChangesAsync();
        return Ok(new { ok = true });
    }


    // =========================================================
    // ESP32 → GET PENDING SESSION
    // =========================================================
    [AllowAnonymous]
    [HttpPost("pending")]
    public async Task<IActionResult> GetPending([FromBody] Esp32PendingReq req)
    {
        if (req == null || string.IsNullOrWhiteSpace(req.DeviceId))
            return BadRequest("Invalid payload");

        // 🔐 Validate DeviceKey từ HEADER
        if (!ValidateDeviceKey(req.DeviceId))
            return Unauthorized("Invalid device key");

        var device = await _db.Devices
            .FirstOrDefaultAsync(x => x.DeviceId == req.DeviceId && x.IsActive);

        if (device == null)
            return Unauthorized("Unknown device");

        device.LastSeenAt = DateTime.UtcNow;
        await _db.SaveChangesAsync();

        var timeout = DateTime.UtcNow.AddMinutes(-5);

        var expired = await _db.EcuReadSessions
            .Where(x => x.DeviceId == req.DeviceId &&
                        x.Status == SessionStatus.Processing &&
                        x.CreatedAt < timeout)
            .ToListAsync();

        foreach (var s in expired)
            s.Status = SessionStatus.Failed;

        if (expired.Count > 0)
            await _db.SaveChangesAsync();

        var session = await _db.EcuReadSessions
            .Where(x => x.DeviceId == req.DeviceId &&
                        x.Status == SessionStatus.Pending)
            .OrderBy(x => x.CreatedAt)
            .FirstOrDefaultAsync();

        if (session == null)
            return NoContent();

        session.Status = SessionStatus.Processing;
        await _db.SaveChangesAsync();

        var vehicle = await _db.Vehicles
            .Include(v => v.VehicleModel)
            .FirstOrDefaultAsync(v => v.VehicleId == session.VehicleId);

        if (vehicle == null || vehicle.VehicleModel == null)
        {
            session.Status = SessionStatus.Failed;
            await _db.SaveChangesAsync();
            return BadRequest("Vehicle not found");
        }

        return Ok(new
        {
            sessionId = session.SessionId,
            actionType = session.ActionType,
            protocol = session.Protocol,
            brand = vehicle.VehicleModel.Brand,
            model = vehicle.VehicleModel.Model,
            year = vehicle.VehicleModel.Year
        });
    }


    // =========================================================
    // ESP32 → SUBMIT ECU INFO (OBD)
    // =========================================================
    [AllowAnonymous]
    [HttpPost("{sessionId:int}/info")]
    public async Task<IActionResult> SubmitInfoFromEsp32(
        int sessionId,
        [FromBody] Esp32SubmitInfoReq req)
    {
        if (req == null || string.IsNullOrWhiteSpace(req.Protocol))
            return BadRequest("Invalid payload");

        var session = await _db.EcuReadSessions
            .FirstOrDefaultAsync(x => x.SessionId == sessionId);

        if (session == null)
            return NotFound("Session not found");

        // 🔐 Validate header
        if (!ValidateDeviceKey(session.DeviceId))
            return Unauthorized("Invalid device key");

        if (session.Status != SessionStatus.Processing)
            return BadRequest("Invalid session state");

        if (!string.Equals(session.Protocol, req.Protocol, StringComparison.OrdinalIgnoreCase))
            return BadRequest("Protocol mismatch");

        _db.EcuInfoResults.RemoveRange(
            _db.EcuInfoResults.Where(x => x.SessionId == sessionId)
        );

        void Add(string key, string? val)
        {
            if (string.IsNullOrWhiteSpace(val)) return;

            _db.EcuInfoResults.Add(new EcuInfoResult
            {
                SessionId = sessionId,
                InfoKey = key,
                InfoLabel = key,
                InfoValue = val
            });
        }

        Add("VIN", req.Vin);
        Add("CALID", req.CalId);
        Add("CVN", req.Cvn);
        Add("HW", req.Hardware);

        await _db.SaveChangesAsync();
        return Ok(new { ok = true });
    }

    // =========================================================
    // ESP32 → COMPLETE SESSION
    // =========================================================
    [AllowAnonymous]
    [HttpPost("{sessionId:int}/complete")]
    public async Task<IActionResult> CompleteSession(int sessionId)
    {
        var session = await _db.EcuReadSessions
            .FirstOrDefaultAsync(x => x.SessionId == sessionId);

        if (session == null)
            return NotFound("Session not found");

        // 🔐 Validate header
        if (!ValidateDeviceKey(session.DeviceId))
            return Unauthorized("Invalid device key");

        if (session.Status != SessionStatus.Processing)
            return BadRequest("Invalid session state");

        session.Status = SessionStatus.Completed;
        session.CompletedAt = DateTime.UtcNow;

        await _db.SaveChangesAsync();
        return Ok(new { ok = true });
    }
}
