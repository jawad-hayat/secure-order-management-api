using System;
using Microsoft.EntityFrameworkCore;
using Microsoft.AspNetCore.Identity.EntityFrameworkCore;
using Microsoft.AspNetCore.Identity;
using OrderManagement.Api.Domain.Products;
using OrderManagement.Api.Infrastructure.Persistence;
using OrderManagement.Api.Infrastructure.Identity;

namespace OrderManagement.Api.Infrastructure
{
    // Identity-aware DbContext using Guid keys
    public class OrderManagementDbContext : IdentityDbContext<ApplicationUser, IdentityRole<Guid>, Guid>
    {
        public OrderManagementDbContext(DbContextOptions<OrderManagementDbContext> options) : base(options)
        {
        }

        public DbSet<Product> Products { get; set; } = null!;

        protected override void OnModelCreating(ModelBuilder modelBuilder)
        {
            base.OnModelCreating(modelBuilder);

            // Apply product mapping
            modelBuilder.ApplyConfiguration(new ProductEntityConfiguration());

            // Apply global query filter for soft-delete. Product now has a real IsDeleted property
            modelBuilder.Entity<Product>().HasQueryFilter(p => !p.IsDeleted);
        }
    }
}
