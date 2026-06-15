namespace Application.ViewModels.Common;

public interface IBaseSearchModel
{
    string? Keyword { get; set; }
    int PageNumber { get; set; }
    int PageSize { get; set; }
    bool DisablePagination { get; set; }
    string? SortBy { get; set; }
    string? SortDirection { get; set; }
}
