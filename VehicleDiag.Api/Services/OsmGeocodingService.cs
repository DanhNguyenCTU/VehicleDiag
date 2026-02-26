namespace VehicleDiag.Api.Services
{
    using System.Text.Json;
    public class OsmGeocodingService
    {
        private readonly HttpClient _httpClient;

        public OsmGeocodingService(HttpClient httpClient)
        {
            _httpClient = httpClient;
            _httpClient.DefaultRequestHeaders.UserAgent.ParseAdd("VehicleTrackingApp/1.0");
        }

        public async Task<string> GetAddressAsync(double lat, double lng)
        {
            var url =
                $"https://nominatim.openstreetmap.org/reverse?format=json&lat={lat}&lon={lng}";

            var response = await _httpClient.GetStringAsync(url);

            using var doc = JsonDocument.Parse(response);
            var root = doc.RootElement;

            if (root.TryGetProperty("display_name", out var display))
            {
                return display.GetString() ?? "Unknown location";
            }

            return "Unknown location";
        }
    }
}
