namespace CourseManagement.Models
{
    public class StudentEnrollmentViewModel
    {
        public string StudentId { get; set; } = string.Empty;
        public string StudentName { get; set; } = string.Empty;
        public string Email { get; set; } = string.Empty;
        public string ClassName { get; set; } = string.Empty;
        public int ClassRoomId { get; set; }
        public DateTime EnrolledAt { get; set; }
    }
}
