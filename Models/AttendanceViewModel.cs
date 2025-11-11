namespace CourseManagement.Models
{
    public class AttendanceViewModel
    {
        public int SessionId { get; set; }
        public DateTime CreatedAt { get; set; }
        public DateTime CloseAt { get; set; }
        public bool IsActive { get; set; }
        public int TotalStudents { get; set; }
        public int PresentCount { get; set; }
        public int AbsentCount { get; set; }
    }

    public class StudentAttendanceViewModel
    {
        public string StudentId { get; set; } = string.Empty;
        public string StudentName { get; set; } = string.Empty;
        public string Email { get; set; } = string.Empty;
        public List<AttendanceStatus> AttendanceStatuses { get; set; } = new List<AttendanceStatus>();
    }

    public class AttendanceStatus
    {
        public int SessionId { get; set; }
        public bool IsPresent { get; set; }
        public DateTime? AttendedAt { get; set; }
        public DateTime SessionDate { get; set; }
    }

    public class CreateAttendanceViewModel
    {
        public int ClassRoomId { get; set; }
        public string ClassName { get; set; } = string.Empty;
        public int DurationMinutes { get; set; } = 15; // Mặc định 15 phút
    }
}
