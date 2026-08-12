using System.ComponentModel.DataAnnotations;

namespace DDAC.Models
{
    public class EmployerProfile
    {
        [Key]
        public int EmployerID { get; set; }

        [Required]
        [StringLength(150)]
        public string CompanyName { get; set; }

        [Required]
        [StringLength(100)]
        public string Industry { get; set; }

        public string CompanyDescription { get; set; }

        [StringLength(255)]
        public string Address { get; set; }

        [StringLength(255)]
        public string Website { get; set; }

        [Required]
        [StringLength(30)]
        public string VerificationStatus { get; set; } = "Pending";
    }
}