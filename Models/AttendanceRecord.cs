using System.ComponentModel.DataAnnotations;
using CourseManagement.Data;

namespace CourseManagement.Models
{
    // Bản ghi điểm danh của từng học viên
    public class AttendanceRecord
    {
        public int Id { get; set; }

        [Required]
        public int AttendanceSessionId { get; set; }

        public AttendanceSession? AttendanceSession { get; set; }

        [Required]
        public string StudentId { get; set; } = string.Empty;

        public ApplicationUser? Student { get; set; }

        public bool IsPresent { get; set; } = false; // true = có mặt, false = vắng

        public DateTime? AttendedAt { get; set; } // Thời điểm điểm danh (null nếu vắng)
    }
}
