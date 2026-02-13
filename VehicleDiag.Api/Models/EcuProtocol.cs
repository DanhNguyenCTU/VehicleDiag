namespace VehicleDiag.Api.Models
{
    public class EcuProtocol
    {
        public int Id { get; set; }
        public int EcuId { get; set; }
        public string ProtocolCode { get; set; } = "";
        public bool IsDefault { get; set; }
    }
}
