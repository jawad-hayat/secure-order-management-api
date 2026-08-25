using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using OrderManagement.Api.Domain.Products;

namespace OrderManagement.Api.Infrastructure.Persistence
{
    public class ProductEntityConfiguration : IEntityTypeConfiguration<Product>
    {
        public void Configure(EntityTypeBuilder<Product> builder)
        {
            builder.ToTable("products");

            builder.HasKey(p => p.Id);

            builder.Property(p => p.Name)
                .IsRequired()
                .HasMaxLength(120);

            builder.Property(p => p.Sku)
                .IsRequired()
                .HasMaxLength(50)
                .HasColumnName("sku");

            builder.HasIndex(p => p.Sku)
                .IsUnique()
                .HasDatabaseName("ux_products_sku");

            builder.Property(p => p.Description)
                .HasMaxLength(1000);

            builder.Property(p => p.Price)
                .HasPrecision(18, 2)
                .HasColumnName("price");

            builder.Property(p => p.AvailableQuantity)
                .HasColumnName("available_quantity");

            builder.Property(p => p.Active)
                .HasColumnName("active");

            // Map domain CreatedAt/UpdatedAt to timestamp with time zone columns
            builder.Property(p => p.CreatedAt)
                .HasColumnType("timestamptz")
                .HasColumnName("created_at_utc");

            builder.Property(p => p.UpdatedAt)
                .HasColumnType("timestamptz")
                .HasColumnName("updated_at_utc");

            // Soft-delete flag
            builder.Property<bool>("IsDeleted")
                .HasColumnName("is_deleted")
                .HasDefaultValue(false);

            // Global index to help active product listing/search: active + sku
            builder.HasIndex(p => new { p.Active, p.Sku })
                .HasDatabaseName("ix_products_active_sku");

            // Global query filter applied in DbContext ensures we only return non-deleted rows
        }
    }
}
