namespace VehicleDiag.Api.Models;

public class DiagnosticRecordFreezeFrame
{
    public int Id { get; set; }
    public int RecordId { get; set; }
    public string? Dtc { get; set; }
    public int? Rpm { get; set; }
    public int? Speed { get; set; }
    public int? Coolant { get; set; }
    public DateTime CreatedAt { get; set; } = DateTime.UtcNow;
}
