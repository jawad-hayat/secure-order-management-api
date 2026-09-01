# Security design — checkpoint 1

## Permission model

Capability | Anonymous | Customer | Admin
:--|:--:|:--:|:--:
Read products | Yes | Yes | Yes
Create, update, or soft-delete products | No | No | Yes
View or place own orders (next lesson) | No | Yes | Yes
View or manage every order (next lesson) | No | No | Yes

Notes: the table is unchanged from the requested model. Roles are coarse-grained authorization labels; fine-grained checks (e.g., order owner checks) are enforced at the resource level.

## Authentication vs Authorization

- Authentication (authN) proves the identity of a caller (who you are). Example: validating a bearer token or username/password.
- Authorization (authZ) decides what an authenticated identity is allowed to do (what you can access). Example: verifying a role or an owner claim before allowing an operation.

## Threats and mitigations

- Credential theft or weak passwords
  - Threat: stolen or easily guessed credentials allow attackers to impersonate users.
  - Mitigation: enforce strong password policies + rate-limited, multi-factor authentication (MFA) for privileged accounts; store only salted hashes (e.g., Argon2/BCrypt) and monitor/login alerts.

- Brute-force login attempts
  - Threat: automated attempts to guess passwords or tokens until one works.
  - Mitigation: implement account-level throttling and progressive delays, CAPTCHAs for repeated failures, and IP-based rate limits; lock or require additional verification after threshold.

- A stolen bearer token
  - Threat: a leaked or intercepted JWT / bearer token grants immediate API access until expiry.
  - Mitigation: use short-lived access tokens + refresh tokens with rotation and revocation; require HTTPS always, bind tokens to client or session when practical, and provide token revocation/blacklist for compromised tokens.

- Privilege escalation from Customer to Admin
  - Threat: bugs or misconfigurations allow customers to gain admin privileges.
  - Mitigation: apply least privilege, perform server-side role checks for sensitive actions, separate admin APIs or additional MFA for admin operations, and audit role changes with alerts.

- Cross-origin browser requests from an untrusted website (CSRF/CORS abuse)
  - Threat: a malicious site causes a user's browser to perform unwanted actions against the API.
  - Mitigation: require and validate same-site or anti-CSRF tokens for state-changing endpoints, configure strict CORS allow-lists, and require explicit authentication headers (not cookies) for APIs.

## Admin role ≠ ownership

- An Admin role grants elevated privileges but does not prove that an Admin is the legitimate owner of a given resource; ownership must be enforced by checking the resource's owner identifier against the authenticated subject for customer-scoped operations.

## Secret-handling rule

- Local developer secrets (store in dotnet user-secrets or OS-provided secure storage): local database connection strings (without committing passwords), OAuth client IDs (public), API keys for development/testing, and any credentials needed to run the app locally.
- Never commit to Git (repository or examples): production connection strings, database passwords, private keys, JWT signing secrets, OAuth client secrets, third-party API secrets, or any PII. Put production secrets in a dedicated secret store (Key Vault, AWS Secrets Manager, environment variables injected by the deployment platform) and reference them at runtime.

## Identity configuration decisions

- Password policy: require digits, lowercase and uppercase letters, at least one non-alphanumeric character, and a minimum length of 12. Rationale: this provides a stronger baseline against password guessing; privileged production accounts should also use MFA where available.
- Lockout policy: lock out after 5 failed attempts for 15 minutes. Rationale: reduces brute-force feasibility while allowing recovery for legitimate users.

These choices (strong password requirements and explicit lockout thresholds) are documented so reviewers can evaluate them as the application evolves.

## Role & Admin Seeding Decisions

- Standard Roles (`Admin`, `Customer`) are idempotently seeded at application startup if they do not already exist.
- Development Admin account:
  - Created ONLY in `Development` environment.
  - Credentials (`SeedAdmin:Password`) are sourced strictly from local User Secrets (`dotnet user-secrets set "SeedAdmin:Password" "..."`) or environment variables; passwords are NEVER hardcoded in source code or committed settings files.
  - If no password is provided in User Secrets, admin user creation is safely skipped and a helpful setup instruction is logged.

## Abuse Controls & Security Logging (Checkpoint 5)

### Rate Limiting Design
- **Endpoint**: `/api/auth/login` (decorated with `[EnableRateLimiting("login")]`).
- **Algorithm & Limit**: Fixed-window limiter with 5 permits per 1-minute window, queue limit 0.
- **Client Partitioning**: Partitioned by `HttpContext.Connection.RemoteIpAddress` (fallback: `"unknown_client"`).
- **Partitioning Limitations**:
  1. *Shared IP / NAT*: Users behind a common corporate gateway share the same public IP. An attacker on that network could exhaust the limit for legitimate users.
  2. *Reverse Proxy Spoofing*: If placed behind a reverse proxy (e.g. NGINX, Cloudflare) without `ForwardedHeadersMiddleware` (or without restrictively configured `KnownProxies`/`KnownNetworks`), attackers can manipulate `X-Forwarded-For` to bypass IP-based rate limiting.
  3. *Distributed Attacks*: Credential stuffing distributed across large botnets requires complementary account-level lockout mechanisms (which are active on `UserManager`).
- **Response**: Returns HTTP `429 Too Many Requests` with RFC 7807 Problem Details (`https://example.com/probs/rate-limited`).

### CORS Policy Design
- **Named Policy**: `"AngularDevClient"`.
- **Allowed Origins**: Strictly configured (default: `http://localhost:4200` via `Cors:AllowedOrigins`), never wildcard `*`.
- **Credentials**: Browser credentials are not enabled. The API uses bearer tokens in the `Authorization` header rather than cookie authentication, so this follows least privilege.

### Structured Security Logging
- **Events Logged**:
  - Registration attempts (success, failure reasons).
  - Login attempts (success, user-not-found, invalid password, account lockout).
  - Rate limiting rejections.
  - Product mutations (create, update, soft-delete with user identifier and product SKU/ID).
- **Data Sanitization**:
  - Passwords, access tokens, and Authorization headers are strictly excluded from log messages and format strings.
  - Contextual metadata (`ClientIp`, `UserName`, `UserId`, `TraceId`) is included in structured log templates for SIEM indexing.


### Answers of asked questions
1. What is the difference between authentication and authorization?
Authentication proves the caller's identity (who they are), for example by validating their password at login or validating a bearer token. Authorization decides what that authenticated caller is allowed to do, for example whether they may create a product.
2. When should this API return `401` versus `403`?
Return `401 Unauthorized` when credentials are missing or invalid. Return `403 Forbidden` when the caller is authenticated but does not have the required permission, such as a Customer trying to create a product.
3. Why must `UseAuthentication()` appear before `UseAuthorization()`?
`UseAuthentication()` validates the credentials and populates `HttpContext.User`. `UseAuthorization()` must run afterwards so it can evaluate that authenticated identity and its roles against the endpoint policy.
4. Why cannot a Customer role by itself prove that the customer owns order `123`?
A Customer role only proves that the caller has customer-level permissions; it does not identify which order they own. The API must load order `123` and verify that `order.CustomerId` equals the authenticated user's ID before allowing a customer-scoped action.


