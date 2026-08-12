using System.ComponentModel.DataAnnotations;

namespace DDAC.Models
{
    public class TrainingProgram
    {
        [Key]
        public int TrainingID { get; set; }

        [Required]
        [StringLength(200)]
        public string Title { get; set; }

        [Required]
        [StringLength(150)]
        public string Provider { get; set; }

        [Required]
        public string Description { get; set; }

        public string Eligibility { get; set; }

        [StringLength(500)]
        public string URL { get; set; }

        [Required]
        [StringLength(20)]
        public string Status { get; set; } = "Active";
    }
}