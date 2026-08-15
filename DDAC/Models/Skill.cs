using System.Collections.Generic;
using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;

namespace DDAC.Models
{
    public class Skill
    {
        [Key]
        public int SkillID { get; set; }

        [Required]
        [StringLength(100)]
        public string SkillName { get; set; }

    }
}