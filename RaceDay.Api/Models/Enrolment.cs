using System.ComponentModel.DataAnnotations;

namespace RaceDay.Api.Models
{
    public class Enrolment
    {
        [Key]
        public int EnrolmentID { get; set; }

        public int ParticipantID { get; set; }
        public User? Participant { get; set; }

        public int EventID { get; set; }
        public Event? Event { get; set; }

        public int CategoryID { get; set; }
        public Category? Category { get; set; }

        public DateTime EnrolmentDate { get; set; } = DateTime.UtcNow;

        [Required, MaxLength(20)]
        public string Status { get; set; } = "Confirmed"; // Confirmed, Cancelled

        public Result? Result { get; set; }
    }
}
