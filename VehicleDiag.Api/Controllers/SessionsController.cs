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
        if (!DeviceRuntimeState.IsConnected)
            return BadRequest("Device not connected");

        var userIdStr = User.FindFirst(ClaimTypes.NameIdentifier)?.Value;
        if (userIdStr == null)
            return Unauthorized();

        // Validate Vehicle
        var vehicle = await _db.Vehicles
            .FirstOrDefaultAsync(v => v.VehicleId == req.VehicleId && v.IsActive);

        if (vehicle == null)
            return BadRequest("Invalid vehicle");

        // Normalize + Validate ActionType 
        if (string.IsNullOrWhiteSpace(req.ActionType))
            return BadRequest("ActionType is required");

        string action = req.ActionType.Trim();

        // CHẤP NHẬN CẢ UI CŨ + ESP32
        if (action.Equals("READ_INFO", StringComparison.OrdinalIgnoreCase))
            action = "ReadInfo";
        else if (action.Equals("READ_DTC", StringComparison.OrdinalIgnoreCase))
            action = "ReadDTC";
        else if (action.Equals("CLEAR_DTC", StringComparison.OrdinalIgnoreCase))
            action = "ClearDTC";

        // Validate theo format ESP32
        if (action != SessionActionType.READ_INFO &&
            action != SessionActionType.READ_DTC &&
            action != SessionActionType.CLEAR_DTC)
        {
            return BadRequest($"Invalid ActionType: {req.ActionType}");
        }

        string protocol = string.IsNullOrWhiteSpace(req.Protocol)
            ? "OBDII"
            : req.Protocol.Trim().ToUpper();

        // Chỉ cho phép protocol hợp lệ
        if (protocol != "OBDII" && protocol != "KWP")
            return BadRequest("Invalid protocol");



        // 4️⃣ Create SESSION
        var session = new EcuReadSession
        {
            CreatedAt = DateTime.UtcNow,
            ActionType = action,                 // ReadInfo / ReadDTC / ClearDTC
            DeviceId = DeviceRuntimeState.DeviceName,

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
        string DeviceKey,
        string Firmware
    );

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

        if (session.Status != SessionStatus.Processing)
            return BadRequest("Invalid session state");

        if (!string.Equals(session.Protocol, req.Protocol, StringComparison.OrdinalIgnoreCase))
            return BadRequest("Protocol mismatch");

        int vehicleId = session.VehicleId;
        var now = DateTime.UtcNow;

        try
        {
            if (!(req.Dtcs?.Any() ?? false))
            {
                var olds = await _db.EcuDtcCurrent
                    .Where(x => x.VehicleId == vehicleId)
                    .ToListAsync();

                _db.EcuDtcCurrent.RemoveRange(olds);

                await _db.SaveChangesAsync();

                return Ok(new { ok = true });
            }
            foreach (var d in req.Dtcs ?? Enumerable.Empty<Esp32DtcItem>())
            {
                if (string.IsNullOrWhiteSpace(d.DtcCode))
                    continue;

                // 1️⃣ INSERT lịch sử theo session
                _db.EcuDtcResults.Add(new EcuDtcResult
                {
                    SessionId = sessionId,
                    DtcCode = d.DtcCode.Trim(),
                    StatusByte = d.StatusByte,
                    Protocol = req.Protocol
                });

                // 2️⃣ UPSERT trạng thái hiện tại
                var cur = await _db.EcuDtcCurrent
                    .FirstOrDefaultAsync(x =>
                        x.VehicleId == vehicleId &&
                        x.DtcCode == d.DtcCode);

                if (cur == null)
                {
                    _db.EcuDtcCurrent.Add(new EcuDtcCurrent
                    {
                        VehicleId = vehicleId,
                        DtcCode = d.DtcCode.Trim(),
                        StatusByte = d.StatusByte,
                        LastSeenAt = now
                    });
                }
                else
                {
                    cur.StatusByte = d.StatusByte;
                    cur.LastSeenAt = now;
                }
            }

            await _db.SaveChangesAsync();
            return Ok(new { ok = true });
        }
        catch
        {
            session.Status = SessionStatus.Failed;
            await _db.SaveChangesAsync();
            return StatusCode(500, "Failed to save DTCs");
        }
    }


    // =========================================================
    // ESP32 → GET PENDING SESSION
    // =========================================================
    [AllowAnonymous]
    [HttpPost("pending")]
    public async Task<IActionResult> GetPending(
        [FromBody] Esp32PendingReq req)
    {
        if (req == null ||
            string.IsNullOrWhiteSpace(req.DeviceId) ||
            string.IsNullOrWhiteSpace(req.DeviceKey))
            return BadRequest("Invalid device payload");

        // ===== VALIDATE DEVICE KEY =====
        var expectedKey = _cfg[$"DeviceKeys:{req.DeviceId}"];
        if (string.IsNullOrWhiteSpace(expectedKey) ||
            expectedKey != req.DeviceKey)
            return Unauthorized("Invalid device key");

        // ===== UPDATE ONLINE + FIRMWARE =====
        DeviceRuntimeState.IsConnected = true;
        DeviceRuntimeState.DeviceName = req.DeviceId;
        DeviceRuntimeState.Firmware = req.Firmware ?? "unknown";
        DeviceRuntimeState.LastSeenUtc = DateTime.UtcNow;

        // ===== AUTO FAIL SESSION TREO (>5 phút) =====
        var timeout = DateTime.UtcNow.AddMinutes(-5);

        var expired = await _db.EcuReadSessions
            .Where(x => x.Status == SessionStatus.Processing &&
                        x.CreatedAt < timeout)
            .ToListAsync();

        foreach (var s in expired)
            s.Status = SessionStatus.Failed;

        if (expired.Count > 0)
            await _db.SaveChangesAsync();

        // ===== LẤY SESSION PENDING CỦA DEVICE =====
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
            .FirstOrDefaultAsync(v => v.VehicleId == session.VehicleId);

        if (vehicle == null)
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
            brand = vehicle.Brand,
            model = vehicle.Model,
            year = vehicle.Year
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

        if (req.Protocol != "OBDII" && req.Protocol != "KWP")
            return BadRequest("Invalid protocol");

        var session = await _db.EcuReadSessions
            .FirstOrDefaultAsync(x => x.SessionId == sessionId);

        if (session == null)
            return NotFound("Session not found");

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

        if (session.Status != SessionStatus.Processing)
            return BadRequest("Invalid session state");

        session.Status = SessionStatus.Completed;
        session.CompletedAt = DateTime.UtcNow;

        await _db.SaveChangesAsync();
        return Ok(new { ok = true });
    }
}
