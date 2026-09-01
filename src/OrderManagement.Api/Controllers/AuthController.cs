using System;
using System.IdentityModel.Tokens.Jwt;
using System.Linq;
using System.Security.Claims;
using System.Text;
using System.Threading.Tasks;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Identity;
using Microsoft.Extensions.Configuration;
using Microsoft.IdentityModel.Tokens;
using OrderManagement.Api.Infrastructure.Identity;
using OrderManagement.Api.Contracts.Auth;

namespace OrderManagement.Api.Controllers
{
    [ApiController]
    [Route("api/auth")]
    public class AuthController : ControllerBase
    {
        private readonly UserManager<ApplicationUser> _userManager;
        private readonly RoleManager<IdentityRole<Guid>> _roleManager;
        private readonly IConfiguration _config;

        public AuthController(UserManager<ApplicationUser> userManager, RoleManager<IdentityRole<Guid>> roleManager, IConfiguration config)
        {
            _userManager = userManager;
            _roleManager = roleManager;
            _config = config;
        }

        [HttpPost("register")]
        public async Task<IActionResult> Register([FromBody] RegisterRequest req)
        {
            if (req is null)
                return BadRequest(new ProblemDetails { Title = "Request body is required" });

            var user = new ApplicationUser
            {
                UserName = req.UserName,
                Email = req.Email
            };

            var create = await _userManager.CreateAsync(user, req.Password);
            if (!create.Succeeded)
            {
                var v = new ValidationProblemDetails();
                foreach (var e in create.Errors)
                {
                    v.Errors.TryAdd(e.Code, new[] { e.Description });
                }
                v.Type = "https://example.com/probs/validation";
                v.Title = "One or more validation errors occurred.";
                v.Status = 400;
                return BadRequest(v);
            }

            // Ensure Customer role exists and assign
            const string customerRole = "Customer";
            if (!await _roleManager.RoleExistsAsync(customerRole))
            {
                await _roleManager.CreateAsync(new IdentityRole<Guid>(customerRole));
            }

            await _userManager.AddToRoleAsync(user, customerRole);

            return CreatedAtAction(null, null); // minimal response for registration
        }

        [HttpPost("login")]
        public async Task<IActionResult> Login([FromBody] LoginRequest req)
        {
            if (req is null)
                return BadRequest(new ProblemDetails { Title = "Request body is required" });

            var user = await _userManager.FindByNameAsync(req.UserName);
            if (user == null)
                return Unauthorized();

            var valid = await _userManager.CheckPasswordAsync(user, req.Password);
            if (!valid)
                return Unauthorized();

            // Read JWT settings from configuration
            var key = _config["Jwt:Key"];
            var issuer = _config["Jwt:Issuer"] ?? "OrderManagementApi";
            var audience = _config["Jwt:Audience"] ?? "OrderManagementApiClients";
            if (string.IsNullOrWhiteSpace(key))
                throw new InvalidOperationException("JWT signing key is not configured. Set Jwt:Key in user-secrets for development.");

            var signingKey = new SymmetricSecurityKey(Encoding.UTF8.GetBytes(key));
            var creds = new SigningCredentials(signingKey, SecurityAlgorithms.HmacSha256);

            var roles = await _userManager.GetRolesAsync(user);

            var claims = new[]
            {
                new Claim(JwtRegisteredClaimNames.Sub, user.Id.ToString()),
                new Claim(JwtRegisteredClaimNames.UniqueName, user.UserName ?? string.Empty),
                new Claim(JwtRegisteredClaimNames.Jti, Guid.NewGuid().ToString())
            }.ToList();

            foreach (var r in roles)
                claims.Add(new Claim(ClaimTypes.Role, r));

            var expiresMinutes = 15;
            if (int.TryParse(_config["Jwt:ExpiryMinutes"], out var m)) expiresMinutes = m;

            var now = DateTime.UtcNow;
            var token = new JwtSecurityToken(
                issuer: issuer,
                audience: audience,
                claims: claims,
                notBefore: now,
                expires: now.AddMinutes(expiresMinutes),
                signingCredentials: creds);

            var tokenStr = new JwtSecurityTokenHandler().WriteToken(token);

            var resp = new AuthResponse
            {
                AccessToken = tokenStr,
                ExpiresIn = (long)TimeSpan.FromMinutes(expiresMinutes).TotalSeconds
            };

            return Ok(resp);
        }
    }
}
