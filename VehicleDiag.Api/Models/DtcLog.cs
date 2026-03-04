namespace VehicleDiag.Api.Models
{
    public class DtcLog
    {
        public int Id { get; set; }
        public string DeviceId { get; set; } = string.Empty;
        public string DtcCode { get; set; } = string.Empty;
        public int StatusByte { get; set; }
        public string Source { get; set; } = "AUTO";
        public DateTime CreatedAt { get; set; }

        public Device Device { get; set; } = null!;
    }
}
