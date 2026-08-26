# Persistence decisions — short rationale

1) Why keep a unique DB index when the API already checks for duplicate SKU?

- API-level checks are advisory and vulnerable to race conditions (concurrent requests, multiple app instances). The database is the single source of truth; a unique index enforces integrity atomically. The index also improves lookup performance for SKU-based queries.

2) Why use decimal / numeric(18,2) for Price?

- Monetary values require exact decimal arithmetic to avoid rounding errors inherent to binary floating-point. C# `decimal` maps cleanly to PostgreSQL `numeric(18,2)`, providing sufficient precision and a fixed 2-decimal scale for currency amounts.

3) Why use AsNoTracking() and projection for the list endpoint?

- `AsNoTracking()` avoids EF Core change-tracking overhead for read-only queries, reducing memory and CPU usage. Projecting directly to `ProductDto` makes the database do filtering, paging and column selection, reducing data transferred and allocations (no full entity materialization), which improves latency and scalability.

4) What must change before applying migrations in production?

- Use a production-grade connection string and secure secret storage (Key Vault, environment variables, or managed identity) — never commit production credentials. Review and test migrations in staging; ensure backups exist and run migrations during a controlled deployment window. Grant the migration executor minimal required privileges, review any destructive schema changes, and consider generating idempotent or provider-specific SQL for DBAs to apply if automatic migrations are not acceptable.
