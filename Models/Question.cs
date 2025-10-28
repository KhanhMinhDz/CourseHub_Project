using CourseManagement.Models;
using System.ComponentModel.DataAnnotations.Schema;
namespace CourseManagement.Models
{
    public class Question
    {
        public int Id { get; set; }
        public int AssignmentId { get; set; }
        public string Content { get; set; } = string.Empty;
        public string CorrectAnswers { get; set; } = string.Empty;
        public bool AllowMultiple { get; set; } = false;

        public string OptionsJson { get; set; } = "[]";

        public Assignment? Assignment { get; set; }

        [NotMapped]
        public List<string> Options
        {
            get => System.Text.Json.JsonSerializer.Deserialize<List<string>>(OptionsJson) ?? new List<string>();
            set => OptionsJson = System.Text.Json.JsonSerializer.Serialize(value);
        }
    }
}