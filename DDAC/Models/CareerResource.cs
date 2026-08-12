using System.ComponentModel.DataAnnotations;

namespace DDAC.Models
{
    public class CareerResource
    {
        [Key]
        public int ResourceID { get; set; }

        public int AdvisorID { get; set; }

        [Required]
        [StringLength(200)]
        public string Title { get; set; }

        [Required]
        public string Description { get; set; }

        [Required]
        [StringLength(100)]
        public string Category { get; set; }

        [StringLength(500)]
        public string ContentURL { get; set; }

        [Required]
        public DateTime PublishedDate { get; set; }

        [Required]
        [StringLength(20)]
        public string Status { get; set; } = "Draft";
    }
}