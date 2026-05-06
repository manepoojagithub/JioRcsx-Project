namespace JioCxRcsWrapper.Application.Common.Pagination;

public interface IPagedResult
{
    int PageNumber { get; }

    int PageSize { get; }

    int TotalItems { get; }

    int TotalPages { get; }

    bool HasPreviousPage { get; }

    bool HasNextPage { get; }
}
