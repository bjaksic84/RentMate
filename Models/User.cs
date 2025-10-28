using System.Collections.Generic;
using System.ComponentModel.DataAnnotations;

namespace RentMate.Models
{
    public class User
    {
        public int? Id { get; set; }

        [Required, StringLength(100)]
        public string? Username { get; set; }

        [Required, EmailAddress]
        public string? Email { get; set; }

        // We'll store a password hash; in production use ASP.NET Identity instead.
        public string? PasswordHash { get; set; }

        public ICollection<Item>? Items { get; set; }
        public ICollection<Rental>? RentalsAsRenter { get; set; }
    }
}
