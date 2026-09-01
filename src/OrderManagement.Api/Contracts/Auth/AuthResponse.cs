namespace OrderManagement.Api.Contracts.Auth
{
    public sealed class AuthResponse
    {
        public string AccessToken { get; set; } = default!;
        public string TokenType { get; set; } = "Bearer";
        public long ExpiresIn { get; set; }
    }
}
