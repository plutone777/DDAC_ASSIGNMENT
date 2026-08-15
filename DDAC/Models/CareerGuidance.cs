using System.ComponentModel.DataAnnotations;

namespace DDAC.Models
{
    public class CareerGuidance
    {
        [Key]
        public int GuidanceID { get; set; }

        public int AdvisorID { get; set; }

        public int JobSeekerID { get; set; }

        [Required]
        [StringLength(50)]
        public string GuidanceType { get; set; }

        [Required]
        [StringLength(200)]
        public string Subject { get; set; }

        [Required]
        public string GuidanceNotes { get; set; }

        [Required]
        public DateTime GuidanceDate { get; set; }

        [Required]
        [StringLength(30)]
        public string Status { get; set; } = "Requested";
    }
}