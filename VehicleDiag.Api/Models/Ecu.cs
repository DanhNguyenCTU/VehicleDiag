namespace VehicleDiag.Api.Models;

public class Ecu
{
    public int EcuId { get; set; }
    public string EcuCode { get; set; } = "";
    public string DisplayName { get; set; } = "";
    public string Manufacturer { get; set; } = "";
    public string Brand { get; set; } = "";
    public bool IsActive { get; set; }
    public string DefaultProtocol { get; set; } = "";
}
