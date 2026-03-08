namespace VehicleDiag.Api.Services
{
    public interface IMqttPublishService
    {
        Task PublishCommand(string deviceId, object payload);
    }
}
