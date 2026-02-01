using Microsoft.AspNetCore.Identity;
using Microsoft.AspNetCore.Mvc;
using Microsoft.Extensions.Localization;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Authentication.JwtBearer;
using Microsoft.AspNetCore.RateLimiting;
using RentMate.Models.Domain;
using RentMate.Models.ViewModels;

[Route("api/[controller]")]
[ApiController]
[EnableRateLimiting("AuthPolicy")]
public class AccountApiController : ControllerBase
{
    private readonly SignInManager<ApplicationUser> _signInManager;
    private readonly UserManager<ApplicationUser> _userManager;
    private readonly IStringLocalizer<AccountApiController> _localizer;

    public AccountApiController(
        SignInManager<ApplicationUser> signInManager, 
        UserManager<ApplicationUser> userManager,
        IStringLocalizer<AccountApiController> localizer)
    {
        _signInManager = signInManager;
        _userManager = userManager;
        _localizer = localizer;
    }

    [HttpPost("login")]
    public async Task<IActionResult> Login([FromBody] LoginModel model)
    {
        if (string.IsNullOrEmpty(model.Email) || string.IsNullOrEmpty(model.Password))
        {
            return BadRequest(_localizer["Email and password are required."].Value);
        }
        
        // Enable lockout on failed attempts for brute force protection
        var result = await _signInManager.PasswordSignInAsync(model.Email, model.Password, isPersistent: true, lockoutOnFailure: true);
        if (result.Succeeded)
        {
            var user = await _userManager.FindByEmailAsync(model.Email);
            if (user == null)
            {
                return Unauthorized(_localizer["User not found."].Value);
            }
            return Ok(new { UserId = user.Id, UserName = user.UserName, Email = user.Email });
        }
        
        if (result.IsLockedOut)
        {
            return Unauthorized(_localizer["Account is locked. Try again later."].Value);
        }
        
        return Unauthorized(_localizer["Invalid login credentials."].Value);
    }

    [HttpGet("currentuser")]
    public async Task<IActionResult> GetCurrentUser()
    {
        var user = await _userManager.GetUserAsync(User);
        if (user == null) return Unauthorized();
        return Ok(new { user.Id, user.UserName, user.Email });
    }

    [HttpPost("logout")]
    public async Task<IActionResult> Logout()
    {
        await _signInManager.SignOutAsync();
        return Ok();
    }
    [Authorize(AuthenticationSchemes = JwtBearerDefaults.AuthenticationScheme)]
    [HttpPut("update-profile")]
    public async Task<IActionResult> UpdateProfile([FromBody] UpdateProfileModel model)
    {
        var user = await _userManager.GetUserAsync(User);
        if (user == null) return Unauthorized();

        user.Email = model.Email;
        user.UserName = model.Email; // UserName je običajno email
        user.City = model.City;
        user.ProfilePictureUrl = model.ProfilePictureUrl; // Predpostavljam, da si to dodal v ApplicationUser

        var result = await _userManager.UpdateAsync(user);
        if (result.Succeeded)
        {
            return Ok(new { user.Id, user.Email, user.City, user.ProfilePictureUrl });
        }
        return BadRequest(result.Errors);
    }

    [Authorize(AuthenticationSchemes = JwtBearerDefaults.AuthenticationScheme)]
    [HttpPost("change-password")]
    public async Task<IActionResult> ChangePassword([FromBody] ChangePasswordModel model)
    {
        var user = await _userManager.GetUserAsync(User);
        if (user == null) return Unauthorized();

        var result = await _userManager.ChangePasswordAsync(user, model.OldPassword, model.NewPassword);
        if (result.Succeeded) return Ok();

        return BadRequest(result.Errors.FirstOrDefault()?.Description ?? "Napaka pri menjavi gesla.");
    }

}

public class LoginModel { public string? Email { get; set; } public string? Password { get; set; } }
// Modeli za prenos podatkov (DTO)
public class UpdateProfileModel { 
    public required string Email { get; set; } 
    public required string City { get; set; } 
    public string? ProfilePictureUrl { get; set; }
}

public class ChangePasswordModel { 
    public required string OldPassword { get; set; } 
    public required string NewPassword { get; set; } 
}

