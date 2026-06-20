using EntityFrameworkCore.CreatedUpdatedDate.Extensions;
using InCleanHome.ProfileService.Domain.Model.Aggregates;
using InCleanHome.ProfileService.Infrastructure.Persistence.Extensions;
using Microsoft.EntityFrameworkCore;

namespace InCleanHome.ProfileService.Infrastructure.Persistence;

public class ProfileDbContext(DbContextOptions<ProfileDbContext> options) : DbContext(options)
{
    public DbSet<ClientProfile> ClientProfiles => Set<ClientProfile>();
    public DbSet<WorkerProfile> WorkerProfiles => Set<WorkerProfile>();

    protected override void OnConfiguring(DbContextOptionsBuilder builder)
    {
        builder.AddCreatedUpdatedInterceptor();
        base.OnConfiguring(builder);
    }

    protected override void OnModelCreating(ModelBuilder builder)
    {
        base.OnModelCreating(builder);

        builder.Entity<ClientProfile>().HasKey(c => c.Id);
        builder.Entity<ClientProfile>().Property(c => c.Id).IsRequired().ValueGeneratedOnAdd();
        builder.Entity<ClientProfile>().Property(c => c.UserId).IsRequired();
        builder.Entity<ClientProfile>().Property(c => c.Name).IsRequired().HasMaxLength(120);
        builder.Entity<ClientProfile>().Property(c => c.Phone).HasMaxLength(20);
        builder.Entity<ClientProfile>().Property(c => c.PhotoUrl);
        builder.Entity<ClientProfile>().HasIndex(c => c.UserId).IsUnique();

        builder.Entity<WorkerProfile>().HasKey(w => w.Id);
        builder.Entity<WorkerProfile>().Property(w => w.Id).IsRequired().ValueGeneratedOnAdd();
        builder.Entity<WorkerProfile>().Property(w => w.UserId).IsRequired();
        builder.Entity<WorkerProfile>().Property(w => w.Name).IsRequired().HasMaxLength(120);
        builder.Entity<WorkerProfile>().Property(w => w.Phone).HasMaxLength(20);
        builder.Entity<WorkerProfile>().Property(w => w.Age);
        builder.Entity<WorkerProfile>().Property(w => w.Gender).HasMaxLength(20);
        builder.Entity<WorkerProfile>().Property(w => w.HourlyRate).HasPrecision(10, 2);
        builder.Entity<WorkerProfile>().Property(w => w.HourlyRateSunday).HasPrecision(10, 2);
        builder.Entity<WorkerProfile>().Property(w => w.ExperienceYears);
        builder.Entity<WorkerProfile>().Property(w => w.Bio).HasMaxLength(1000);
        builder.Entity<WorkerProfile>().Property(w => w.AverageRating).HasPrecision(3, 2);
        builder.Entity<WorkerProfile>().Property(w => w.TotalServices);
        builder.Entity<WorkerProfile>().Property(w => w.PhotoUrl);
        builder.Entity<WorkerProfile>().HasIndex(w => w.UserId).IsUnique();

        builder.Entity<WorkerProfile>().Property(w => w.ServiceTypes).HasColumnType("text[]");
        builder.Entity<WorkerProfile>().Property(w => w.Zones).HasColumnType("text[]");

        builder.UseSnakeCaseNamingConvention();
    }
}
