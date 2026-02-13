namespace VehicleDiag.Api.Services
{
    public static class DeviceRuntimeState
    {
        public static bool IsConnected { get; set; }
        public static string DeviceName { get; set; } = "";
        public static string Firmware { get; set; } = "unknown";
        public static DateTime LastSeenUtc { get; set; } = DateTime.MinValue;
        public static bool LiveDataEnabled;
        public static DateTime LiveDataCommandUtc;
    }
}
