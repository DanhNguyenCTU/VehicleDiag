namespace VehicleDiag.Api.Dtos
{
    public class DtcLookupRequestDto
    {
        public List<string> Codes { get; set; } = new();
        public string Brand { get; set; } = "";
    }
}
