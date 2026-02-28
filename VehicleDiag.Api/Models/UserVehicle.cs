namespace VehicleDiag.Api.Models
{
    public class UserVehicle
    {
        public int Id { get; set; }

        public int VehicleId { get; set; }
        public Vehicle Vehicle { get; set; } = null!; 

        public int UserId { get; set; }
        public AppUser User { get; set; } = null!;     

        public DateTime AssignedAt { get; set; }
    }
}