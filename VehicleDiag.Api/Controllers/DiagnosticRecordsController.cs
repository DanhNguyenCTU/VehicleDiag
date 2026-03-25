using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using System.Security.Claims;
using VehicleDiag.Api.Constants;
using VehicleDiag.Api.Data;
using VehicleDiag.Api.Dtos;
using VehicleDiag.Api.Dtos.Requests;
using VehicleDiag.Api.Models;

namespace VehicleDiag.Api.Controllers;

[Authorize(Roles = "Technician,Admin")]
[ApiController]
[Route("api/diagnostic-records")]
public class DiagnosticRecordsController : ControllerBase
{
    private readonly AppDbContext _db;

    public DiagnosticRecordsController(AppDbContext db)
    {
        _db = db;
    }

    [HttpPost]
    public async Task<IActionResult> Create([FromBody] CreateDiagnosticRecordReq req)
    {
        var userIdStr = User.FindFirst(ClaimTypes.NameIdentifier)?.Value;
        if (userIdStr == null)
            return Unauthorized();

        var vehicle = await _db.Vehicles
            .FirstOrDefaultAsync(v => v.VehicleId == req.VehicleId && v.IsActive);

        if (vehicle == null)
            return BadRequest("Invalid vehicle");

        var recordType = DiagnosticRecordType.Normalize(req.RecordType);
        if (!DiagnosticRecordType.IsValid(recordType))
            return BadRequest("RecordType must be READ_DTC or READ_INFO");

        if (recordType == DiagnosticRecordType.READ_DTC && req.Info is { Count: > 0 })
            return BadRequest("READ_DTC record cannot include info items");

        if (recordType == DiagnosticRecordType.READ_INFO &&
            ((req.Dtcs is { Count: > 0 }) || req.FreezeFrame != null))
        {
            return BadRequest("READ_INFO record cannot include DTC or freeze-frame data");
        }

        var protocol = string.IsNullOrWhiteSpace(req.Protocol)
            ? "OBDII"
            : req.Protocol.Trim().ToUpperInvariant();

        if (protocol != "OBDII" && protocol != "KWP")
            return BadRequest("Invalid protocol");

        var savedAt = DateTime.UtcNow;
        var capturedAt = req.CapturedAt?.ToUniversalTime() ?? savedAt;

        await using var tx = await _db.Database.BeginTransactionAsync();

        var record = new DiagnosticRecord
        {
            SavedAt = savedAt,
            CapturedAt = capturedAt,
            RecordType = recordType,
            DeviceId = vehicle.DeviceId,
            EcuId = req.EcuId,
            EcuName = string.IsNullOrWhiteSpace(req.EcuName) ? null : req.EcuName.Trim(),
            CreatedByUserId = int.Parse(userIdStr),
            Status = SessionStatus.Completed,
            Protocol = protocol,
            VehicleId = vehicle.VehicleId
        };

        _db.DiagnosticRecords.Add(record);
        await _db.SaveChangesAsync();

        if (recordType == DiagnosticRecordType.READ_DTC && req.Dtcs != null)
        {
            foreach (var dtc in req.Dtcs)
            {
                if (string.IsNullOrWhiteSpace(dtc.DtcCode))
                    continue;

                _db.DiagnosticRecordDtcs.Add(new DiagnosticRecordDtc
                {
                    RecordId = record.RecordId,
                    DtcCode = dtc.DtcCode.Trim().ToUpperInvariant(),
                    StatusByte = dtc.StatusByte,
                    Protocol = string.IsNullOrWhiteSpace(dtc.Protocol)
                        ? protocol
                        : dtc.Protocol.Trim().ToUpperInvariant()
                });
            }

            if (req.FreezeFrame != null)
            {
                _db.DiagnosticRecordFreezeFrames.Add(new DiagnosticRecordFreezeFrame
                {
                    RecordId = record.RecordId,
                    Dtc = req.FreezeFrame.Dtc,
                    Rpm = req.FreezeFrame.Rpm,
                    Speed = req.FreezeFrame.Speed,
                    Coolant = req.FreezeFrame.Coolant,
                    CreatedAt = savedAt
                });
            }
        }

        if (recordType == DiagnosticRecordType.READ_INFO && req.Info != null)
        {
            foreach (var item in req.Info)
            {
                if (string.IsNullOrWhiteSpace(item.InfoKey) || string.IsNullOrWhiteSpace(item.InfoValue))
                    continue;

                _db.DiagnosticRecordInfos.Add(new DiagnosticRecordInfo
                {
                    RecordId = record.RecordId,
                    InfoKey = item.InfoKey.Trim().ToUpperInvariant(),
                    InfoLabel = string.IsNullOrWhiteSpace(item.InfoLabel) ? item.InfoKey.Trim() : item.InfoLabel.Trim(),
                    InfoValue = item.InfoValue.Trim()
                });
            }
        }

        await _db.SaveChangesAsync();
        await tx.CommitAsync();

        return Ok(new
        {
            RecordId = record.RecordId,
            record.RecordType,
            record.SavedAt,
            record.CapturedAt,
            record.Status
        });
    }

    [HttpGet("{recordId:int}")]
    public async Task<IActionResult> GetById(int recordId)
    {
        var record = await _db.DiagnosticRecords
            .Where(x => x.RecordId == recordId)
            .Select(x => new
            {
                x.RecordId,
                x.RecordType,
                x.SavedAt,
                x.CapturedAt,
                x.Status,
                x.Protocol,
                x.EcuId,
                x.EcuName,
                x.DeviceId,
                x.VehicleId,
                x.CreatedByUserId
            })
            .FirstOrDefaultAsync();

        if (record == null)
            return NotFound();

        return Ok(record);
    }

    [HttpGet]
    public async Task<IActionResult> List([FromQuery] int vehicleId)
    {
        var items = await _db.DiagnosticRecords
            .Where(x => x.VehicleId == vehicleId)
            .OrderByDescending(x => x.SavedAt)
            .Select(x => new
            {
                x.RecordId,
                x.RecordType,
                x.SavedAt,
                x.CapturedAt,
                x.Status,
                x.Protocol,
                x.EcuName
            })
            .ToListAsync();

        return Ok(items);
    }

    [HttpGet("{recordId:int}/dtcs")]
    public async Task<IActionResult> GetDtcs(int recordId)
    {
        var record = await _db.DiagnosticRecords.FirstOrDefaultAsync(x => x.RecordId == recordId);
        if (record == null)
            return NotFound();

        var dtcs = await (
            from r in _db.DiagnosticRecordDtcs
            join d in _db.DtcDictionary on r.DtcCode equals d.DtcCode into dict
            from d in dict.DefaultIfEmpty()
            where r.RecordId == recordId
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

    [HttpGet("{recordId:int}/info")]
    public async Task<IActionResult> GetInfo(int recordId)
    {
        var record = await _db.DiagnosticRecords.FirstOrDefaultAsync(x => x.RecordId == recordId);
        if (record == null)
            return NotFound();

        var info = await _db.DiagnosticRecordInfos
            .Where(x => x.RecordId == recordId)
            .OrderBy(x => x.Id)
            .Select(x => new
            {
                Field = x.InfoKey,
                Label = x.InfoLabel,
                Value = x.InfoValue
            })
            .ToListAsync();

        return Ok(info);
    }

    [HttpGet("{recordId:int}/freeze-frame")]
    public async Task<IActionResult> GetFreezeFrame(int recordId)
    {
        var data = await _db.DiagnosticRecordFreezeFrames
            .Where(x => x.RecordId == recordId)
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
}
