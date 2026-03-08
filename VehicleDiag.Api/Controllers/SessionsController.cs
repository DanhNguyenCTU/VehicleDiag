using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using System.Security.Claims;
using VehicleDiag.Api.Data;
using VehicleDiag.Api.Services;
using VehicleDiag.Api.Dtos.Requests;
using VehicleDiag.Api.Models;
using VehicleDiag.Api.Constants;

namespace VehicleDiag.Api.Controllers;

[Authorize(Roles = "Technician,Admin")]
[ApiController]
[Route("api/sessions")]
public class SessionsController : ControllerBase
{
    private readonly AppDbContext _db;
    private readonly IMqttPublishService _mqtt;

    public SessionsController(AppDbContext db, IMqttPublishService mqtt)
    {
        _db = db;
        _mqtt = mqtt;
    }

    // =========================================================
    // UI → CREATE SESSION
    // =========================================================

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
            return BadRequest("Vehicle has no device");

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

        // ======================================================
        // Publish command to ESP32 via MQTT
        // ======================================================

        await _mqtt.PublishCommand(
            session.DeviceId,
            new
            {
                sessionId = session.SessionId,
                actionType = session.ActionType,
                protocol = session.Protocol,
                brand = vehicle.VehicleModel?.Brand,
                model = vehicle.VehicleModel?.Model,
                year = vehicle.VehicleModel?.Year
            });

        session.Status = SessionStatus.Processing;

        await _db.SaveChangesAsync();

        return Ok(new { SessionId = session.SessionId });
    }

    // =========================================================
    // UI → GET SESSION DTCs
    // =========================================================
    [HttpGet("{sessionId:int}/dtcs")]
    public async Task<IActionResult> GetSessionDtcs(int sessionId)
    {
        var session = await _db.EcuReadSessions
            .FirstOrDefaultAsync(x => x.SessionId == sessionId);

        if (session == null)
            return NotFound("Session not found");

        if (!string.Equals(session.Status,
            SessionStatus.Completed,
            StringComparison.OrdinalIgnoreCase))
            return BadRequest("Session not completed");

        var dtcs = await (
            from r in _db.EcuDtcResults
            join d in _db.DtcDictionary
                on r.DtcCode equals d.DtcCode into dict
            from d in dict.DefaultIfEmpty()
            where r.SessionId == sessionId
            orderby r.Id
            select new
            {
                Code = r.DtcCode,
                Description = d.Description ?? "Unknown DTC",
                StatusByte = r.StatusByte,
                Protocol = r.Protocol
            }
        ).ToListAsync();

        return Ok(dtcs);
    }

    // =========================================================
    // UI → GET SESSION INFO
    // =========================================================

    [HttpGet("{sessionId:int}/info")]
    public async Task<IActionResult> GetSessionInfo(int sessionId)
    {
        var session = await _db.EcuReadSessions
            .FirstOrDefaultAsync(x => x.SessionId == sessionId);

        if (session == null)
            return NotFound("Session not found");

        if (!string.Equals(session.Status,
            SessionStatus.Completed,
            StringComparison.OrdinalIgnoreCase))
            return BadRequest("Session not completed");

        var info = await _db.EcuInfoResults
            .Where(x => x.SessionId == sessionId)
            .OrderBy(x => x.Id)
            .Select(x => new
            {
                Field = x.InfoKey,
                Value = x.InfoValue
            })
            .ToListAsync();

        return Ok(info);
    }

    // =========================================================
    // UI → SESSION STATUS
    // =========================================================

    public record SessionStatusDto(
        int SessionId,
        string Status,
        DateTime CreatedAt,
        DateTime? CompletedAt
    );

    [HttpGet("{sessionId:int}/status")]
    public async Task<IActionResult> GetSessionStatus(int sessionId)
    {
        var session = await _db.EcuReadSessions
            .Where(x => x.SessionId == sessionId)
            .Select(x => new SessionStatusDto(
                x.SessionId,
                x.Status,
                x.CreatedAt,
                x.CompletedAt
            ))
            .FirstOrDefaultAsync();

        if (session == null)
            return NotFound("Session not found");

        return Ok(session);
    }
}