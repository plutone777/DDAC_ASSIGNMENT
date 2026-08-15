using System.ComponentModel.DataAnnotations;

namespace DDAC.Models
{
    public class JobInterview
    {
        [Key]
        public int InterviewID { get; set; }

        public int ApplicationID { get; set; }

        [Required]
        public DateTime InterviewDate { get; set; }

        [Required]
        [StringLength(30)]
        public string InterviewType { get; set; }

        [StringLength(255)]
        public string Location { get; set; }

        [Required]
        [StringLength(30)]
        public string Status { get; set; } = "Scheduled";

        public string Notes { get; set; }
    }
}