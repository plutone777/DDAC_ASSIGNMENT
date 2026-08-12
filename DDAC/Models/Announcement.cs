using System.ComponentModel.DataAnnotations;

namespace DDAC.Models
{
    public class Announcement
    {
        [Key]
        public int AnnouncementID { get; set; }

        public int AdminID { get; set; }

        [Required]
        [StringLength(200)]
        public string Title { get; set; }

        [Required]
        public string Content { get; set; }

        [Required]
        public DateTime PublishedDate { get; set; }

        [Required]
        [StringLength(20)]
        public string Status { get; set; } = "Draft";
    }
}