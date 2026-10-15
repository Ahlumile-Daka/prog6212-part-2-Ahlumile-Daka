using System.ComponentModel.DataAnnotations;

namespace RaceDay.Api.Models
{
    public class EventType
    {
        [Key]
        public int EventTypeID { get; set; }

        [Required, MaxLength(30)]
        public string Name { get; set; } = string.Empty;

        [MaxLength(200)]
        public string? Description { get; set; }

        public ICollection<Event> Events { get; set; } = new List<Event>();
    }
}
