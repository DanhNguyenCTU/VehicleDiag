using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using VehicleDiag.Api.Data;
using VehicleDiag.Api.Dtos;

namespace VehicleDiag.Api.Controllers;

[Authorize(Roles = "Technician,Admin")]
[ApiController]
[Route("api/dtcs")]
public class DtcsController : ControllerBase
{
    private readonly AppDbContext _db;

    public DtcsController(AppDbContext db)
    {
        _db = db;
    }

    public class DtcLookupDto
    {
        public string Code { get; set; } = "";
        public string Description { get; set; } = "";
    }

    [HttpPost("lookup")]
    public async Task<IActionResult> Lookup([FromBody] DtcLookupRequestDto req)
    {
        if (req == null || req.Codes == null || req.Codes.Count == 0)
            return Ok(new List<DtcLookupDto>());

        var normalizedCodes = req.Codes
            .Where(x => !string.IsNullOrWhiteSpace(x))
            .Select(x => x.Trim().ToUpper())
            .Distinct()
            .ToList();

        if (normalizedCodes.Count == 0)
            return Ok(new List<DtcLookupDto>());

        string brand = (req.Brand ?? "").Trim();
        string? groupCode = null;

        if (!string.IsNullOrWhiteSpace(brand))
        {
            groupCode = await _db.ManufacturerBrands
                .Where(x => x.Brand == brand)
                .Select(x => x.GroupCode)
                .FirstOrDefaultAsync();
        }

        var dictRows = await _db.DtcDictionary
            .Where(d =>
                normalizedCodes.Contains(d.DtcCode) &&
                (
                    d.Scope == "Generic" ||
                    (
                        groupCode != null &&
                        d.Scope == "Manufacturer" &&
                        d.GroupCode == groupCode
                    )
                ))
            .ToListAsync();

        var result = normalizedCodes
            .Select(code =>
            {
                var bestMatch = dictRows
                    .Where(d => d.DtcCode == code)
                    .OrderBy(d => d.Scope == "Manufacturer" ? 0 : 1)
                    .FirstOrDefault();

                if (bestMatch == null)
                    return null;

                return new DtcLookupDto
                {
                    Code = code,
                    Description = string.IsNullOrWhiteSpace(bestMatch.Description)
                        ? "Unknown DTC"
                        : bestMatch.Description
                };
            })
            .Where(x => x != null)
            .ToList();

        return Ok(result);
    }
}
