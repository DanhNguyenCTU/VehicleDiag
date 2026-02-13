namespace VehicleDiag.Api.Models;

public class EcuDtcResult
{
    public int Id { get; set; }
    public int SessionId { get; set; }
    public string DtcCode { get; set; } = "";
    public byte StatusByte { get; set; }
    public string? Protocol { get; set; }
  
}
