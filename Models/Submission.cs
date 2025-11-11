using System.ComponentModel.DataAnnotations;

namespace CourseManagement.Models
{
    public class Submission
    {
        public int Id { get; set; }
        [Required]
        public int AssignmentId { get; set; }
        public Assignment? Assignment { get; set; }

        [Required]
        public string StudentId { get; set; } = null!;

        public string? FilePath { get; set; }
        public string? Comments { get; set; }
        public DateTime SubmittedAt { get; set; } = DateTime.UtcNow;

        // Điểm số cho bài trắc nghiệm (null = bài tự luận hoặc chưa chấm)
        public double? Score { get; set; }
    }
}
