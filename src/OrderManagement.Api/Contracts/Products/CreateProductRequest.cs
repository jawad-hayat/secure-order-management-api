using System.ComponentModel.DataAnnotations;

namespace OrderManagement.Api.Contracts.Products
{
    public sealed class CreateProductRequest
    {
        [Required]
        [StringLength(120, MinimumLength = 3)]
        public string? Name { get; set; }

        [Required]
        [StringLength(50, MinimumLength = 3)]
        public string? Sku { get; set; }

        [StringLength(1000)]
        public string? Description { get; set; }

        [Range(0.01, double.MaxValue)]
        public decimal Price { get; set; }

        [Range(0, 100000)]
        public int AvailableQuantity { get; set; }
    }
}
