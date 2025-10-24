namespace CourseManagement.Models
{
    public class EnrollmentViewModel
    {
        public int Id { get; set; }
        public string StudentId { get; set; } = null!;
        public string? StudentName { get; set; }
        public System.DateTime EnrolledAt { get; set; }
    }
}
