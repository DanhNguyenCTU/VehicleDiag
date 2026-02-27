namespace VehicleDiag.Api.Models
{
    public class UserVehicle
    {
        public int Id { get; set; }
        public int UserId { get; set; }
        public int VehicleId { get; set; }
        public DateTime? AssignedAt { get; set; }
    }
}