using MQTTnet;
using MQTTnet.Client;
using System.Text.Json;

namespace VehicleDiag.Api.Services;

public class MqttPublishService : IMqttPublishService
{
    private readonly IMqttClient _client;

    public MqttPublishService()
    {
        var factory = new MqttFactory();
        _client = factory.CreateMqttClient();

        var options = new MqttClientOptionsBuilder()
            .WithTcpServer("dc3f111e040f4e8d9b48df7616b1b80e.s1.eu.hivemq.cloud", 8883)
            .WithCredentials("ds_backend", "DSbackend32265")
            .WithTlsOptions(o =>
            {
                o.UseTls();
            })
            .Build();

        _client.ConnectAsync(options).GetAwaiter().GetResult();
    }

    public async Task PublishCommand(string deviceId, object payload)
    {
        var topic = $"vehicle/{deviceId}/command";

        var message = new MqttApplicationMessageBuilder()
            .WithTopic(topic)
            .WithPayload(JsonSerializer.Serialize(payload))
            .Build();

        await _client.PublishAsync(message);
    }
}