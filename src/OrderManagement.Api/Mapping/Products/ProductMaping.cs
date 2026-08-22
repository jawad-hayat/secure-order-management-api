using OrderManagement.Api.Contracts.Products;
using OrderManagement.Api.Domain.Products;

namespace OrderManagement.Api.Mapping.Products
{
    public class ProductMapping
    {
        public static ProductDto MapToDto(Product p) => new ProductDto
        {
            Id = p.Id,
            Name = p.Name,
            Sku = p.Sku,
            Description = p.Description,
            Price = p.Price,
            AvailableQuantity = p.AvailableQuantity,
            Active = p.Active,
            CreatedAt = p.CreatedAt,
            UpdatedAt = p.UpdatedAt
        };
    }
}
