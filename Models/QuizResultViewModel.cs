namespace CourseManagement.Models
{
    public class QuizResultViewModel
    {
        public int AssignmentId { get; set; }
        public string AssignmentTitle { get; set; } = string.Empty;
        public List<QuestionResult> QuestionResults { get; set; } = new();
        public int TotalQuestions { get; set; }
        public int CorrectAnswers { get; set; }
        public double Score { get; set; }
        public double MaxScore { get; set; } = 10.0;
    }

    public class QuestionResult
    {
        public int QuestionId { get; set; }
        public string Content { get; set; } = string.Empty;
        public List<string> Options { get; set; } = new();
        public List<string> CorrectAnswers { get; set; } = new();
        public List<string> StudentAnswers { get; set; } = new();
        public bool IsCorrect { get; set; }
        public double PointsPerQuestion { get; set; }
    }
}
