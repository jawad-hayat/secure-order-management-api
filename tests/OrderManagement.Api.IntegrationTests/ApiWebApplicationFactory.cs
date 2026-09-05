using Microsoft.AspNetCore.Hosting;
using Microsoft.AspNetCore.Mvc.Testing;
using Microsoft.Extensions.Configuration;

namespace OrderManagement.Api.IntegrationTests;

public sealed class ApiWebApplicationFactory : WebApplicationFactory<Program>
{
    protected override void ConfigureWebHost(IWebHostBuilder builder)
    {
        const string connectionStringTemplate =
            "Host=localhost;Database=order_management_test;Username=oms;Password=";

        var password = Environment.GetEnvironmentVariable(
            "ORDER_MANAGEMENT_TEST_DB_PASSWORD");

        if (string.IsNullOrWhiteSpace(password))
        {
            throw new InvalidOperationException(
                "Set ORDER_MANAGEMENT_TEST_DB_PASSWORD before running integration tests.");
        }

        var testConnectionString = connectionStringTemplate + password;

        const string jwtKey =
            "integration-test-signing-key-must-be-at-least-32-characters";

        const string jwtIssuer = "OrderManagementApi.Tests";
        const string jwtAudience = "OrderManagementApi.IntegrationTests";

        builder.UseEnvironment("Testing");

        // Explicit host settings used by WebApplicationFactory.
        builder.UseSetting(
            "ConnectionStrings:OrderManagement",
            testConnectionString);

        builder.UseSetting("Jwt:Key", jwtKey);
        builder.UseSetting("Jwt:Issuer", jwtIssuer);
        builder.UseSetting("Jwt:Audience", jwtAudience);

        // Application configuration overrides.
        builder.ConfigureAppConfiguration((_, configuration) =>
        {
            configuration.AddInMemoryCollection(new Dictionary<string, string?>
            {
                ["ConnectionStrings:OrderManagement"] = testConnectionString,
                ["Jwt:Key"] = jwtKey,
                ["Jwt:Issuer"] = jwtIssuer,
                ["Jwt:Audience"] = jwtAudience
            });
        });
    }
}