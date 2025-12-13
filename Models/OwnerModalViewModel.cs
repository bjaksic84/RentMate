namespace RentMate.Models;
public class OwnerModalViewModel {
    public string? Id { get; set; }
    public string? FirstName { get; set; }
    public string? LastName { get; set; }
    public string? City { get; set; }
    public string? Email { get; set; }
    public string? ProfilePictureUrl { get; set; }
    public double AverageRating { get; set; }
    public int ReviewCount { get; set; }
    public DateTime JoinDate { get; set; }
}