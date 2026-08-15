using System.ComponentModel.DataAnnotations;

namespace DDAC.Models
{
    public class Inquiry
    {
        [Key]
        public int InquiryID { get; set; }

        public int UserID { get; set; }

        public int AdvisorID { get; set; }

        [Required]
        [StringLength(200)]
        public string Subject { get; set; }

        [Required]
        public string Message { get; set; }

        public string? Response { get; set; }

        [Required]
        [StringLength(30)]
        public string Status { get; set; } = "Open";

        [Required]
        public DateTime CreatedDate { get; set; }

        public DateTime? ResolvedDate { get; set; }
    }
}