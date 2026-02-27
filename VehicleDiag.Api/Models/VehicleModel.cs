namespace VehicleDiag.Api.Models
{
    public class VehicleModel
    {
        public int ModelId { get; set; }

        public string Brand { get; set; } = "";

        public string Model { get; set; } = "";

        public int Year { get; set; }
    }
}
