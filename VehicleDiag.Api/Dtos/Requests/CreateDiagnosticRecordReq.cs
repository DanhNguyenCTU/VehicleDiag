using VehicleDiag.Api.Dtos;

namespace VehicleDiag.Api.Dtos.Requests;

public class CreateDiagnosticRecordReq
{
    public int VehicleId { get; set; }
    public string RecordType { get; set; } = "";
    public string? Protocol { get; set; }
    public DateTime? CapturedAt { get; set; }
    public List<CreateDiagnosticRecordDtcReq>? Dtcs { get; set; }
    public List<CreateDiagnosticRecordInfoReq>? Info { get; set; }
    public FreezeFrameDto? FreezeFrame { get; set; }
}

public class CreateDiagnosticRecordDtcReq
{
    public string DtcCode { get; set; } = "";
    public int StatusByte { get; set; }
    public string? Protocol { get; set; }
}

public class CreateDiagnosticRecordInfoReq
{
    public string InfoKey { get; set; } = "";
    public string? InfoLabel { get; set; }
    public string? InfoValue { get; set; }
}
