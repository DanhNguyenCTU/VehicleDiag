using VehicleDiag.Api.Constants;

namespace VehicleDiag.Api.Dtos.Requests;

public class CreateSessionReq
{
    public int VehicleId { get; set; }
    public string ActionType { get; set; } = "";
    public string? Protocol { get; set; }
}

