namespace VehicleDiag.Api.Models;

public class DiagnosticRecord
{
    public int RecordId { get; set; }
    public DateTime SavedAt { get; set; }
    public string? RecordType { get; set; }
    public string? DeviceId { get; set; }
    public int? CreatedByUserId { get; set; }
    public string? Status { get; set; }
    public string? Protocol { get; set; }
    public int VehicleId { get; set; }
    public DateTime? CapturedAt { get; set; }
}

