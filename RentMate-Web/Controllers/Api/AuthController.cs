using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Identity;
using Microsoft.AspNetCore.RateLimiting;
using Microsoft.IdentityModel.Tokens;
using System.Security.Claims;
using System.IdentityModel.Tokens.Jwt;
using System.Text;
using Microsoft.Extensions.Localization;
using RentMate.Models.Domain;
using RentMate.Models.ViewModels;
using RentMate.Models.Dto.Auth;
using RentMate.Shared.Contracts.Requests;
using RentMate.Shared.Contracts.Responses;


namespace RentMate.Controllers.Api
{
    [ApiController]
    [Route("api/[controller]")]
    public class AuthController : ControllerBase
    {
        private readonly UserManager<ApplicationUser> _userManager;
        private readonly RoleManager<IdentityRole> _roleManager;
        private readonly IConfiguration _configuration;
        private readonly IStringLocalizer<AuthController> _localizer;
        private readonly ILogger<AuthController> _logger;
        private readonly IWebHostEnvironment _environment;

        public AuthController(
            UserManager<ApplicationUser> userManager,
            RoleManager<IdentityRole> roleManager,
            IConfiguration configuration,
            IStringLocalizer<AuthController> localizer,
            ILogger<AuthController> logger,
            IWebHostEnvironment environment)
        {
            _userManager = userManager;
            _roleManager = roleManager;
            _configuration = configuration;
            _localizer = localizer;
            _logger = logger;
            _environment = environment;
        }

        [HttpPost("register")]
        [EnableRateLimiting("AuthPolicy")]
        public async Task<IActionResult> Register([FromBody] RegisterDto model)
        {
            if (!ModelState.IsValid) return BadRequest(ModelState);

            var user = new ApplicationUser
            {
                UserName = model.Email,
                Email = model.Email,
                FirstName = model.FirstName,
                LastName = model.LastName,
                City = model.City
            };

            var result = await _userManager.CreateAsync(user, model.Password);
            if (!result.Succeeded)
                return BadRequest(result.Errors);

            // In development, auto-confirm email since there's no mail server
            if (_environment.IsDevelopment())
            {
                var token = await _userManager.GenerateEmailConfirmationTokenAsync(user);
                await _userManager.ConfirmEmailAsync(user, token);
            }

            // Optionally add default role
            if (!await _roleManager.RoleExistsAsync("User"))
                await _roleManager.CreateAsync(new IdentityRole("User"));

            await _userManager.AddToRoleAsync(user, "User");

            return Ok(new { message = _localizer["Registration successful"].Value });
        }

        [HttpPost("login")]
        [EnableRateLimiting("AuthPolicy")]
        public async Task<ActionResult<AuthResponse>> Login([FromBody] LoginRequest model)
        {
            var user = await _userManager.FindByEmailAsync(model.Email);
            
            // Check if user is locked out
            if (user != null && await _userManager.IsLockedOutAsync(user))
            {
                _logger.LogWarning("Locked out user {Email} attempted to login from IP {IP}", 
                    model.Email, HttpContext.Connection.RemoteIpAddress);
                return Unauthorized(new { message = _localizer["Account is locked. Try again later."].Value });
            }
            
            if (user != null && await _userManager.CheckPasswordAsync(user, model.Password))
            {
                // Block deactivated users from obtaining new tokens
                if (user.IsDeactivated)
                {
                    return Unauthorized(new { message = _localizer["Account is deactivated."].Value });
                }

                // Reset failed access count on successful login
                await _userManager.ResetAccessFailedCountAsync(user);
                
                var token = await GenerateJwtTokenAsync(user);
                var roles = await _userManager.GetRolesAsync(user);
                
                _logger.LogInformation("User {Email} logged in successfully", model.Email);
                
                var userSummary = new UserSummary(
                    user.Id,
                    user.UserName ?? "",
                    user.Email,
                    user.FirstName,
                    user.LastName,
                    user.City,
                    user.ProfilePictureUrl
                );
                
                return Ok(AuthResponse.Successful(token, DateTime.UtcNow.AddDays(7), userSummary, roles.ToList()));
            }
            
            // Record failed login attempt
            if (user != null)
            {
                await _userManager.AccessFailedAsync(user);
            }
            
            _logger.LogWarning("Failed login attempt for {Email} from IP {IP}", 
                model.Email, HttpContext.Connection.RemoteIpAddress);

            return Ok(AuthResponse.Failed(_localizer["Invalid email or password."].Value));
        }

        private async Task<string> GenerateJwtTokenAsync(ApplicationUser user)
        {
            var jwtSection = _configuration.GetSection("Jwt");
            var key = jwtSection["Key"] ?? throw new InvalidOperationException("JWT Key not configured");
            var issuer = jwtSection["Issuer"];
            var audience = jwtSection["Audience"];
            

            var signingKey = new SymmetricSecurityKey(Encoding.UTF8.GetBytes(key));
            var creds = new SigningCredentials(signingKey, SecurityAlgorithms.HmacSha256);

            // basic claims
            var claims = new List<Claim>
            {
                new Claim(JwtRegisteredClaimNames.Sub, user.Id), 
                new Claim(JwtRegisteredClaimNames.Jti, Guid.NewGuid().ToString()),
                new Claim(ClaimTypes.NameIdentifier, user.Id),
                new Claim(ClaimTypes.Name, user.UserName ?? ""),
                new Claim(ClaimTypes.Email, user.Email ?? "")
            };

            // add role claims
            var roles = await _userManager.GetRolesAsync(user);
            foreach (var role in roles)
            {
                claims.Add(new Claim(ClaimTypes.Role, role));
            }

            // TODO: Implement refresh token rotation for better UX without compromising security
            // See: https://auth0.com/blog/refresh-tokens-what-are-they-and-when-to-use-them/
            var token = new JwtSecurityToken(
                issuer: issuer,
                audience: audience,
                claims: claims,
                expires: DateTime.UtcNow.AddDays(7), // 1 week token lifetime
                signingCredentials: creds);

            return new JwtSecurityTokenHandler().WriteToken(token);
        }
    }
}

