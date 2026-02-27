public class Device
{
    public string DeviceId { get; set; } = default!;
    public DateTime CreatedAt { get; set; }
    public bool IsActive { get; set; }
    public DateTime? LastSeenAt { get; set; }
}