namespace VehicleDiag.Api.Models
{
    public class Vehicle
    {
        public int VehicleId { get; set; }
        public string Brand { get; set; } = "";
        public string Model { get; set; } = "";
        public int Year { get; set; }
        public bool IsActive { get; set; }
    }
}
