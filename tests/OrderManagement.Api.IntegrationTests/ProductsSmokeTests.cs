using OrderManagement.Api.Contracts.Products;
using System.Net;
using System.Net.Http.Json;

namespace OrderManagement.Api.IntegrationTests;

public sealed class ProductsSmokeTests : IClassFixture<ApiWebApplicationFactory>
{
    private readonly HttpClient _client;

    public ProductsSmokeTests(ApiWebApplicationFactory factory)
    {
        _client = factory.CreateClient();
    }

    [Fact]
    public async Task GetProducts_ReturnsOkWithJson()
    {
        using var response = await _client.GetAsync("/api/products");

        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        Assert.Equal("application/json", response.Content.Headers.ContentType?.MediaType);

        var products = await response.Content.ReadFromJsonAsync<ProductDto[]>();

        Assert.NotNull(products);
    }
}
