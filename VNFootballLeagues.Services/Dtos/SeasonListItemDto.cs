namespace VNFootballLeagues.Services.Dtos;

/// <summary>Kết quả liệt kê mùa giải (DB và/hoặc SofaScore).</summary>
public sealed class SeasonListItemDto
{
    public int? SeasonId { get; set; }
    public int? LeagueId { get; set; }
    public int? Year { get; set; }
    public int? ApiSeasonId { get; set; }
}
