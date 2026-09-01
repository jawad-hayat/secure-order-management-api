namespace OrderManagement.Api.Contracts.Auth
{
    public sealed class RegisterRequest
    {
        public string UserName { get; set; } = default!;
        public string? Email { get; set; }
        public string Password { get; set; } = default!;
    }
}
