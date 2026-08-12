using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;

namespace DDAC.Models
{
    public class JobSeekerQualification
    {
        [Key]
        public int QualificationID { get; set; }

        [ForeignKey(nameof(JobSeekerProfile))]
        public int JobSeekerID { get; set; }

        [Required]
        [StringLength(150)]
        public string QualificationName { get; set; }

        [Required]
        [StringLength(150)]
        public string Institution { get; set; }

        public int CompletionYear { get; set; }
    }
}