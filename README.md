# OrderManagement API (local)

This workspace contains the OrderManagement API used for demo and development.

Prerequisite:
- .NET 10 SDK

## Local interactive API docs (Swagger UI)

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
  "currency": "USD",
  "category": "default",
  "stock": 100,
  "tags": ["new","featured"]
}
```

Note:
- This API uses in-memory storage for development. All data is reset when the API restarts.

The API returns RFC 7807 Problem Details for validation and error responses. Validation failures use the type `https://example.com/probs/validation`.

To run:
- dotnet run --project src/OrderManagement.Api/OrderManagement.Api.csproj

Open the URL above and test endpoints directly from the Swagger UI.

