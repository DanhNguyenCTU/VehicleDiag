using MQTTnet;
using MQTTnet.Client;
using MQTTnet.Protocol;
using System.Text.Json;

namespace VehicleDiag.Api.Services
{
    public class MqttPublishService : IMqttPublishService
    {
        private readonly IMqttClient _client;

        public MqttPublishService()
        {
            var factory = new MqttFactory();
            _client = factory.CreateMqttClient();

            Connect();
        }

        private void Connect()
        {
            try
            {
                if (_client.IsConnected)
                    return;

                Console.WriteLine("MQTT connecting (PublishService)...");

                var options = new MqttClientOptionsBuilder()
                    .WithTcpServer(
                        "dc3f111e040f4e8d9b48df7616b1b80e.s1.eu.hivemq.cloud",
                        8883)
                    .WithCredentials(
                        "ds_backend",
                        "DSbackend32265")
                    .WithTlsOptions(o =>
                    {
                        o.UseTls();
                    })
                    .Build();

                _client.ConnectAsync(options).GetAwaiter().GetResult();

                Console.WriteLine("MQTT connected (PublishService)");
            }
            catch (Exception ex)
            {
                Console.WriteLine("MQTT CONNECT ERROR:");
                Console.WriteLine(ex.ToString());
            }
        }

        public async Task PublishCommand(string deviceId, object payload)
        {
            try
            {
                if (!_client.IsConnected)
                {
                    Console.WriteLine("MQTT reconnecting...");
                    Connect();
                }

                var topic = $"vehicle/{deviceId}/command";
                var json = JsonSerializer.Serialize(payload);

                Console.WriteLine("====================================");
                Console.WriteLine("MQTT COMMAND SEND");
                Console.WriteLine($"Topic  : {topic}");
                Console.WriteLine($"Payload: {json}");
                Console.WriteLine("====================================");

                var message = new MqttApplicationMessageBuilder()
                    .WithTopic(topic)
                    .WithPayload(json)
                    .WithQualityOfServiceLevel(
                        MqttQualityOfServiceLevel.AtLeastOnce)
                    .Build();

                var result = await _client.PublishAsync(message);

                Console.WriteLine($"MQTT RESULT: {result.ReasonCode}");
            }
            catch (Exception ex)
            {
                Console.WriteLine("MQTT PUBLISH ERROR:");
                Console.WriteLine(ex.ToString());
            }
        }
    }
}