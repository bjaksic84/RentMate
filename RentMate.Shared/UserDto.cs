namespace RentMate.Shared
{
    public class UserDto
    {
        public string Id { get; set; } = string.Empty;
        public string UserName { get; set; } = string.Empty;
        public string Email { get; set; } = string.Empty;
        // Dodaš lahko še npr. telefonsko, če jo rabiš v mobilni aplikaciji
        public ICollection<Item>? Items { get; set; }
        public ICollection<Rental>? RentalsAsRenter { get; set; }
        public ICollection<Rental>? RentalsAsOwner { get; set; } // ✅ NEW
    }
}