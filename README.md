# OrderManagement API (local)

This workspace contains the OrderManagement API used for demo and development.

Prerequisites
- .NET 10 SDK
- PostgreSQL server (local or reachable) and psql client (for creating the local database)
- dotnet-ef tool (installed via dotnet tool restore using the provided manifest)

Local configuration (secrets)
- Use user-secrets to store the local connection string (do not commit passwords):

  cd src/OrderManagement.Api
  dotnet user-secrets init
  dotnet user-secrets set "ConnectionStrings:OrderManagement" "Host=localhost;Database=order_management_dev;Username=oms;Password=YOUR_PASSWORD"

- Alternatively copy `src/OrderManagement.Api/appsettings.Development.example.json` to
  `src/OrderManagement.Api/appsettings.Development.json` and fill in the password. Do NOT commit that file.

Database migrations
- Restore tools and packages:

  dotnet restore
  dotnet tool restore

- Create and apply EF migrations (project root):

  dotnet ef migrations add InitialCreate --project src/OrderManagement.Api --startup-project src/OrderManagement.Api --context OrderManagementDbContext
  dotnet ef database update --project src/OrderManagement.Api --startup-project src/OrderManagement.Api --context OrderManagementDbContext

Local verification steps

1. Build the project:

   dotnet build

2. Apply migrations to an empty local database (see commands above). Ensure the connection string points to an empty database named `order_management_dev` or create it first.

3. Run the API and create a product via Swagger UI (https://localhost:{port}/swagger). Restart the API process, then retrieve the created product by id — the product should still exist in the database.

4. Attempt to create the same normalized SKU twice. The second request must return `409 Conflict` with the project's Problem Details shape (type `https://example.com/probs/conflict`). The API handles races by translating DB unique constraint violations to 409.

5. Soft-delete a product (DELETE /api/products/{id}) and confirm that normal GET list and GET by id do NOT expose the soft-deleted product.

6. Request a paged, searched product list (GET /api/products?page=1&pageSize=20&search=...) and confirm behavior matches Lesson 1: validation, paging limits, and search behavior.

Notes
- The repository contains `src/OrderManagement.Api/appsettings.Development.example.json` as a safe template (no passwords). Use user-secrets or an untracked dev config for secrets.
- The API returns RFC 7807 Problem Details for validation, not-found, conflict and internal errors. Follow the documented response shapes when validating behavior.

To run locally:
- dotnet run --project src/OrderManagement.Api/OrderManagement.Api.csproj

Local interactive API docs (Swagger UI)

When running the API in the Development environment, an interactive Swagger UI is available at:

- https://localhost:7072/swagger/index.html
- http://localhost:5028/swagger/index.html

From that UI you can exercise and verify:

- GET /api/products?page=1&pageSize=20&search=phone
- GET /api/products/{id}
- POST /api/products
- PUT /api/products/{id}
- DELETE /api/products/{id}

Sample POST /api/products JSON request

```json
{
  "name": "Example Product",
  "sku": "EX-1000",
  "description": "Short description",
  "price": 19.99,
  "availableQuantity": 100
}
```

Open the Swagger UI and follow the verification steps above.

