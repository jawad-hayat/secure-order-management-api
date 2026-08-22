using System;
using System.Collections.Generic;
using System.Linq;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc;
using OrderManagement.Api.Domain.Products;
using Microsoft.Extensions.Logging;
using System.ComponentModel.DataAnnotations;
using OrderManagement.Api.Contracts.Products;
using OrderManagement.Api.Mapping.Products;

namespace OrderManagement.Api.Controllers
{
    [Route("api/products")]
    [ApiController]
    [Produces("application/json")]
    public class ProductController : ControllerBase
    {
        private readonly ILogger<ProductController> _logger;
        // In-memory store for demo purposes. Replace with repository/DB in production.
        private static readonly List<Product> _store = new();

        // Seed with 50 sample products for demo/testing
        static ProductController()
        {
            for (int i = 1; i <= 50; i++)
            {
                try
                {
                    decimal price = decimal.Round(5m + i * 1.25m, 2);
                    int qty = Math.Min(i * 10, 100000);
                    var p = Product.Create($"Product {i}", $"sku{i:0000}", price, qty, $"Sample product {i}", active: true);
                    _store.Add(p);
                }
                catch
                {
                    // ignore seeding errors for demo data
                }
            }
        }

        public ProductController(ILogger<ProductController> logger)
        {
            _logger = logger;
        }

        /// <summary>
        /// List products with paging and optional search.
        /// </summary>
        [HttpGet]
        public ActionResult<IEnumerable<ProductDto>> List([FromQuery] int page = 1, [FromQuery] int pageSize = 20, [FromQuery] string? search = null)
        {
            if (page < 1)
                ModelState.AddModelError("page", "Page must be at least 1.");
            if (pageSize < 1 || pageSize > 100)
                ModelState.AddModelError("pageSize", "PageSize must be between 1 and 100.");

            if (!ModelState.IsValid)
            {
                var v = new ValidationProblemDetails(ModelState)
                {
                    Type = "https://example.com/probs/validation",
                    Title = "One or more validation errors occurred.",
                    Status = StatusCodes.Status400BadRequest,
                    Detail = "See the errors property for details."
                };
                return BadRequest(v);
            }

            IEnumerable<Product> query = _store.Where(p => p.Active);
            if (!string.IsNullOrWhiteSpace(search))
            {
                var s = search.Trim();
                query = query.Where(p => p.Name.Contains(s, StringComparison.OrdinalIgnoreCase)
                                         || p.Sku.Contains(s, StringComparison.OrdinalIgnoreCase));
            }

            // Validate that requested page is within available range
            var totalCount = query.Count();
            var totalPages = (int)Math.Ceiling(totalCount / (double)pageSize);

            if (totalPages > 0 && page > totalPages)
            {
                ModelState.AddModelError("page", $"Page must be between 1 and {totalPages}.");
                var v = new ValidationProblemDetails(ModelState)
                {
                    Type = "https://example.com/probs/validation",
                    Title = "One or more validation errors occurred.",
                    Status = StatusCodes.Status400BadRequest,
                    Detail = "See the errors property for details."
                };
                return BadRequest(v);
            }

            var items = query
                .Skip((page - 1) * pageSize)
                .Take(pageSize)
                .Select(ProductMapping.MapToDto)
                .ToArray();

            return Ok(items);
        }

        /// <summary>
        /// Get a single product by id.
        /// </summary>
        [HttpGet("{id:guid}")]
        public ActionResult<ProductDto> GetById([FromRoute] Guid id)
        {
            var product = _store.FirstOrDefault(p => p.Id == id && p.Active);
            if (product is null)
            {
                return NotFound(new ProblemDetails
                {
                    Type = "https://example.com/probs/not-found",
                    Title = "Product not found",
                    Status = StatusCodes.Status404NotFound,
                    Detail = $"Product with id '{id}' was not found."
                });
            }

            return Ok(ProductMapping.MapToDto(product));
        }

        /// <summary>
        /// Create a new product.
        /// </summary>
        [HttpPost]
        public ActionResult<ProductDto> Create([FromBody] CreateProductRequest req)
        {
            if (req is null)
                return BadRequest(new ProblemDetails { Title = "Request body is required", Status = StatusCodes.Status400BadRequest });

            // Normalize SKU
            req.Sku = req.Sku?.Trim().ToUpperInvariant();

            // Model validation via DataAnnotations
            if (!TryValidateModel(req))
            {
                var v = new ValidationProblemDetails(ModelState)
                {
                    Type = "https://example.com/probs/validation",
                    Title = "One or more validation errors occurred.",
                    Status = StatusCodes.Status400BadRequest,
                    Detail = "See the errors property for details."
                };
                return BadRequest(v);
            }

            // Duplicate SKU check
            if (_store.Any(p => string.Equals(p.Sku, req.Sku, StringComparison.OrdinalIgnoreCase)))
            {
                return Conflict(new ProblemDetails
                {
                    Type = "https://example.com/probs/conflict",
                    Title = "Conflict - duplicate SKU",
                    Status = StatusCodes.Status409Conflict,
                    Detail = "A product with the same SKU already exists."
                });
            }

            // Create domain product and handle domain validation exceptions
            Product product;
            try
            {
                product = Product.Create(req.Name!, req.Sku!, req.Price, req.AvailableQuantity, req.Description, active: true);
            }
            catch (ArgumentOutOfRangeException ex)
            {
                ModelState.AddModelError(ex.ParamName ?? "", ex.Message);
                var v = new ValidationProblemDetails(ModelState)
                {
                    Type = "https://example.com/probs/validation",
                    Title = "One or more validation errors occurred.",
                    Status = StatusCodes.Status400BadRequest,
                    Detail = "See the errors property for details."
                };
                return BadRequest(v);
            }
            catch (ArgumentException ex)
            {
                ModelState.AddModelError(ex.ParamName ?? "", ex.Message);
                var v = new ValidationProblemDetails(ModelState)
                {
                    Type = "https://example.com/probs/validation",
                    Title = "One or more validation errors occurred.",
                    Status = StatusCodes.Status400BadRequest,
                    Detail = "See the errors property for details."
                };
                return BadRequest(v);
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Unexpected error while creating product (SKU: {Sku}).", req.Sku);
                var prob = new ProblemDetails
                {
                    Type = "https://example.com/probs/internal-error",
                    Title = "An unexpected error occurred.",
                    Status = StatusCodes.Status500InternalServerError,
                    Detail = "An unexpected error occurred while creating the product.",
                    Instance = HttpContext.TraceIdentifier
                };
                return StatusCode(StatusCodes.Status500InternalServerError, prob);
            }

            _store.Add(product);

            var dto = ProductMapping.MapToDto(product);
            return CreatedAtAction(nameof(GetById), new { id = product.Id }, dto);
        }

        /// <summary>
        /// Replace editable details of a product.
        /// </summary>
        [HttpPut("{id:guid}")]
        public IActionResult Replace([FromRoute] Guid id, [FromBody] UpdateProductRequest req)
        {
            if (req is null)
                return BadRequest(new ProblemDetails { Title = "Request body is required", Status = StatusCodes.Status400BadRequest });

            // Normalize SKU
            req.Sku = req.Sku?.Trim().ToUpperInvariant();

            if (!TryValidateModel(req))
            {
                var v = new ValidationProblemDetails(ModelState)
                {
                    Type = "https://example.com/probs/validation",
                    Title = "One or more validation errors occurred.",
                    Status = StatusCodes.Status400BadRequest,
                    Detail = "See the errors property for details."
                };
                return BadRequest(v);
            }

            var product = _store.FirstOrDefault(p => p.Id == id && p.Active);
            if (product is null)
                return NotFound(new ProblemDetails { Type = "https://example.com/probs/not-found", Title = "Product not found", Status = StatusCodes.Status404NotFound });

            // Check if SKU conflicts with another product
            if (!string.Equals(product.Sku, req.Sku, StringComparison.OrdinalIgnoreCase)
                && _store.Any(p => p.Id != product.Id && string.Equals(p.Sku, req.Sku, StringComparison.OrdinalIgnoreCase)))
            {
                return Conflict(new ProblemDetails
                {
                    Type = "https://example.com/probs/conflict",
                    Title = "Conflict - duplicate SKU",
                    Status = StatusCodes.Status409Conflict,
                    Detail = "A product with the same SKU already exists."
                });
            }

            try
            {
                product.UpdateName(req.Name!);
                product.UpdateSku(req.Sku!);
                product.UpdateDescription(req.Description);
                product.UpdatePrice(req.Price);
                // adjust quantity directly to requested value
                var delta = req.AvailableQuantity - product.AvailableQuantity;
                if (delta != 0) product.AdjustQuantity(delta);
            }
            catch (ArgumentOutOfRangeException ex)
            {
                ModelState.AddModelError(ex.ParamName ?? "", ex.Message);
                var v = new ValidationProblemDetails(ModelState)
                {
                    Type = "https://example.com/probs/validation",
                    Title = "One or more validation errors occurred.",
                    Status = StatusCodes.Status400BadRequest,
                    Detail = "See the errors property for details."
                };
                return BadRequest(v);
            }
            catch (ArgumentException ex)
            {
                ModelState.AddModelError(ex.ParamName ?? "", ex.Message);
                var v = new ValidationProblemDetails(ModelState)
                {
                    Type = "https://example.com/probs/validation",
                    Title = "One or more validation errors occurred.",
                    Status = StatusCodes.Status400BadRequest,
                    Detail = "See the errors property for details."
                };
                return BadRequest(v);
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Unexpected error while updating product (Id: {Id}).", id);
                var prob = new ProblemDetails
                {
                    Type = "https://example.com/probs/internal-error",
                    Title = "An unexpected error occurred.",
                    Status = StatusCodes.Status500InternalServerError,
                    Detail = "An unexpected error occurred while updating the product.",
                    Instance = HttpContext.TraceIdentifier
                };
                return StatusCode(StatusCodes.Status500InternalServerError, prob);
            }

            return NoContent();
        }

        /// <summary>
        /// Deactivate (soft-delete) a product.
        /// </summary>
        [HttpDelete("{id:guid}")]
        public IActionResult Deactivate([FromRoute] Guid id)
        {
            var product = _store.FirstOrDefault(p => p.Id == id && p.Active);
            if (product is null)
                return NotFound(new ProblemDetails { Type = "https://example.com/probs/not-found", Title = "Product not found", Status = StatusCodes.Status404NotFound });

            product.Deactivate();
            return NoContent();
        }
    }
}
