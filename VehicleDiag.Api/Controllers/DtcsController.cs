using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using VehicleDiag.Api.Data;

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
    public async Task<IActionResult> Lookup([FromBody] List<string> codes)
    {
        if (codes == null || codes.Count == 0)
            return Ok(new List<DtcLookupDto>());

        var normalized = codes
            .Where(x => !string.IsNullOrWhiteSpace(x))
            .Select(x => x.Trim().ToUpper())
            .Distinct()
            .ToList();

        var result = await _db.DtcDictionary
            .Where(x => normalized.Contains(x.DtcCode))
            .Select(x => new DtcLookupDto
            {
                Code = x.DtcCode,
                Description = x.Description
            })
            .ToListAsync();

        return Ok(result);
    }
}
