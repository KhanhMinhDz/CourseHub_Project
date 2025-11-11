using System.ComponentModel.DataAnnotations;

namespace CourseManagement.Models
{
    // Phiên điểm danh do giảng viên tạo
    public class AttendanceSession
    {
        public int Id { get; set; }

        [Required]
        public int ClassRoomId { get; set; }

        public ClassRoom? ClassRoom { get; set; }

        [Required]
        public DateTime CreatedAt { get; set; } = DateTime.Now;

        [Required]
        public DateTime CloseAt { get; set; } // Thời gian đóng điểm danh

        public bool IsActive { get; set; } = true; // Còn mở hay đã đóng

        public ICollection<AttendanceRecord> Records { get; set; } = new List<AttendanceRecord>();
    }
}
