using System.ComponentModel.DataAnnotations;

namespace DDAC.Models
{
    public class CareerAdvisorProfile
    {
        [Key]
        public int AdvisorID { get; set; }

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