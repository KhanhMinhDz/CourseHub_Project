namespace CourseManagement.Models
{
    public class ClassDisplayViewModel
    {
        public int Id { get; set; }
        public string Title { get; set; } = string.Empty;
        public string Description { get; set; } = string.Empty;
        public string InstructorName { get; set; } = string.Empty;
        public int StudentCount { get; set; }
    }
}
