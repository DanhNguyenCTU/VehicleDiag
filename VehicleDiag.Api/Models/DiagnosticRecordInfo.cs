namespace VehicleDiag.Api.Models;

public class DiagnosticRecordInfo
{
    public int Id { get; set; }
    public int RecordId { get; set; }
    public string? InfoKey { get; set; }
    public string? InfoLabel { get; set; }
    public string? InfoValue { get; set; }
}
