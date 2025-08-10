// Models/MatchInfo.cs
namespace FileUploadApi.Models
{
    public class MatchInfo
    {
        public int Id { get; set; }
        public string TournamentName { get; set; } = null!;
        public string Year { get; set; } = null!;
        public string MatchId { get; set; } = null!;
        public string JsonData { get; set; } = null!;
    }
}
