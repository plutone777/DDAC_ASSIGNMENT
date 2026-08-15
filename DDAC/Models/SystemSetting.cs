using System.ComponentModel.DataAnnotations;

namespace DDAC.Models
{
    public class SystemSetting
    {
        [Key]
        public int SettingID { get; set; }

        [Required]
        [StringLength(50)]
        public string SettingCategory { get; set; }

        [Required]
        [StringLength(100)]
        public string SettingKey { get; set; }

        [Required]
        [StringLength(255)]
        public string SettingValue { get; set; }

        [Required]
        [StringLength(255)]
        public string Description { get; set; }

        [Required]
        public DateTime UpdatedDate { get; set; }

        public int UpdatedBy { get; set; }
    }
}