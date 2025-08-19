// Models/MatchInfo.cs
using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;

namespace FileUploadApi.Models
{
    public class MatchInfo
    {
        [Key]
        [DatabaseGenerated(DatabaseGeneratedOption.Identity)]
        public int Id { get; set; }
        public string TournamentName { get; set; } = null!;
        public string Year { get; set; } = null!;
        public string MatchId { get; set; } = null!;
        public string JsonData { get; set; } = null!;
    }
}