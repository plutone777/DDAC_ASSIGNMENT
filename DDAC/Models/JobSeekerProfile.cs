using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;

namespace DDAC.Models
{
    public class JobSeekerProfile
    {
        [Key]
        [ForeignKey(nameof(User))]
        public int JobSeekerID { get; set; }

        public User? User { get; set; }

        [StringLength(255)]
        public string? CareerGoal { get; set; }

        public string? Bio { get; set; }

        [StringLength(500)]
        public string? ResumeURL { get; set; }

        [StringLength(150)]
        public string? PreferredLocation { get; set; }

        public string? AccommodationNeeds { get; set; }

    }
}