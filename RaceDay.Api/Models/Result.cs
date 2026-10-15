using System.ComponentModel.DataAnnotations;

namespace RaceDay.Api.Models
{
    public class Result
    {
        [Key]
        public int ResultID { get; set; }

        public int EnrolmentID { get; set; }
        public Enrolment? Enrolment { get; set; }

        public TimeSpan? FinishTime { get; set; }

        public int? Position { get; set; }

        [MaxLength(200)]
        public string? Notes { get; set; }

        public int CapturedBy { get; set; }
        public User? CapturedByUser { get; set; }

        public DateTime CapturedDate { get; set; } = DateTime.UtcNow;
    }
}
