namespace VehicleDiag.Api.Services;

public static class LiveDataCache
{
    // frame livedata mới nhất
    public static volatile LiveDataFrame? Current;
    public static DateTime LastUpdatedUtc;

    public static void Update(LiveDataFrame frame)
    {
        Current = frame;
        LastUpdatedUtc = DateTime.UtcNow;
    }
}

public class LiveDataFrame
{
    public float Rpm { get; set; }
    public float Speed { get; set; }
    public float Coolant { get; set; }
    public float Load { get; set; }
    public float Iat { get; set; }
    public float Throttle { get; set; }
    public float Map { get; set; }
}