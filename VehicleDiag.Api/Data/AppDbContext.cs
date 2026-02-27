using Microsoft.EntityFrameworkCore;
using VehicleDiag.Api.Models;

namespace VehicleDiag.Api.Data;

public class AppDbContext : DbContext
{
    public AppDbContext(DbContextOptions<AppDbContext> options)
        : base(options) { }

    public DbSet<AppUser> AppUsers => Set<AppUser>();
    public DbSet<Vehicle> Vehicles => Set<Vehicle>();
    public DbSet<VehicleModel> VehicleModels => Set<VehicleModel>();
    public DbSet<Device> Devices => Set<Device>();
    public DbSet<Telemetry> Telemetry => Set<Telemetry>();
    public DbSet<Ecu> Ecus => Set<Ecu>();
    public DbSet<EcuProtocol> EcuProtocols => Set<EcuProtocol>();
    public DbSet<ManufacturerBrand> ManufacturerBrands => Set<ManufacturerBrand>();
    public DbSet<EcuReadSession> EcuReadSessions => Set<EcuReadSession>();
    public DbSet<EcuInfoResult> EcuInfoResults => Set<EcuInfoResult>();
    public DbSet<EcuDtcResult> EcuDtcResults => Set<EcuDtcResult>();
    public DbSet<EcuDtcCurrent> EcuDtcCurrent => Set<EcuDtcCurrent>();
    public DbSet<DtcDictionary> DtcDictionary => Set<DtcDictionary>();
    public DbSet<UserVehicle> UserVehicles => Set<UserVehicle>();

    protected override void OnModelCreating(ModelBuilder modelBuilder)
    {
        // ================= TABLE MAPPING =================

        modelBuilder.Entity<AppUser>()
            .ToTable("AppUser")
            .HasKey(x => x.Id);

        modelBuilder.Entity<VehicleModel>()
            .ToTable("VehicleModel")
            .HasKey(x => x.ModelId);

        modelBuilder.Entity<Vehicle>()
            .ToTable("Vehicle")
            .HasKey(x => x.VehicleId);

        modelBuilder.Entity<Device>()
            .ToTable("Device")
            .HasKey(x => x.DeviceId);

        modelBuilder.Entity<Telemetry>()
            .ToTable("Telemetry")
            .HasKey(x => x.Id);
        modelBuilder.Entity<ManufacturerBrand>()
            .ToTable("ManufacturerBrand")
            .HasNoKey();

        modelBuilder.Entity<Ecu>()
            .ToTable("Ecu")
            .HasKey(x => x.EcuId);

        modelBuilder.Entity<EcuProtocol>()
            .ToTable("EcuProtocol")
            .HasKey(x => x.Id);

        modelBuilder.Entity<EcuReadSession>()
            .ToTable("EcuReadSession")
            .HasKey(x => x.SessionId);

        modelBuilder.Entity<EcuInfoResult>()
            .ToTable("EcuInfoResult")
            .HasKey(x => x.Id);

        modelBuilder.Entity<EcuDtcResult>()
            .ToTable("EcuDtcResult")
            .HasKey(x => x.Id);

        modelBuilder.Entity<EcuDtcCurrent>()
            .ToTable("EcuDtcCurrent")
            .HasKey(x => new { x.VehicleId, x.DtcCode });

        modelBuilder.Entity<DtcDictionary>()
            .ToTable("DtcDictionary")
            .HasNoKey();

        modelBuilder.Entity<UserVehicle>()
            .ToTable("UserVehicle")
            .HasKey(x => x.Id);

        // ================= RELATIONSHIPS =================

        // Vehicle → VehicleModel (Many Vehicle, One Model)
        modelBuilder.Entity<Vehicle>()
            .HasOne(v => v.VehicleModel)
            .WithMany()
            .HasForeignKey(v => v.ModelId)
            .OnDelete(DeleteBehavior.Restrict);

        // Vehicle → Device (One to One logical, but FK stored in Vehicle)
        modelBuilder.Entity<Vehicle>()
            .HasOne<Device>()
            .WithMany()
            .HasForeignKey(v => v.DeviceId)
            .OnDelete(DeleteBehavior.SetNull);

        // UserVehicle → AppUser
        modelBuilder.Entity<UserVehicle>()
            .HasOne<AppUser>()
            .WithMany()
            .HasForeignKey(x => x.UserId)
            .OnDelete(DeleteBehavior.Cascade);

        // UserVehicle → Vehicle
        modelBuilder.Entity<UserVehicle>()
            .HasOne<Vehicle>()
            .WithMany()
            .HasForeignKey(x => x.VehicleId)
            .OnDelete(DeleteBehavior.Cascade);
    }
}