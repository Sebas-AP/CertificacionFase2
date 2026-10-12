using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Infrastructure;
using Microsoft.EntityFrameworkCore.Metadata;
using Microsoft.EntityFrameworkCore.Migrations;
using RioClaro.Infrastructure.Persistence;

#nullable disable

namespace RioClaro.Infrastructure.Migrations;

[DbContext(typeof(RioClaroDbContext))]
partial class RioClaroDbContextModelSnapshot : ModelSnapshot
{
    protected override void BuildModel(ModelBuilder modelBuilder)
    {
        modelBuilder
            .HasAnnotation("ProductVersion", "8.0.22")
            .HasAnnotation("Relational:MaxIdentifierLength", 128);

        modelBuilder.Entity("RioClaro.Domain.Entities.Organization", entity =>
        {
            entity.Property<Guid>("Id")
                .ValueGeneratedOnAdd()
                .HasColumnType("uniqueidentifier");

            entity.Property<string>("Name")
                .IsRequired()
                .HasMaxLength(200)
                .HasColumnType("nvarchar(200)");

            entity.HasKey("Id");
            entity.ToTable("Organizations");
        });

        modelBuilder.Entity("RioClaro.Domain.Entities.ApplicationUser", entity =>
        {
            entity.Property<Guid>("Id")
                .ValueGeneratedOnAdd()
                .HasColumnType("uniqueidentifier");

            entity.Property<string>("DisplayName")
                .IsRequired()
                .HasMaxLength(200)
                .HasColumnType("nvarchar(200)");

            entity.Property<Guid>("OrganizationId")
                .HasColumnType("uniqueidentifier");

            entity.Property<string>("Role")
                .IsRequired()
                .HasMaxLength(50)
                .HasColumnType("nvarchar(50)");

            entity.HasKey("Id");

            entity.HasIndex("OrganizationId");

            entity.ToTable("Users");
        });

        modelBuilder.Entity("RioClaro.Domain.Entities.ApplicationUser", entity =>
        {
            entity.HasOne("RioClaro.Domain.Entities.Organization", "Organization")
                .WithMany("Users")
                .HasForeignKey("OrganizationId")
                .OnDelete(DeleteBehavior.Restrict)
                .IsRequired();

            entity.Navigation("Organization");
        });

        modelBuilder.Entity("RioClaro.Domain.Entities.Organization", entity =>
        {
            entity.Navigation("Users");
        });
    }
}
