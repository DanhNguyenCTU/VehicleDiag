namespace VehicleDiag.Api.Models;

public class EcuReadSession
{
    public int SessionId { get; set; }
    public DateTime CreatedAt { get; set; }

    public string ActionType { get; set; } = "";   // ReadDTC | ReadInfo
    public string DeviceId { get; set; } = "";

    public int VehicleId { get; set; }
    public int CreatedByUserId { get; set; }
    // public int EcuId { get; set; }                  
    // public string EcuName { get; set; } = "";      

    public string Protocol { get; set; } = "";  
 

    public string Status { get; set; } = "PENDING";
    public DateTime? CompletedAt { get; set; }
}

