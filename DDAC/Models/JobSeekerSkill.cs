using System.ComponentModel.DataAnnotations;

namespace DDAC.Models
{
    public class JobSeekerSkill
    {
        public int JobSeekerID { get; set; }

        public int SkillID { get; set; }

        [Required]
        [StringLength(50)]
        public string SkillLevel { get; set; }
    }
}