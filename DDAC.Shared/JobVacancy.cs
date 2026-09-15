using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;

namespace DDAC.Models
{
    public class JobVacancy
    {
        [Key]
        public int JobID { get; set; }

        public int EmployerID { get; set; }

        [Required]
        [StringLength(150)]
        public string JobTitle { get; set; }

        [Required]
        public string Description { get; set; }

        [StringLength(150)]
        public string Location { get; set; }

        [Required]
        [StringLength(50)]
        public string EmploymentType { get; set; }

        [Column(TypeName = "decimal(10,2)")]
        public decimal Salary { get; set; }

        public string AccessibilityFeatures { get; set; }

        public string AccommodationsAvailable { get; set; }

        [Required]
        public DateTime PostedDate { get; set; }

        public DateTime ClosingDate { get; set; }

        [Required]
        [StringLength(20)]
        public string Status { get; set; } = "Draft";
    }
}