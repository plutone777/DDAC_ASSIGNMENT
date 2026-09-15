using System.ComponentModel.DataAnnotations;

namespace DDAC.Models
{
    public class JobApplication
    {
        [Key]
        public int ApplicationID { get; set; }

        public int JobID { get; set; }

        public int JobSeekerID { get; set; }

        [Required]
        public DateTime ApplicationDate { get; set; }

        [StringLength(500)]
        public string ResumeURL { get; set; }

        public string CoverLetter { get; set; }

        [Required]
        [StringLength(30)]
        public string Status { get; set; } = "Submitted";
    }
}