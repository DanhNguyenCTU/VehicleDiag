using System.IdentityModel.Tokens.Jwt;
using System.Security.Claims;
using System.Text;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using Microsoft.IdentityModel.Tokens;
using VehicleDiag.Api.Data;

namespace VehicleDiag.Api.Controllers;

[ApiController]
[Route("api/auth")]
public class AuthController : ControllerBase
{
    private readonly AppDbContext _db;
    private readonly IConfiguration _cfg;

    public AuthController(AppDbContext db, IConfiguration cfg)
    {
        _db = db;
        _cfg = cfg;
    }

    public record LoginReq(string Username, string Password);

    [HttpPost("login")]
    public async Task<IActionResult> Login([FromBody] LoginReq req)
    {
        // BẮT BUỘC HTTPS
        if (!Request.IsHttps)
        {
            return BadRequest("Login must be performed over HTTPS.");
        }

        // Validate user
        var user = await _db.AppUsers
            .FirstOrDefaultAsync(x => x.Username == req.Username);

        if (user == null)
            return Unauthorized();

        // PasswordHash đang là plain text trong DB
        if (user.PasswordHash != req.Password)
            return Unauthorized();

        // JWT config
        var key = _cfg["Jwt:Key"]!;
        var issuer = _cfg["Jwt:Issuer"]!;
        var audience = _cfg["Jwt:Audience"]!;

        var claims = new List<Claim>
        {
            new Claim(ClaimTypes.NameIdentifier, user.Id.ToString()),
            new Claim(ClaimTypes.Name, user.Username),
            new Claim(ClaimTypes.Role, user.Role)
        };

        var token = new JwtSecurityToken(
            issuer: issuer,
            audience: audience,
            claims: claims,
            expires: DateTime.UtcNow.AddHours(12),
            signingCredentials: new SigningCredentials(
                new SymmetricSecurityKey(Encoding.UTF8.GetBytes(key)),
                SecurityAlgorithms.HmacSha256
            )
        );

        var jwt = new JwtSecurityTokenHandler().WriteToken(token);

        // Response
        return Ok(new
        {
            token = jwt,
            user = new
            {
                user.Id,
                user.Username,
                user.DisplayName,
                user.Role
            }
        });
    }
}
