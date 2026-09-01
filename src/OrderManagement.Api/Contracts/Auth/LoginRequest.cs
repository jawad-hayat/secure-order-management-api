namespace OrderManagement.Api.Contracts.Auth
{
    public sealed class LoginRequest
    {
        public string UserName { get; set; } = default!;
        public string Password { get; set; } = default!;
    }
}
