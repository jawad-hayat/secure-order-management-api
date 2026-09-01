using Microsoft.AspNetCore.Authentication.JwtBearer;
using Microsoft.AspNetCore.Identity;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.RateLimiting;
using Microsoft.EntityFrameworkCore;
using Microsoft.IdentityModel.Tokens;
using Microsoft.OpenApi;
using Npgsql.EntityFrameworkCore.PostgreSQL;
using OrderManagement.Api.Infrastructure;
using System;
using System.Text;
using System.Collections.Generic;
using System.Threading.RateLimiting;
using Swashbuckle.AspNetCore.Filters;

var builder = WebApplication.CreateBuilder(args);

// Add services to the container.

builder.Services.AddControllers();

// Configure CORS: narrow named policy for Angular dev origin (never combine AllowAnyOrigin with credentials)
const string AngularDevCorsPolicy = "AngularDevClient";
var allowedOrigins = builder.Configuration.GetSection("Cors:AllowedOrigins").Get<string[]>() 
    ?? new[] { "http://localhost:4200" };

builder.Services.AddCors(options =>
{
    options.AddPolicy(AngularDevCorsPolicy, policy =>
    {
        policy.WithOrigins(allowedOrigins)
              .AllowAnyMethod()
              .AllowAnyHeader();
    });
});

// Configure Rate Limiting: named policy "login" to protect against brute-force and credential stuffing
builder.Services.AddRateLimiter(options =>
{
    options.RejectionStatusCode = StatusCodes.Status429TooManyRequests;
    options.OnRejected = async (context, token) =>
    {
        var clientIp = context.HttpContext.Connection.RemoteIpAddress?.ToString() ?? "unknown_client";
        var logger = context.HttpContext.RequestServices.GetRequiredService<ILogger<Program>>();
        logger.LogWarning("Security event: Rate limit exceeded on {Path} from ClientIp: {ClientIp}. TraceId: {TraceId}",
            context.HttpContext.Request.Path, clientIp, context.HttpContext.TraceIdentifier);

        context.HttpContext.Response.ContentType = "application/problem+json";
        var problemDetails = new ProblemDetails
        {
            Type = "https://example.com/probs/rate-limited",
            Title = "Too Many Requests",
            Status = StatusCodes.Status429TooManyRequests,
            Detail = "Rate limit exceeded. Please wait before retrying.",
            Instance = context.HttpContext.TraceIdentifier
        };
        await context.HttpContext.Response.WriteAsJsonAsync(problemDetails, cancellationToken: token);
    };

    options.AddPolicy("login", httpContext =>
    {
        // Partition anonymous login requests by client IP
        var clientIp = httpContext.Connection.RemoteIpAddress?.ToString() ?? "unknown_client";
        return RateLimitPartition.GetFixedWindowLimiter(
            partitionKey: clientIp,
            factory: _ => new FixedWindowRateLimiterOptions
            {
                PermitLimit = 5,
                Window = TimeSpan.FromMinutes(1),
                QueueLimit = 0,
                AutoReplenishment = true
            });
    });
});

// Customize automatic InvalidModelState responses to match our RFC7807 validation shape
builder.Services.Configure<Microsoft.AspNetCore.Mvc.ApiBehaviorOptions>(options =>
{
    options.InvalidModelStateResponseFactory = context =>
    {
        var details = OrderManagement.Api.Infrastructure.ProblemDetailsFactory.CreateValidationProblemDetails(context.ModelState, context.HttpContext);
        return new BadRequestObjectResult(details)
        {
            ContentTypes = { "application/problem+json" }
        };
    };
});

// Learn more about configuring OpenAPI at https://aka.ms/aspnet/openapi
builder.Services.AddOpenApi();
builder.Services.AddEndpointsApiExplorer();
builder.Services.AddSwaggerGen(options =>
{
    options.SwaggerDoc("v1", new OpenApiInfo
    {
        Title = "OrderManagement.Api",
        Version = "v1",
        Description = "Order Management API with JWT Authentication"
    });

    options.AddSecurityDefinition("Bearer", new OpenApiSecurityScheme
    {
        Name = "Authorization",
        Type = SecuritySchemeType.Http,
        Scheme = "Bearer",
        BearerFormat = "JWT",
        In = ParameterLocation.Header,
        Description = "JWT Authorization header using the Bearer scheme. Enter your token below."
    });

    options.AddSecurityRequirement(doc => new OpenApiSecurityRequirement
    {
        {
            new OpenApiSecuritySchemeReference("Bearer", doc),
            new List<string>()
        }
    });
});
// Configure EF Core with PostgreSQL provider. Connection string must be provided via user secrets
// or an untracked appsettings.Development.json under "ConnectionStrings:OrderManagement".
var orderConn = builder.Configuration.GetConnectionString("OrderManagement");
if (string.IsNullOrWhiteSpace(orderConn))
{
    // Fail fast with a clear error so developers know how to configure their local environment.
    throw new InvalidOperationException("Missing required connection string 'ConnectionStrings:OrderManagement'.\n" +
                                        "Set it using 'dotnet user-secrets set \"ConnectionStrings:OrderManagement\" \"Host=localhost;Database=order_management_dev;Username=oms;Password=YOUR_PASSWORD\"'\n" +
                                        "or create an untracked src/OrderManagement.Api/appsettings.Development.json with the ConnectionStrings section.");
}

// Register OrderManagementDbContext with PostgreSQL provider
builder.Services.AddDbContext<OrderManagementDbContext>(opts => opts.UseNpgsql(orderConn));

// Configure ASP.NET Core Identity with Guid keys and EF Core stores
builder.Services.AddIdentity<OrderManagement.Api.Infrastructure.Identity.ApplicationUser, Microsoft.AspNetCore.Identity.IdentityRole<Guid>>(options =>
{
    // Password requirements - deliberate choices documented in docs/security-design.md
    options.Password.RequireDigit = true;
    options.Password.RequireLowercase = true;
    options.Password.RequireUppercase = true;
    options.Password.RequireNonAlphanumeric = true;
    options.Password.RequiredLength = 12;

    // Lockout settings
    options.Lockout.MaxFailedAccessAttempts = 5;
    options.Lockout.DefaultLockoutTimeSpan = TimeSpan.FromMinutes(15);
    options.Lockout.AllowedForNewUsers = true;

    // User settings
    options.User.RequireUniqueEmail = false; // email uniqueness optional for now
})
    .AddEntityFrameworkStores<OrderManagementDbContext>()
    .AddDefaultTokenProviders();

// Configure JWT authentication
var jwtKey = builder.Configuration["Jwt:Key"];
var jwtIssuer = builder.Configuration["Jwt:Issuer"] ?? "OrderManagementApi";
var jwtAudience = builder.Configuration["Jwt:Audience"] ?? "OrderManagementApiClients";
if (string.IsNullOrWhiteSpace(jwtKey))
{
    // Fail fast in development so developers configure user-secrets
    throw new InvalidOperationException("Missing JWT signing key. Set Jwt:Key in user-secrets before running the app.");
}

builder.Services.AddAuthentication(options =>
{
    options.DefaultAuthenticateScheme = JwtBearerDefaults.AuthenticationScheme;
    options.DefaultChallengeScheme = JwtBearerDefaults.AuthenticationScheme;
})
    .AddJwtBearer(options =>
    {
        options.TokenValidationParameters = new TokenValidationParameters
        {
            ValidateIssuer = true,
            ValidateAudience = true,
            ValidateLifetime = true,
            ValidateIssuerSigningKey = true,
            ValidIssuer = jwtIssuer,
            ValidAudience = jwtAudience,
            IssuerSigningKey = new SymmetricSecurityKey(Encoding.UTF8.GetBytes(jwtKey))
        };
    });

// Configure Authorization policies
builder.Services.AddAuthorization(options =>
{
    options.AddPolicy("Admin", policy => policy.RequireRole("Admin"));
    options.AddPolicy("Customer", policy => policy.RequireRole("Customer"));
});

// Swagger JWT security configured above in AddSwaggerGen

var app = builder.Build();

// Configure the HTTP request pipeline.
if (app.Environment.IsDevelopment())
{
    // expose OpenAPI document
    app.MapOpenApi();

    // enable interactive Swagger UI for testing every endpoint from the browser
    app.UseSwagger();
    app.UseSwaggerUI(c =>
    {
        c.SwaggerEndpoint("/swagger/v1/swagger.json", "OrderManagement.Api v1");
        c.RoutePrefix = "swagger";
    });
}

app.UseHttpsRedirection();

// Enable CORS for allowed origins before authentication/authorization
app.UseCors(AngularDevCorsPolicy);

// Enable Rate Limiting
app.UseRateLimiter();

// Authentication must be called before Authorization
app.UseAuthentication();
app.UseAuthorization();

app.MapControllers();

// Seed identity roles and optional development admin account
await OrderManagement.Api.Infrastructure.Identity.DbInitializer.SeedIdentityAsync(
    app.Services,
    app.Configuration,
    app.Environment,
    app.Logger);

app.Run();
