# Secure Order Management API - Contract

This document records the public contract for the Products endpoints and key validation and design decisions.

## Base route

All endpoints are rooted at: `/api/products`

## Endpoints (summary)

- GET /api/products?page=1&pageSize=20&search=phone
  - Intent: List active products with optional name/SKU search and pagination.
  - Success: 200 OK with a JSON array of product DTOs.
  - Failure: 400 Bad Request (ValidationProblemDetails) for invalid paging parameters or out-of-range page.

- GET /api/products/{id}
  - Intent: Return a single active product by id.
  - Success: 200 OK with product DTO.
  - Failure: 404 Not Found (ProblemDetails) when the product does not exist or is inactive.

- POST /api/products
  - Intent: Create a new product.
  - Success: 201 Created with Location header and created product DTO in the response body.
  - Failure: 400 Bad Request (ValidationProblemDetails) for invalid input; 409 Conflict when a product with the same SKU already exists; 500 ProblemDetails for unexpected errors.

- PUT /api/products/{id}
  - Intent: Replace editable product details (name, sku, description, price, availableQuantity).
  - Success: 204 No Content.
  - Failure: 400 Bad Request (ValidationProblemDetails) for invalid input; 404 Not Found when product does not exist; 409 Conflict for SKU collisions; 500 ProblemDetails for unexpected errors.

- DELETE /api/products/{id}
  - Intent: Deactivate (soft-delete) the product.
  - Success: 204 No Content.
  - Failure: 404 Not Found (ProblemDetails) when the product does not exist.

## Validation rules

- name: required, 3–120 characters.
- sku: required, 3–50 characters. The API trims and normalizes SKU to uppercase before validation and persistence.
- description: optional, maximum 1,000 characters.
- price: must be greater than 0 and have no more than two decimal places.
- availableQuantity: integer between 0 and 100,000 inclusive.
- page: integer, at least 1.
- pageSize: integer, between 1 and 100 inclusive.

Validation failures are returned as RFC 7807 Problem Details using ValidationProblemDetails, for example:

{
  "type": "https://example.com/probs/validation",
  "title": "One or more validation errors occurred.",
  "status": 400,
  "detail": "See the errors property for details.",
  "errors": {
	"name": ["Name is required and must be 3–120 characters."],
	"sku": ["SKU is required, 3–50 characters; will be normalized to uppercase."]
  }
}

Use stable `type` URIs for machine-readable classification of problems and include field-level `errors` for client guidance.

## Decisions

1) Why decimal is used for Price

- Rationale: `decimal` is a base-10 fixed-point numeric type designed for financial and monetary calculations. It avoids the rounding and representation errors common with binary floating-point types (float/double), which can cause subtle and unacceptable inaccuracies in money computations (e.g., totaling, tax calculations). Using `decimal` yields predictable rounding behavior and exact decimal fractions for two-digit currency amounts.
2) Why delete = deactivate (soft delete)

- Rationale: Soft-deleting (marking a product inactive) preserves historical integrity and referential stability. Orders, invoices, analytics, and audit logs reference products; permanently removing a product breaks those historical records or requires complex cascading deletes and data migration. Soft deletes enable recovery/undo, safer UX, consistent foreign-key relationships, and simpler auditing and compliance.