using Microsoft.AspNetCore.Identity;

namespace CourseManagement.Data
{
    public class ApplicationUser : IdentityUser
    {
        // Additional profile fields
        public string? FullName { get; set; }
        public string? DisplayRole { get; set; }
    }
}
