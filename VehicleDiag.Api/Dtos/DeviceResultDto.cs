namespace VehicleDiag.Api.Dtos;

public class DeviceResultDto
{
    public int SessionId { get; set; }
    public string DeviceId { get; set; } = "";


    public VehicleDto Vehicle { get; set; } = null!;

    public List<InfoItemDto> Info { get; set; } = new();
    public List<DtcItemDto> Dtcs { get; set; } = new();
}

public class InfoItemDto
{
    public string Key { get; set; } = "";
    public string? Label { get; set; }
    public string Value { get; set; } = "";
}

public class DtcItemDto
{
    public string Code { get; set; } = "";
    public byte StatusByte { get; set; }      
    public string? Description { get; set; }
    public string? Protocol { get; set; }
}
