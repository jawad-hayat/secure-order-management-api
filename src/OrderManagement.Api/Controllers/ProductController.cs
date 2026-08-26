using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc;
using OrderManagement.Api.Domain.Products;
using Microsoft.Extensions.Logging;
using System.ComponentModel.DataAnnotations;
using OrderManagement.Api.Contracts.Products;
using OrderManagement.Api.Mapping.Products;
using OrderManagement.Api.Infrastructure;
using Npgsql;
using Microsoft.EntityFrameworkCore;
using System.Threading;
using Microsoft.EntityFrameworkCore;

namespace OrderManagement.Api.Controllers
{
    [Route("api/products")]
    [ApiController]
    [Produces("application/json")]
    public class ProductController : ControllerBase
    {
        private readonly ILogger<ProductController> _logger;
        private readonly OrderManagementDbContext _db;

        public ProductController(ILogger<ProductController> logger, OrderManagementDbContext db)
        {
            _logger = logger;
            _db = db;
        }

        /// <summary>
        /// List products with paging and optional search.
        /// </summary>
        [HttpGet]
        public async Task<ActionResult<IEnumerable<ProductDto>>> List([FromQuery] int page = 1, [FromQuery] int pageSize = 20, [FromQuery] string? search = null, CancellationToken cancellationToken = default)
        {
            if (page < 1)
                ModelState.AddModelError("page", "Page must be at least 1.");
            if (pageSize < 1 || pageSize > 100)
                ModelState.AddModelError("pageSize", "PageSize must be between 1 and 100.");

            if (!ModelState.IsValid)
            {
                return BadRequest(OrderManagement.Api.Infrastructure.ProblemDetailsFactory.CreateValidationProblemDetails(ModelState, HttpContext));
            }

            IQueryable<Product> query = _db.Products.AsNoTracking().Where(p => p.Active);
            if (!string.IsNullOrWhiteSpace(search))
            {
                var s = search.Trim();
                // Use case-insensitive match via database function when available
                query = query.Where(p => EF.Functions.ILike(p.Name, $"%{s}%") || EF.Functions.ILike(p.Sku, $"%{s}%"));
            }

            // Validate that requested page is within available range
            var totalCount = await query.CountAsync(cancellationToken);
            var totalPages = (int)Math.Ceiling(totalCount / (double)pageSize);

            if (totalPages > 0 && page > totalPages)
            {
                ModelState.AddModelError("page", $"Page must be between 1 and {totalPages}.");
                return BadRequest(OrderManagement.Api.Infrastructure.ProblemDetailsFactory.CreateValidationProblemDetails(ModelState, HttpContext));
            }

            var items = await query
                .OrderBy(p => p.Name)
                .Skip((page - 1) * pageSize)
                .Take(pageSize)
                .Select(p => new ProductDto
                {
                    Id = p.Id,
                    Name = p.Name,
                    Sku = p.Sku,
                    Description = p.Description,
                    Price = p.Price,
                    AvailableQuantity = p.AvailableQuantity,
                    Active = p.Active,
                    CreatedAtUtc = p.CreatedAt,
                    UpdatedAtUtc = p.UpdatedAt
                })
                .ToArrayAsync(cancellationToken);

            return Ok(items);
        }

        /// <summary>
        /// Get a single product by id.
        /// </summary>
        [HttpGet("{id:guid}")]
        public async Task<ActionResult<ProductDto>> GetById([FromRoute] Guid id, CancellationToken cancellationToken = default)
        {
            var product = await _db.Products.AsNoTracking()
                .Where(p => p.Id == id && p.Active)
                .Select(p => new ProductDto
                {
                    Id = p.Id,
                    Name = p.Name,
                    Sku = p.Sku,
                    Description = p.Description,
                    Price = p.Price,
                    AvailableQuantity = p.AvailableQuantity,
                    Active = p.Active,
                    CreatedAtUtc = p.CreatedAt,
                    UpdatedAtUtc = p.UpdatedAt
                })
                .FirstOrDefaultAsync(cancellationToken);
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

            return Ok(product);
        }

        /// <summary>
        /// Create a new product.
        /// </summary>
        [HttpPost]
        public async Task<ActionResult<ProductDto>> Create([FromBody] CreateProductRequest req, CancellationToken cancellationToken = default)
        {
            if (req is null)
                return BadRequest(new ProblemDetails { Title = "Request body is required", Status = StatusCodes.Status400BadRequest });

            // Normalize SKU
            req.Sku = req.Sku?.Trim().ToUpperInvariant();

            // Model validation via DataAnnotations
            if (!TryValidateModel(req))
            {
                return BadRequest(OrderManagement.Api.Infrastructure.ProblemDetailsFactory.CreateValidationProblemDetails(ModelState, HttpContext));
            }

            // Create domain product and handle domain validation exceptions; let DB unique constraint handle races
            Product product;
            try
            {
                product = Product.Create(req.Name!, req.Sku!, req.Price, req.AvailableQuantity, req.Description, active: true);
                _db.Products.Add(product);
                await _db.SaveChangesAsync(cancellationToken);
            }
            catch (ArgumentOutOfRangeException ex)
            {
                ModelState.AddModelError(ex.ParamName ?? "", ex.Message);
                return BadRequest(OrderManagement.Api.Infrastructure.ProblemDetailsFactory.CreateValidationProblemDetails(ModelState, HttpContext));
            }
            catch (ArgumentException ex)
            {
                ModelState.AddModelError(ex.ParamName ?? "", ex.Message);
                return BadRequest(OrderManagement.Api.Infrastructure.ProblemDetailsFactory.CreateValidationProblemDetails(ModelState, HttpContext));
            }
            catch (DbUpdateException dbEx)
            {
                // Translate unique constraint violation to 409 Conflict without leaking DB details
                if (dbEx.InnerException is PostgresException pg && pg.SqlState == "23505")
                {
                    return Conflict(new ProblemDetails
                    {
                        Type = "https://example.com/probs/conflict",
                        Title = "Conflict - duplicate SKU",
                        Status = StatusCodes.Status409Conflict,
                        Detail = "A product with the same SKU already exists."
                    });
                }

                _logger.LogError(dbEx, "Database update error while creating product (SKU: {Sku}).", req.Sku);
                var probDb = new ProblemDetails
                {
                    Type = "https://example.com/probs/internal-error",
                    Title = "An unexpected database error occurred.",
                    Status = StatusCodes.Status500InternalServerError,
                    Detail = "An unexpected error occurred while creating the product.",
                    Instance = HttpContext.TraceIdentifier
                };
                return StatusCode(StatusCodes.Status500InternalServerError, probDb);
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

            return CreatedAtAction(nameof(GetById), new { id = product.Id }, ProductMapping.MapToDto(product));
        }

        /// <summary>
        /// Soft-delete a product.
        /// </summary>
        [HttpDelete("{id:guid}")]
        public async Task<IActionResult> Delete([FromRoute] Guid id, CancellationToken cancellationToken = default)
        {
            var product = await _db.Products.FirstOrDefaultAsync(p => p.Id == id && p.Active, cancellationToken);
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

            // Mark soft-deleted and save
            product.SoftDelete();
            await _db.SaveChangesAsync(cancellationToken);

            return NoContent();
        }

        /// <summary>
        /// Replace editable details of a product.
        /// </summary>
        [HttpPut("{id:guid}")]
        public async Task<IActionResult> Replace([FromRoute] Guid id, [FromBody] UpdateProductRequest req)
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

            var product = await _db.Products.FirstOrDefaultAsync(p => p.Id == id && p.Active);
            if (product is null)
                return NotFound(new ProblemDetails { Type = "https://example.com/probs/not-found", Title = "Product not found", Status = StatusCodes.Status404NotFound });

            // Check if SKU conflicts with another product
            if (!string.Equals(product.Sku, req.Sku, StringComparison.OrdinalIgnoreCase)
                && await _db.Products.AnyAsync(p => p.Id != product.Id && string.Equals(p.Sku, req.Sku, StringComparison.OrdinalIgnoreCase)))
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

                await _db.SaveChangesAsync();
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
    }
}
