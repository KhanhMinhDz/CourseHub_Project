using Microsoft.AspNetCore.Identity;

namespace CourseManagement.Data
{
    public class ApplicationUser : IdentityUser
    {
        // Additional profile fields
        public string? FullName { get; set; }

        // Extended profile fields
        public string? Avatar { get; set; } // Đường dẫn file ảnh đại diện
        public string? Address { get; set; } // Địa chỉ
        public string? Gender { get; set; } // Giới tính: Nam, Nữ, Khác
        // Note: PhoneNumber đã có sẵn trong IdentityUser
    }
}
