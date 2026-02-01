using System.ComponentModel.DataAnnotations;

namespace RentMate.Models.Dto.Auth
{
    public class RegisterDto
    {
        [Required(ErrorMessage = "Email is required")]
        [EmailAddress(ErrorMessage = "Invalid email format")]
        [MaxLength(256, ErrorMessage = "Email cannot exceed 256 characters")]
        public string Email { get; set; } = null!;
        
        [Required(ErrorMessage = "Password is required")]
        [MinLength(8, ErrorMessage = "Password must be at least 8 characters")]
        [MaxLength(128, ErrorMessage = "Password cannot exceed 128 characters")]
        public string Password { get; set; } = null!;
        
        [MaxLength(100, ErrorMessage = "First name cannot exceed 100 characters")]
        [RegularExpression(@"^[a-zA-ZÀ-ÿ\s\-']+$", ErrorMessage = "First name contains invalid characters")]
        public string? FirstName { get; set; }
        
        [MaxLength(100, ErrorMessage = "Last name cannot exceed 100 characters")]
        [RegularExpression(@"^[a-zA-ZÀ-ÿ\s\-']+$", ErrorMessage = "Last name contains invalid characters")]
        public string? LastName { get; set; }
        
        [MaxLength(100, ErrorMessage = "City cannot exceed 100 characters")]
        [RegularExpression(@"^[a-zA-ZÀ-ÿ\s\-']+$", ErrorMessage = "City contains invalid characters")]
        public string? City { get; set; }
    }
}
