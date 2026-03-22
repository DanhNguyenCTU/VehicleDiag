public class Telemetry
{
    public long Id { get; set; }
    public string DeviceId { get; set; } = default!;
    public double Lat { get; set; }
    public double Lng { get; set; }
    public DateTime? SentAtUtc { get; set; }
    public DateTime CreatedAt { get; set; }
}
