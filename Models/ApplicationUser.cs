using Microsoft.AspNetCore.Identity;

namespace RentMate.Models;

public class ApplicationUser : IdentityUser
{
    public string? FirstName { get; set; }
    public string? LastName { get; set; }
    public string? City { get; set; }

    public ICollection<Item>? Items { get; set; }
    public ICollection<Rental>? RentalsAsRenter { get; set; }
}

