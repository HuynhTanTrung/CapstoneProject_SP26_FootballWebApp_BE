namespace VNFootballLeaguesApp.DTOs.Common;

public class PagedResultDto<T>
{
    public IReadOnlyCollection<T> Items { get; set; } = [];

    public int PageNumber { get; set; }

    public int PageSize { get; set; }

    public int TotalCount { get; set; }

    public int TotalPages { get; set; }
}
