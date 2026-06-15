namespace Application.ViewModels.Common;

public class BaseSearchModel : IBaseSearchModel
{
    public string? Keyword { get; set; }
    public int PageNumber { get; set; } = 1;
    public int PageSize { get; set; } = 10;
    public bool DisablePagination { get; set; } = false;
    public string? SortBy { get; set; }
    public string? SortDirection { get; set; }
}
