namespace RentMate.Models.ViewModels
{
    public class ManageRolesViewModel
    {
        public required string UserId { get; set; }
        public required string UserEmail { get; set; }
        public List<RoleSelection> Roles { get; set; } = new();
    }

}
