using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;

namespace RaceDay.Api.Models
{
    public class Category
    {
        [Key]
        public int CategoryID { get; set; }

        public int EventID { get; set; }
        public Event? Event { get; set; }

        [Required, MaxLength(50)]
        public string CategoryName { get; set; } = string.Empty;

        public int? MinAge { get; set; }
        public int? MaxAge { get; set; }

        [Column(TypeName = "decimal(8,2)")]
        public decimal EntryFee { get; set; } = 0;

        public int? MaxEntries { get; set; }

        public ICollection<Enrolment> Enrolments { get; set; } = new List<Enrolment>();
    }
}
