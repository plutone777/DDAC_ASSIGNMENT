using System.ComponentModel.DataAnnotations;

namespace DDAC.Models
{
    public class CareerRecommendation
    {
        [Key]
        public int RecommendationID { get; set; }

        public int AdvisorID { get; set; }

        public int JobSeekerID { get; set; }

        [Required]
        [StringLength(50)]
        public string RecommendationType { get; set; }

        [Required]
        [StringLength(200)]
        public string Title { get; set; }

        [Required]
        public string Description { get; set; }

        [Required]
        public string Reason { get; set; }

        [Required]
        public DateTime DateCreated { get; set; }
    }
}