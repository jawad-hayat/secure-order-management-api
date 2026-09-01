# OrderManagement API (local)

This workspace contains the OrderManagement API used for demo and development.

Prerequisites
- .NET 10 SDK
- PostgreSQL server (local or reachable) and psql client (for creating the local database)
- EF Core CLI tool (`dotnet-ef`) installed globally

Local configuration (secrets)
- Use user-secrets to store local configuration (do not commit secrets or passwords):

  cd src/OrderManagement.Api
  dotnet user-secrets set "ConnectionStrings:OrderManagement" "Host=localhost;Database=order_management_dev;Username=oms;Password=YOUR_PASSWORD"
  dotnet user-secrets set "Jwt:Key" "AStrongJwtSigningKeyForLocalDevelopmentOnly12345!"
  dotnet user-secrets set "SeedAdmin:Password" "AdminPassword123!"

- The project already has a User Secrets ID. Do not put real connection strings, signing keys, or passwords in any tracked configuration file.

Database migrations
- Restore packages:

  dotnet restore

- Apply the committed migration to the local database (project root):

  dotnet ef database update --project src/OrderManagement.Api --startup-project src/OrderManagement.Api --context OrderManagementDbContext

Local verification steps

1. Build the project:

   dotnet build

2. Apply migrations to an empty local database (see commands above). Ensure the connection string points to an empty database named `order_management_dev` or create it first.

3. Run the API:

   dotnet run --project src/OrderManagement.Api/OrderManagement.Api.csproj

4. **Security & Authorization Verification** (via Swagger UI or `OrderManagement.Api.http`):
   - **401 Unauthorized**: Send `POST /api/products` anonymously (without `Authorization` header). Confirm response is `401` with `WWW-Authenticate: Bearer` challenge header.
   - **403 Forbidden**: Register and log in as a Customer (`/api/auth/register`, `/api/auth/login`), attach the Bearer token, and send `POST /api/products`. Confirm response is `403 Forbidden`.
   - **201 Created**: Log in with the seeded Admin user (`admin` / `AdminPassword123!`), attach the Admin Bearer token, and send `POST /api/products`. Confirm response is `201 Created`.

5. **Abuse Controls Verification**:
   - **Rate Limiting (429 Too Many Requests)**:
     Send 6 rapid `POST /api/auth/login` requests from the same client IP. The 6th request will be rejected with HTTP `429 Too Many Requests` and an RFC 7807 Problem Details payload (`type: https://example.com/probs/rate-limited`).
   - **CORS Policy (Origin Isolation)**:
     - Preflight `OPTIONS /api/products` with `Origin: http://localhost:4200` &rarr; returns `Access-Control-Allow-Origin: http://localhost:4200` and `Access-Control-Allow-Credentials: true`.
     - Preflight `OPTIONS /api/products` with an untrusted origin (`Origin: http://malicious.example.com`) &rarr; rejected without CORS allow headers.
   - **Structured Security Logging**:
     Review server console output. All authentication attempts, lockout events, rate-limit rejections, and administrative mutations emit structured logs (`ClientIp`, `UserName`, `UserId`, `TraceId`). **Passwords, access tokens, and Authorization headers are never logged.**

6. Attempt to create the same normalized SKU twice. The second request must return `409 Conflict` with the project's Problem Details shape (type `https://example.com/probs/conflict`). The API handles races by translating DB unique constraint violations to 409.

7. Soft-delete a product (DELETE /api/products/{id}) with the Admin token and confirm that normal GET list and GET by id do NOT expose the soft-deleted product.

8. Request a paged, searched product list (GET /api/products?page=1&pageSize=20&search=...) and confirm behavior matches validation, paging limits, and search behavior.

Abuse controls & design decisions

- **Rate Limiting**:
  - Policy `"login"` protects `/api/auth/login` using a fixed-window limiter of 5 requests per 1-minute window.
  - *Partitioning client identifier*: Requests are partitioned by `RemoteIpAddress`.
  - *Documented limitations*:
    1. **NAT / Shared Networks**: Multiple users behind a corporate gateway or NAT share the same public IP; one misbehaving client may temporarily throttle legitimate users on that network.
    2. **Reverse Proxy / Spoofing**: In production behind a reverse proxy (e.g. NGINX, Cloudflare, ALB), `ForwardedHeadersMiddleware` with configured `KnownProxies` / `KnownNetworks` must be used. Relying on unverified `X-Forwarded-For` allows attackers to bypass IP limits via spoofed headers.
- **CORS Policy**:
  - Narrow named policy `"AngularDevClient"` explicitly permits `http://localhost:4200` (configurable via `Cors:AllowedOrigins`).
  - This API uses bearer tokens in the `Authorization` header, not browser cookies, so CORS credentials are not enabled.
- **Security Logging**:
  - Logs structured events with machine-readable placeholders for SIEM integration.
  - Strict sanitization: passwords, access tokens, and Authorization headers are completely omitted from logs.

Notes
- Use User Secrets for local connection strings, JWT keys, and admin passwords. The safe configuration template contains no real credentials.
- The API returns RFC 7807 Problem Details for validation, not-found, conflict, rate-limiting, and internal errors. Follow the documented response shapes when validating behavior.

To run locally:
- dotnet run --project src/OrderManagement.Api/OrderManagement.Api.csproj

Local interactive API docs (Swagger UI)

When running the API in the Development environment, an interactive Swagger UI is available at:

- https://localhost:7072/swagger/index.html
- http://localhost:5028/swagger/index.html

From that UI you can exercise and verify all endpoints with the interactive **Authorize** button.

Sample POST /api/products JSON request:

```json
{
  "name": "Example Product",
  "sku": "EX-1000",
  "description": "Short description",
  "price": 19.99,
  "availableQuantity": 100
}
```

