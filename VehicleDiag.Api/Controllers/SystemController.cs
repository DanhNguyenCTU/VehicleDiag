using Microsoft.AspNetCore.Mvc;
using VehicleDiag.Api.Services;

namespace VehicleDiag.Api.Controllers;

[ApiController]
[Route("api/system")]
public class SystemController : ControllerBase
{
    [HttpGet("health")]
    public IActionResult Health()
    {
        return Ok(new { status = "ok" });
    }

    [HttpGet("status")]
    public IActionResult Status()
    {
        bool connected =
            (DateTime.UtcNow - DeviceRuntimeState.LastSeenUtc).TotalSeconds < 15;

        return Ok(new
        {
            deviceName = DeviceRuntimeState.DeviceName,
            status = connected ? "connected" : "disconnected",
            firmware = DeviceRuntimeState.Firmware
        });
    }
}
