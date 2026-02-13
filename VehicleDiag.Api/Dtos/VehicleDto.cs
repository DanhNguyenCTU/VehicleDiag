namespace VehicleDiag.Api.Dtos;

public class VehicleDto
{
    public int VehicleId { get; set; }
    public string Brand { get; set; } = "";
    public string Model { get; set; } = "";
    public int Year { get; set; }
}
