using MQTTnet;
using MQTTnet.Client;
using MQTTnet.Client.Options;
using MQTTnet.Protocol;
using System.Text;
using System.Text.Json;

namespace VehicleDiag.Api.Services;

public class MqttBackgroundService : BackgroundService
{
    private readonly IConfiguration _config;
    private IMqttClient? _client;
    private IMqttClientOptions? _options;

    public MqttBackgroundService(IConfiguration config)
    {
        _config = config;
    }

    protected override async Task ExecuteAsync(CancellationToken stoppingToken)
    {
        var factory = new MqttFactory();
        _client = factory.CreateMqttClient();

        // ===== MESSAGE HANDLER (MQTTnet v3) =====
        _client.UseApplicationMessageReceivedHandler(e =>
        {
            var payload = Encoding.UTF8.GetString(e.ApplicationMessage.Payload);

            Console.WriteLine($"📥 Topic: {e.ApplicationMessage.Topic}");
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
                    expectedKey != data.DeviceKey)
                {
                    Console.WriteLine("❌ Invalid device key");
                    return;
                }

                DeviceRuntimeState.IsConnected = true;
                DeviceRuntimeState.DeviceName = data.DeviceId;
                DeviceRuntimeState.Firmware = data.Firmware;
                DeviceRuntimeState.LastSeenUtc = DateTime.UtcNow;

                Console.WriteLine("✅ Heartbeat processed");
            }
            catch (Exception ex)
            {
                Console.WriteLine("❌ JSON error: " + ex.Message);
            }
        });

        // ===== DISCONNECT HANDLER (MQTTnet v3) =====
        _client.UseDisconnectedHandler(async e =>
        {
            Console.WriteLine("⚠ MQTT disconnected. Reconnecting in 5s...");

            await Task.Delay(TimeSpan.FromSeconds(5));

            try
            {
                await _client.ConnectAsync(_options!);
                Console.WriteLine("🔄 MQTT reconnected");
            }
            catch (Exception ex)
            {
                Console.WriteLine("❌ Reconnect failed: " + ex.Message);
            }
        });

        // ===== MQTT OPTIONS =====
        _options = new MqttClientOptionsBuilder()
            .WithClientId($"render-api-{Guid.NewGuid():N}")
            .WithTcpServer(
                _config["Mqtt:Host"],
                int.Parse(_config["Mqtt:Port"]!))
            .WithCredentials(
                _config["Mqtt:Username"],
                _config["Mqtt:Password"])
            .WithCleanSession()
            .WithTls() // HiveMQ Cloud TLS
            .Build();

        // ===== CONNECT =====
        await _client.ConnectAsync(_options);

        Console.WriteLine("✅ MQTT Connected (.NET)");

        // ===== SUBSCRIBE =====
        await _client.SubscribeAsync(new TopicFilterBuilder()
            .WithTopic("vehicle/#")
            .WithQualityOfServiceLevel(MqttQualityOfServiceLevel.AtLeastOnce)
            .Build());

        Console.WriteLine("📡 Subscribed to vehicle/#");

        // Keep service alive
        await Task.Delay(Timeout.Infinite, stoppingToken);
    }

    private record DeviceHeartbeatReq(
        string DeviceId,
        string Firmware,
        string? DeviceKey
    );
}
