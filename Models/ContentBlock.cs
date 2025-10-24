using System.ComponentModel.DataAnnotations;

namespace CourseManagement.Models
{
    public class ContentBlock
    {
        public int Id { get; set; }
        [Required]
        public int ClassRoomId { get; set; }
        public ClassRoom? ClassRoom { get; set; }

        // HTML content
        public string Content { get; set; } = string.Empty;

        // Ordering for blocks
        public int Order { get; set; }

        public DateTime CreatedAt { get; set; } = DateTime.UtcNow;
        public DateTime? UpdatedAt { get; set; }
    }
}
