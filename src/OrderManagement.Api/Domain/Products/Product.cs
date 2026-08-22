using System;

namespace OrderManagement.Api.Domain.Products
{
    /// <summary>
    /// Represents a product in the catalog.
    /// </summary>
    public class Product
    {
        /// <summary>Unique identifier.</summary>
        public Guid Id { get; private set; }

        /// <summary>Product name (required, 3–120 characters).</summary>
        public string Name { get; private set; }

        /// <summary>Stock Keeping Unit (required, normalized to uppercase, 3–50 characters).</summary>
        public string Sku { get; private set; }

        /// <summary>Optional product description (max 1000 characters).</summary>
        public string? Description { get; private set; }

        /// <summary>Price (must be &gt; 0, up to two decimal places).</summary>
        public decimal Price { get; private set; }

        /// <summary>Available quantity (0–100000).</summary>
        public int AvailableQuantity { get; private set; }

        /// <summary>Whether the product is active.</summary>
        public bool Active { get; private set; }

        /// <summary>Creation timestamp (UTC).</summary>
        public DateTimeOffset CreatedAt { get; private set; }

        /// <summary>Last update timestamp (UTC).</summary>
        public DateTimeOffset UpdatedAt { get; private set; }

        // Parameterless ctor for serializers/ORMs
        private Product() { }

        /// <summary>
        /// Create a new product with validation.
        /// </summary>
        public static Product Create(string name, string sku, decimal price, int availableQuantity = 0, string? description = null, bool active = true)
        {
            ValidateName(name);
            var normalizedSku = NormalizeSku(sku);
            ValidateSku(normalizedSku);
            ValidateDescription(description);
            ValidatePrice(price);
            ValidateAvailableQuantity(availableQuantity);

            var now = DateTimeOffset.UtcNow;

            return new Product
            {
                Id = Guid.NewGuid(),
                Name = name,
                Sku = normalizedSku,
                Description = description,
                Price = price,
                AvailableQuantity = availableQuantity,
                Active = active,
                CreatedAt = now,
                UpdatedAt = now
            };
        }

        /// <summary>Update product name with validation.</summary>
        public void UpdateName(string name)
        {
            ValidateName(name);
            Name = name;
            Touch();
        }

        /// <summary>Update SKU (will be normalized to uppercase).</summary>
        public void UpdateSku(string sku)
        {
            var normalized = NormalizeSku(sku);
            ValidateSku(normalized);
            Sku = normalized;
            Touch();
        }

        /// <summary>Update optional description.</summary>
        public void UpdateDescription(string? description)
        {
            ValidateDescription(description);
            Description = description;
            Touch();
        }

        /// <summary>Update price with validation.</summary>
        public void UpdatePrice(decimal price)
        {
            ValidatePrice(price);
            Price = price;
            Touch();
        }

        /// <summary>Adjust available quantity by delta. Result will be constrained to [0,100000].</summary>
        public void AdjustQuantity(int delta)
        {
            var newQty = AvailableQuantity + delta;
            if (newQty < 0 || newQty > 100_000)
            {
                throw new ArgumentOutOfRangeException(nameof(delta), "Resulting quantity must be between 0 and 100000.");
            }

            AvailableQuantity = newQty;
            Touch();
        }

        /// <summary>Mark product active.</summary>
        public void Activate()
        {
            if (!Active)
            {
                Active = true;
                Touch();
            }
        }

        /// <summary>Mark product inactive.</summary>
        public void Deactivate()
        {
            if (Active)
            {
                Active = false;
                Touch();
            }
        }

        private void Touch()
        {
            UpdatedAt = DateTimeOffset.UtcNow;
        }

        #region Validation helpers

        private static void ValidateName(string name)
        {
            if (string.IsNullOrWhiteSpace(name))
                throw new ArgumentException("Name is required.", nameof(name));

            var len = name.Trim().Length;
            if (len < 3 || len > 120)
                throw new ArgumentException("Name must be between 3 and 120 characters.", nameof(name));
        }

        private static string NormalizeSku(string? sku)
        {
            if (sku is null) return string.Empty;
            return sku.Trim().ToUpperInvariant();
        }

        private static void ValidateSku(string sku)
        {
            if (string.IsNullOrWhiteSpace(sku))
                throw new ArgumentException("SKU is required.", nameof(sku));

            var len = sku.Length;
            if (len < 3 || len > 50)
                throw new ArgumentException("SKU must be between 3 and 50 characters.", nameof(sku));
        }

        private static void ValidateDescription(string? description)
        {
            if (description is null) return;
            if (description.Length > 1000)
                throw new ArgumentException("Description cannot be longer than 1000 characters.", nameof(description));
        }

        private static void ValidatePrice(decimal price)
        {
            if (price <= 0m)
                throw new ArgumentOutOfRangeException(nameof(price), "Price must be greater than zero.");

            // Ensure at most two decimal places
            if (decimal.Round(price, 2) != price)
                throw new ArgumentException("Price can have at most two decimal places.", nameof(price));
        }

        private static void ValidateAvailableQuantity(int qty)
        {
            if (qty < 0 || qty > 100_000)
                throw new ArgumentOutOfRangeException(nameof(qty), "AvailableQuantity must be between 0 and 100000.");
        }

        #endregion
    }
}
