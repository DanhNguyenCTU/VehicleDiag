namespace VehicleDiag.Api.Models;

public class DiagnosticRecordDtc
{
    public int Id { get; set; }
    public int RecordId { get; set; }
    public string? DtcCode { get; set; }
    public int? StatusByte { get; set; }
    public string? Protocol { get; set; }
}
