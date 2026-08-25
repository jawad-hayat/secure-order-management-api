using Microsoft.EntityFrameworkCore;
using OrderManagement.Api.Domain.Products;
using OrderManagement.Api.Infrastructure.Persistence;

namespace OrderManagement.Api.Infrastructure
{
    public class OrderManagementDbContext : DbContext
    {
        public OrderManagementDbContext(DbContextOptions<OrderManagementDbContext> options) : base(options)
        {
        }

        public DbSet<Product> Products { get; set; } = null!;

        protected override void OnModelCreating(ModelBuilder modelBuilder)
        {
            base.OnModelCreating(modelBuilder);

            modelBuilder.ApplyConfiguration(new ProductEntityConfiguration());

            // Apply global query filter for soft-delete. Product defines IsDeleted as shadow or real property
            modelBuilder.Entity<Product>().HasQueryFilter(p => EF.Property<bool>(p, "IsDeleted") == false);
        }
    }
}
