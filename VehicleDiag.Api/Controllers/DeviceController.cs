using Microsoft.AspNetCore.Mvc;
using VehicleDiag.Api.Services;

namespace VehicleDiag.Api.Controllers;

[ApiController]
[Route("api/device")]
public class DeviceController : ControllerBase
{
    private readonly IConfiguration _cfg;

    public DeviceController(IConfiguration cfg)
    {
        _cfg = cfg;
    }

    // =========================
    // AUTH HELPER (API KEY)
    // =========================
    private bool ValidateDeviceKey(
        string deviceId,
        string? deviceKey)
    {
        if (string.IsNullOrWhiteSpace(deviceKey))
            return false;

        var expectedKey = _cfg[$"DeviceKeys:{deviceId}"];
        return expectedKey != null && expectedKey == deviceKey;
    }

    // =========================
    // ESP32 → CONNECT
    // =========================
    [HttpPost("connect")]
    public IActionResult Connect(
        [FromHeader(Name = "X-Device-Key")] string? deviceKey)
    {
        const string deviceId = "DS32";

        if (!ValidateDeviceKey(deviceId, deviceKey))
            return Unauthorized("Invalid device key");

        DeviceRuntimeState.IsConnected = true;
        DeviceRuntimeState.DeviceName = deviceId;
        DeviceRuntimeState.Firmware = "v1.0.3";
        DeviceRuntimeState.LastSeenUtc = DateTime.UtcNow;

        return Ok(new
        {
            success = true,
            deviceId = deviceId
        });
    }

    // =========================
    // ESP32 → DISCONNECT
    // =========================
    [HttpPost("disconnect")]
    public IActionResult Disconnect(
        [FromHeader(Name = "X-Device-Key")] string? deviceKey)
    {
        const string deviceId = "DS32";

        if (!ValidateDeviceKey(deviceId, deviceKey))
            return Unauthorized("Invalid device key");

        DeviceRuntimeState.IsConnected = false;
        DeviceRuntimeState.DeviceName = "";
        DeviceRuntimeState.Firmware = "unknown";
        DeviceRuntimeState.LastSeenUtc = DateTime.MinValue;

        return Ok(new { success = true });
    }

    // =========================
    // ESP32 → HEARTBEAT
    // =========================
    public record DeviceHeartbeatReq(
        string DeviceId,
        string Firmware,
        string? DeviceKey
    );


    [HttpPost("heartbeat")]
    public IActionResult Heartbeat(
        [FromBody] DeviceHeartbeatReq req,
        [FromHeader(Name = "X-Device-Key")] string? headerDeviceKey)
    {
        if (req == null || string.IsNullOrWhiteSpace(req.DeviceId))
            return BadRequest("Invalid heartbeat payload");

        string? deviceKey = !string.IsNullOrWhiteSpace(req.DeviceKey)
            ? req.DeviceKey
            : headerDeviceKey;

        if (string.IsNullOrWhiteSpace(deviceKey))
            return Unauthorized("Missing device key");

        var expectedKey = _cfg[$"DeviceKeys:{req.DeviceId}"];
        if (string.IsNullOrWhiteSpace(expectedKey) || expectedKey != deviceKey)
            return Unauthorized("Invalid device key");

        DeviceRuntimeState.IsConnected = true;
        DeviceRuntimeState.DeviceName = req.DeviceId;
        DeviceRuntimeState.Firmware = req.Firmware;
        DeviceRuntimeState.LastSeenUtc = DateTime.UtcNow;

        var json = System.Text.Json.JsonSerializer.Serialize(new { ok = true });

        return new ContentResult
        {
            Content = json,
            ContentType = "application/json",
            StatusCode = 200
        };
    }


}
