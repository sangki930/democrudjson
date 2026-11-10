using DemoCrud.Api.Models.Entities;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace DemoCrud.Api.Data.Configurations;

public sealed class LayerConfiguration : IEntityTypeConfiguration<Layer>
{
    public void Configure(EntityTypeBuilder<Layer> entity)
    {
        entity.ToTable("LAYER", "democrud_demo", table =>
        {
            table.HasCheckConstraint("LAYER_id_check", "length(trim(id)) > 0");
            table.HasCheckConstraint("LAYER_data_check", "jsonb_typeof(data) = 'object'");
        });
        entity.HasKey(layer => layer.Id).HasName("LAYER_pkey");
        entity.Property(layer => layer.Id).HasColumnName("id").HasMaxLength(24).ValueGeneratedNever();
        entity.Property(layer => layer.Data).HasColumnName("data").HasColumnType("jsonb").IsRequired();
    }
}

