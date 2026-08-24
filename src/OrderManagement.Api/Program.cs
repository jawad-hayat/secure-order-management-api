using Microsoft.AspNetCore.Mvc;
using System;
using Microsoft.EntityFrameworkCore;
using OrderManagement.Api.Infrastructure;
using Npgsql.EntityFrameworkCore.PostgreSQL;

var builder = WebApplication.CreateBuilder(args);

// Add services to the container.

builder.Services.AddControllers();

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
builder.Services.AddSwaggerGen();
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
        c.SwaggerEndpoint("/openapi/v1.json", "OrderManagement.Api v1");
        c.RoutePrefix = "swagger";
    });
}

app.UseHttpsRedirection();

app.UseAuthorization();

app.MapControllers();

app.Run();