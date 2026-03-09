namespace VehicleDiag.Api.Dtos
{
    public class FreezeFrameDto
    {
        public string Dtc { get; set; } = "";
        public int Rpm { get; set; }
        public int Speed { get; set; }
        public int Coolant { get; set; }
    }
}
