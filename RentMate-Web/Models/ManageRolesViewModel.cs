namespace RentMate.Models
{
    public class ManageRolesViewModel
    {
        public string UserId { get; set; }
        public string UserEmail { get; set; }
        public List<RoleSelection> Roles { get; set; } = new();
    }

}
