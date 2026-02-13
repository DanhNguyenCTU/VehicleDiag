using System.Text;
using System.Text.Json;
using MQTTnet;
using MQTTnet.Client;
using MQTTnet.Client.Options;

namespace VehicleDiag.Api.Services;

public class MqttBackgroundService : BackgroundService
{
    private readonly IConfiguration _config;
    private IMqttClient? _mqttClient;
    private IMqttClientOptions? _options;

    public MqttBackgroundService(IConfiguration config)
    {
        _config = config;
    }

    protected override async Task ExecuteAsync(CancellationToken stoppingToken)
    {
        var factory = new MqttFactory();
        _mqttClient = factory.CreateMqttClient();

        // ===== Message Handler =====
        _mqttClient.UseApplicationMessageReceivedHandler(e =>
        {
            var payload = Encoding.UTF8.GetString(e.ApplicationMessage.Payload);

            Console.WriteLine($"📥 MQTT Topic: {e.ApplicationMessage.Topic}");
            Console.WriteLine($"Payload: {payload}");

            try
            {
                var data = JsonSerializer.Deserialize<DeviceHeartbeatReq>(
                    payload,
                    new JsonSerializerOptions
                    {
                        PropertyNameCaseInsensitive = true
                    });

                if (data == null || string.IsNullOrWhiteSpace(data.DeviceId))
                {
                    Console.WriteLine("❌ Invalid payload");
                    return;
                }

                var expectedKey = _config[$"DeviceKeys:{data.DeviceId}"];

                if (string.IsNullOrWhiteSpace(data.DeviceKey) ||
                    expectedKey == null ||
                    expectedKey != data.DeviceKey)
                {
                    Console.WriteLine("❌ Invalid device key");
                    return;
                }

                DeviceRuntimeState.IsConnected = true;
                DeviceRuntimeState.DeviceName = data.DeviceId;
                DeviceRuntimeState.Firmware = data.Firmware;
                DeviceRuntimeState.LastSeenUtc = DateTime.UtcNow;

                Console.WriteLine("✅ Heartbeat processed (.NET)");
            }
            catch (Exception ex)
            {
                Console.WriteLine("❌ JSON Error: " + ex.Message);
            }
        });

        // ===== Reconnect Handler =====
        _mqttClient.UseDisconnectedHandler(async e =>
        {
            Console.WriteLine("⚠ MQTT disconnected. Reconnecting in 5 seconds...");

            await Task.Delay(TimeSpan.FromSeconds(5));

            try
            {
                await _mqttClient.ConnectAsync(_options!, stoppingToken);
                Console.WriteLine("🔄 MQTT reconnected");
            }
            catch (Exception ex)
            {
                Console.WriteLine("❌ Reconnect failed: " + ex.Message);
            }
        });

        _options = new MqttClientOptionsBuilder()
            .WithTcpServer(
                _config["Mqtt:Host"],
                int.Parse(_config["Mqtt:Port"]!))
            .WithCredentials(
                _config["Mqtt:Username"],
                _config["Mqtt:Password"])
            .WithTls()
            .Build();

        await _mqttClient.ConnectAsync(_options, stoppingToken);

        Console.WriteLine("✅ MQTT Connected (.NET)");

        await _mqttClient.SubscribeAsync(
            new TopicFilterBuilder()
                .WithTopic("ds32/device/+/heartbeat")
                .Build());

        Console.WriteLine("📡 Subscribed to ds32/device/+/heartbeat");

        // Giữ service sống
        await Task.Delay(Timeout.Infinite, stoppingToken);
    }

    private record DeviceHeartbeatReq(
        string DeviceId,
        string Firmware,
        string? DeviceKey
    );
}
