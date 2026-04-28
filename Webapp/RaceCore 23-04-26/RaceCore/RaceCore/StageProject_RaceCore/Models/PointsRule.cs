using System.ComponentModel.DataAnnotations;

namespace StageProject_RaceCore.Models
{
    public class PointsRule
    {
        public int Id { get; set; }

        [Required]
        [MaxLength(50)]
        public string Type { get; set; } = string.Empty;

        public int? FromPosition { get; set; }
        public int? ToPosition { get; set; }
        public int Points { get; set; }
    }
}