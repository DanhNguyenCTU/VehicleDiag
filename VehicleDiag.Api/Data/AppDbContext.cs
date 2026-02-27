using Microsoft.EntityFrameworkCore;
using VehicleDiag.Api.Models;
namespace VehicleDiag.Api.Data;
public class AppDbContext : DbContext
{
    public AppDbContext(DbContextOptions<AppDbContext> options)
        : base(options) { }

    public DbSet<AppUser> AppUsers => Set<AppUser>();
    public DbSet<Vehicle> Vehicles => Set<Vehicle>();
    public DbSet<Device> Devices => Set<Device>();
    public DbSet<Telemetry> Telemetry => Set<Telemetry>();
    public DbSet<Ecu> Ecus => Set<Ecu>();
    public DbSet<EcuProtocol> EcuProtocols => Set<EcuProtocol>();

    public DbSet<EcuReadSession> EcuReadSessions => Set<EcuReadSession>();
    public DbSet<EcuInfoResult> EcuInfoResults => Set<EcuInfoResult>();
    public DbSet<EcuDtcResult> EcuDtcResults => Set<EcuDtcResult>();
    public DbSet<EcuDtcCurrent> EcuDtcCurrent => Set<EcuDtcCurrent>();
    public DbSet<DtcDictionary> DtcDictionary => Set<DtcDictionary>();
    public DbSet<UserVehicle> UserVehicles => Set<UserVehicle>();

    protected override void OnModelCreating(ModelBuilder modelBuilder)
    {
        modelBuilder.Entity<AppUser>().ToTable("AppUser").HasKey(x => x.Id);
        modelBuilder.Entity<EcuDtcCurrent>().HasKey(x => new { x.VehicleId, x.DtcCode });
        modelBuilder.Entity<UserVehicle>().ToTable("UserVehicle").HasKey(x => x.Id);
        modelBuilder.Entity<Vehicle>().ToTable("Vehicle").HasKey(x => x.VehicleId);
        modelBuilder.Entity<Device>().ToTable("Device").HasKey(x => x.DeviceId);
        modelBuilder.Entity<Telemetry>().ToTable("Telemetry").HasKey(x => x.Id);
        modelBuilder.Entity<Ecu>().ToTable("Ecu").HasKey(x => x.EcuId);
        modelBuilder.Entity<EcuReadSession>().ToTable("EcuReadSession").HasKey(x => x.SessionId);
        modelBuilder.Entity<EcuInfoResult>().ToTable("EcuInfoResult").HasKey(x => x.Id);
        modelBuilder.Entity<EcuDtcResult>().ToTable("EcuDtcResult").HasKey(x => x.Id);
        modelBuilder.Entity<DtcDictionary>().ToTable("DtcDictionary").HasNoKey();
        modelBuilder.Entity<EcuProtocol>().ToTable("EcuProtocol").HasKey(x => x.Id);
    }

}
