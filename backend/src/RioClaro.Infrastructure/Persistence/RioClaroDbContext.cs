using Microsoft.EntityFrameworkCore;
using RioClaro.Domain.Entities;

namespace RioClaro.Infrastructure.Persistence;

public sealed class RioClaroDbContext(DbContextOptions<RioClaroDbContext> options) : DbContext(options)
{
    public DbSet<Organization> Organizations => Set<Organization>();
    public DbSet<ApplicationUser> Users => Set<ApplicationUser>();

    protected override void OnModelCreating(ModelBuilder modelBuilder)
    {
        modelBuilder.Entity<Organization>(entity =>
        {
            entity.ToTable("Organizations");
            entity.HasKey(organization => organization.Id);
            entity.Property(organization => organization.Name)
                .HasMaxLength(200)
                .IsRequired();
        });

        modelBuilder.Entity<ApplicationUser>(entity =>
        {
            entity.ToTable("Users");
            entity.HasKey(user => user.Id);
            entity.Property(user => user.DisplayName)
                .HasMaxLength(200)
                .IsRequired();
            entity.Property(user => user.Role)
                .HasMaxLength(50)
                .IsRequired();
            entity.HasOne(user => user.Organization)
                .WithMany(organization => organization.Users)
                .HasForeignKey(user => user.OrganizationId)
                .OnDelete(DeleteBehavior.Restrict);
        });
    }
}
