using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;

namespace DDAC.Models
{
    public class CareerAdvisorProfile
    {
        [Key]
        [ForeignKey(nameof(User))]
        public int AdvisorID { get; set; }
        public User User { get; set; }

        [Required]
        [StringLength(150)]
        public string Specialisation { get; set; }

        [Required]
        [StringLength(200)]
        public string Qualification { get; set; }

        [Required]
        public int ExperienceYears { get; set; }

        public string Bio { get; set; }
    }
}