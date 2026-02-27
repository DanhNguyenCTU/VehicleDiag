namespace VehicleDiag.Api.Models
{
    public class DtcDictionary
    {
        public string DtcCode { get; set; } = "";
        public string? System { get; set; }
        public string? Scope { get; set; }
        public string? Description { get; set; }
        public string? Detail { get; set; }
        public string? GroupCode { get; set; }
    }
}
