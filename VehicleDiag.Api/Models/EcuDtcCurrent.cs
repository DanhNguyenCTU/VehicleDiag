namespace VehicleDiag.Api.Models
{
    public class EcuDtcCurrent
    {
        public int VehicleId { get; set; }
        public string DtcCode { get; set; } = "";
        public int StatusByte { get; set; }
        public DateTime LastSeenAt { get; set; }
    }
}
