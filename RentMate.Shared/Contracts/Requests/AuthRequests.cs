namespace RentMate.Shared.Contracts.Requests;

/// <summary>
/// Login request credentials.
/// </summary>
public record LoginRequest(
    string Email,
    string Password,
    bool RememberMe = false
);

/// <summary>
/// Registration request.
/// </summary>
public record RegisterRequest
{
    public string Email { get; init; } = string.Empty;
    public string Password { get; init; } = string.Empty;
    public string ConfirmPassword { get; init; } = string.Empty;
    public string UserName { get; init; } = string.Empty;
    public string? FirstName { get; init; }
    public string? LastName { get; init; }
    public string? City { get; init; }
}

/// <summary>
/// Password change request.
/// </summary>
public record ChangePasswordRequest(
    string CurrentPassword,
    string NewPassword,
    string ConfirmNewPassword
);

/// <summary>
/// Password reset request (forgot password).
/// </summary>
public record ResetPasswordRequest(
    string Email,
    string Token,
    string NewPassword,
    string ConfirmPassword
);

/// <summary>
/// Request to send password reset email.
/// </summary>
public record ForgotPasswordRequest(string Email);
