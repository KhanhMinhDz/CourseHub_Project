using System.ComponentModel.DataAnnotations;

namespace CourseManagement.Models
{
    public class Assignment
    {
        public int Id { get; set; }
        [Required]
        public int ClassRoomId { get; set; }
        public ClassRoom? ClassRoom { get; set; }

        [Required]
        public string Title { get; set; } = null!;
        public string? Description { get; set; }

        public DateTime? DueDate { get; set; }

        public DateTime CreatedAt { get; set; } = DateTime.UtcNow;
    }
}
