namespace JioCxRcsWrapper.Application.Common.Pagination;

public sealed class PagedResult<T> : IPagedResult
{
    private const int DefaultPageSize = 10;
    private const int MaximumPageSize = 100;

    private PagedResult(IReadOnlyList<T> items, int pageNumber, int pageSize, int totalItems)
    {
        Items = items;
        PageNumber = pageNumber;
        PageSize = pageSize;
        TotalItems = totalItems;
        TotalPages = totalItems == 0 ? 1 : (int)Math.Ceiling(totalItems / (double)pageSize);
    }

    public IReadOnlyList<T> Items { get; }

    public int PageNumber { get; }

    public int PageSize { get; }

    public int TotalItems { get; }

    public int TotalPages { get; }

    public bool HasPreviousPage => PageNumber > 1;

    public bool HasNextPage => PageNumber < TotalPages;

    public static PagedResult<T> Create(IEnumerable<T> source, int pageNumber, int pageSize)
    {
        ArgumentNullException.ThrowIfNull(source);

        var normalizedPageSize = pageSize <= 0 ? DefaultPageSize : Math.Min(pageSize, MaximumPageSize);
        var allItems = source as IReadOnlyList<T> ?? source.ToArray();
        var totalItems = allItems.Count;
        var totalPages = totalItems == 0 ? 1 : (int)Math.Ceiling(totalItems / (double)normalizedPageSize);
        var normalizedPageNumber = Math.Clamp(pageNumber <= 0 ? 1 : pageNumber, 1, totalPages);
        var items = allItems
            .Skip((normalizedPageNumber - 1) * normalizedPageSize)
            .Take(normalizedPageSize)
            .ToArray();

        return new PagedResult<T>(items, normalizedPageNumber, normalizedPageSize, totalItems);
    }
}
