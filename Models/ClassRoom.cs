using System.ComponentModel.DataAnnotations;

namespace CourseManagement.Models
{
    public class ClassRoom
    {
        public int Id { get; set; }

        [Required]
        public string Title { get; set; } = null!;

        public string? Description { get; set; }

        // Password students must enter to enroll
        public string? EnrollmentPasswordHash { get; set; }

        // FK to instructor (ApplicationUser.Id)
        public string? InstructorId { get; set; }
        public virtual ICollection<Assignment>? Assignments { get; set; }
        public virtual ICollection<ContentBlock>? ContentBlocks { get; set; }
    }
}
