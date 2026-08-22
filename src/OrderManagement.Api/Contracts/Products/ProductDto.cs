namespace OrderManagement.Api.Contracts.Products
{
    public sealed class ProductDto
    {
        public Guid Id { get; set; }
        public string Name { get; set; } = default!;
        public string Sku { get; set; } = default!;
        public string? Description { get; set; }
        public decimal Price { get; set; }
        public int AvailableQuantity { get; set; }
        public bool Active { get; set; }
        public DateTimeOffset CreatedAtUtc { get; set; }
        public DateTimeOffset UpdatedAtUtc { get; set; }
    }
}
