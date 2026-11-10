using DemoCrud.Api.Models.Entities;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace DemoCrud.Api.Data.Configurations;

public sealed class ItemConfiguration : IEntityTypeConfiguration<Item>
{
    public void Configure(EntityTypeBuilder<Item> entity)
    {
        entity.ToTable("items", "democrud_demo", table =>
            table.HasCheckConstraint("items_name_check", "length(trim(name)) > 0"));
        entity.HasKey(item => item.Id).HasName("items_pkey");
        entity.Property(item => item.Id).HasColumnName("id").HasColumnType("uuid").ValueGeneratedNever();
        entity.Property(item => item.Name).HasColumnName("name").HasMaxLength(200).IsRequired();
        entity.Property(item => item.Description).HasColumnName("description").HasMaxLength(2000);
        entity.Property(item => item.IsCompleted).HasColumnName("is_completed").HasDefaultValue(false);
        entity.Property(item => item.CreatedAt).HasColumnName("created_at")
            .HasColumnType("timestamp with time zone").HasDefaultValueSql("now()");
        entity.Property(item => item.UpdatedAt).HasColumnName("updated_at")
            .HasColumnType("timestamp with time zone").HasDefaultValueSql("now()");
    }
}
