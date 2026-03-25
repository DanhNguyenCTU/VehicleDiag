using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using System.Security.Claims;
using VehicleDiag.Api.Constants;
using VehicleDiag.Api.Data;
using VehicleDiag.Api.Dtos;
using VehicleDiag.Api.Dtos.Requests;
using VehicleDiag.Api.Services;

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

        var recordType = NormalizeRecordType(req.ActionType);
        if (!DiagnosticRecordType.IsValid(recordType))
            return BadRequest("Invalid RecordType");

        var protocol = string.IsNullOrWhiteSpace(req.Protocol)
            ? "OBDII"
            : req.Protocol.Trim().ToUpperInvariant();

        if (protocol != "OBDII" && protocol != "KWP")
            return BadRequest("Invalid protocol");

        var record = new Models.DiagnosticRecord
        {
            SavedAt = DateTime.UtcNow,
            RecordType = recordType,
            DeviceId = vehicle.DeviceId,
            VehicleId = vehicle.VehicleId,
            Protocol = protocol,
            CreatedByUserId = int.Parse(userIdStr),
            Status = SessionStatus.Pending
        };

        _db.DiagnosticRecords.Add(record);
        await _db.SaveChangesAsync();

        await _mqtt.PublishCommand(
            record.DeviceId!,
            new
            {
                sessionId = record.RecordId,
                actionType = ToCommandAction(record.RecordType),
                protocol = record.Protocol,
                brand = vehicle.VehicleModel?.Brand,
                model = vehicle.VehicleModel?.Model,
                year = vehicle.VehicleModel?.Year
            });

        record.Status = SessionStatus.Processing;
        await _db.SaveChangesAsync();

        return Ok(new { SessionId = record.RecordId, RecordId = record.RecordId });
    }

    [HttpGet("{sessionId:int}/dtcs")]
    public async Task<IActionResult> GetSessionDtcs(int sessionId)
    {
        var record = await _db.DiagnosticRecords
            .FirstOrDefaultAsync(x => x.RecordId == sessionId);

        if (record == null)
            return NotFound("Session not found");

        if (!string.Equals(record.Status, SessionStatus.Completed, StringComparison.OrdinalIgnoreCase))
            return BadRequest("Session not completed");

        var dtcs = await (
            from r in _db.DiagnosticRecordDtcs
            join d in _db.DtcDictionary on r.DtcCode equals d.DtcCode into dict
            from d in dict.DefaultIfEmpty()
            where r.RecordId == sessionId
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

    [HttpGet("{sessionId:int}/info")]
    public async Task<IActionResult> GetSessionInfo(int sessionId)
    {
        var record = await _db.DiagnosticRecords
            .FirstOrDefaultAsync(x => x.RecordId == sessionId);

        if (record == null)
            return NotFound("Session not found");

        if (!string.Equals(record.Status, SessionStatus.Completed, StringComparison.OrdinalIgnoreCase))
            return BadRequest("Session not completed");

        var info = await _db.DiagnosticRecordInfos
            .Where(x => x.RecordId == sessionId)
            .OrderBy(x => x.Id)
            .Select(x => new
            {
                Field = x.InfoKey,
                Value = x.InfoValue
            })
            .ToListAsync();

        return Ok(info);
    }

    public record SessionStatusDto(
        int SessionId,
        string Status,
        DateTime CreatedAt,
        DateTime? CompletedAt,
        string? RecordType
    );

    [HttpGet("{sessionId:int}/status")]
    public async Task<IActionResult> GetSessionStatus(int sessionId)
    {
        var session = await _db.DiagnosticRecords
            .Where(x => x.RecordId == sessionId)
            .Select(x => new SessionStatusDto(
                x.RecordId,
                x.Status ?? SessionStatus.Pending,
                x.SavedAt,
                x.CapturedAt,
                x.RecordType
            ))
            .FirstOrDefaultAsync();

        if (session == null)
            return NotFound("Session not found");

        return Ok(session);
    }

    [HttpGet("{sessionId:int}/freeze-frame")]
    public async Task<IActionResult> GetFreezeFrame(int sessionId)
    {
        var data = await _db.DiagnosticRecordFreezeFrames
            .Where(x => x.RecordId == sessionId)
            .Select(x => new FreezeFrameDto
            {
                Dtc = x.Dtc ?? string.Empty,
                Rpm = x.Rpm ?? 0,
                Speed = x.Speed ?? 0,
                Coolant = x.Coolant ?? 0
            })
            .FirstOrDefaultAsync();

        if (data == null)
            return NotFound();

        return Ok(data);
    }

    private static string NormalizeRecordType(string? value)
    {
        if (string.IsNullOrWhiteSpace(value))
            return string.Empty;

        var normalized = value.Trim()
            .Replace("-", "_")
            .Replace(" ", "_")
            .ToUpperInvariant();

        return normalized switch
        {
            "READINFO" => DiagnosticRecordType.READ_INFO,
            "READ_INFO" => DiagnosticRecordType.READ_INFO,
            "READDTC" => DiagnosticRecordType.READ_DTC,
            "READ_DTC" => DiagnosticRecordType.READ_DTC,
            _ => string.Empty
        };
    }

    private static string ToCommandAction(string? recordType)
    {
        return DiagnosticRecordType.Normalize(recordType) switch
        {
            DiagnosticRecordType.READ_INFO => SessionActionType.READ_INFO,
            DiagnosticRecordType.READ_DTC => SessionActionType.READ_DTC,
            _ => string.Empty
        };
    }
}
