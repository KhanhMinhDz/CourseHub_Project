using System.ComponentModel.DataAnnotations;

namespace CourseManagement.Models
{
    public class Enrollment
    {
        public int Id { get; set; }
        [Required]
        public int ClassRoomId { get; set; }
        public ClassRoom? ClassRoom { get; set; }

        [Required]
        public string StudentId { get; set; } = null!; // ApplicationUser.Id

        public DateTime EnrolledAt { get; set; } = DateTime.UtcNow;
    }
}
