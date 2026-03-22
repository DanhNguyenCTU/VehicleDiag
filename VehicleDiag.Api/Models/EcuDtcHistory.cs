namespace VehicleDiag.Api.Models
{
    public class EcuDtcHistory
    {
        public long Id { get; set; }

        public int VehicleId { get; set; }

        public string DtcCode { get; set; } = null!;

        public int StatusByte { get; set; }

        public DateTime FirstSeenAt { get; set; }

        public DateTime LastSeenAt { get; set; }

        public DateTime? SentAtUtc { get; set; }

        public DateTime? ClearedAt { get; set; }

        public Vehicle Vehicle { get; set; } = null!;
    }
}
