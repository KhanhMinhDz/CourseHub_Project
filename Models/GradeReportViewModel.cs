namespace CourseManagement.Models
{
    public class StudentGradeReportViewModel
    {
        public string StudentId { get; set; } = string.Empty;
        public string StudentName { get; set; } = string.Empty;
        public string Email { get; set; } = string.Empty;
        public Dictionary<int, AssignmentGrade> AssignmentGrades { get; set; } = new();
        public double AverageScore { get; set; }
        public List<AttendanceStatus> AttendanceStatuses { get; set; } = new();
    }

    public class AssignmentGrade
    {
        public int AssignmentId { get; set; }
        public string AssignmentTitle { get; set; } = string.Empty;
        public bool IsQuiz { get; set; } // true = trắc nghiệm, false = tự luận
        public double? Score { get; set; } // Điểm (null = chưa nộp hoặc chưa chấm)
        public int? SubmissionId { get; set; } // ID của submission để update điểm
        public bool HasSubmitted { get; set; }
    }

    public class GradeReportViewModel
    {
        public List<StudentGradeReportViewModel> Students { get; set; } = new();
        public List<Assignment> Assignments { get; set; } = new();
    }
}
