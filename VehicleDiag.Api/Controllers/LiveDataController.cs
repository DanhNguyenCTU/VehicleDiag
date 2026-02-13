using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using VehicleDiag.Api.Services;

namespace VehicleDiag.Api.Controllers;

[ApiController]
[Route("api/livedata")]
public class LiveDataController : ControllerBase
{
    [HttpPost("start")]
    public IActionResult Start()
    {
        if (!DeviceRuntimeState.IsConnected)
            return BadRequest("Device not connected");

        DeviceRuntimeState.LiveDataEnabled = true;
        DeviceRuntimeState.LiveDataCommandUtc = DateTime.UtcNow;

        return Ok(new { ok = true });
    }

    [HttpPost("stop")]
    public IActionResult Stop()
    {
        DeviceRuntimeState.LiveDataEnabled = false;
        DeviceRuntimeState.LiveDataCommandUtc = DateTime.UtcNow;

        return Ok(new { ok = true });
    }
    [HttpPost("frame")]
    [AllowAnonymous]
    public IActionResult Push([FromBody] LiveDataFrame frame)
    {
        if (!DeviceRuntimeState.LiveDataEnabled)
            return Ok(); // ignore nếu UI đã tắt
        if (frame == null)
            return BadRequest("Invalid frame");

        LiveDataCache.Update(frame);
 
        return Ok(new { ok = true });
    }
    [HttpGet]
    public IActionResult Get()
    {

        if (!DeviceRuntimeState.LiveDataEnabled)
            return Ok(new { enabled = false });

      
        if (LiveDataCache.Current == null)
            return Ok(new
            {
                enabled = true,
                status = "waiting"
            });

        // 3️⃣ Có dữ liệu
        return Ok(new
        {
            enabled = true,
            status = "ok",
            timestamp = LiveDataCache.LastUpdatedUtc,
            data = LiveDataCache.Current
        });
    }
}