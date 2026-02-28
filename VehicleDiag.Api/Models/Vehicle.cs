namespace VehicleDiag.Api.Models;

public class Vehicle
{
    public int VehicleId { get; set; }

    public int ModelId { get; set; }

    public VehicleModel VehicleModel { get; set; } = null!;

    public bool IsActive { get; set; }

    public string? DeviceId { get; set; }

    public string? PlateNumber { get; set; }
}
