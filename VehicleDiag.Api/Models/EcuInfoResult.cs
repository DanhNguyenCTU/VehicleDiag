namespace VehicleDiag.Api.Models;

public class EcuInfoResult
{
    public int Id { get; set; }
    public int SessionId { get; set; }
    public string InfoKey { get; set; } = "";
    public string InfoLabel { get; set; } = "";
    public string InfoValue { get; set; } = "";
}
