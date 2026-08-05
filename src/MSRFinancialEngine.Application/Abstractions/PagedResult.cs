namespace MSRFinancialEngine.Application.Abstractions;

public class PagedResult<T>
{
    public required IReadOnlyList<T> Items { get; init; }
    public required int Page { get; init; }
    public required int PageSize { get; init; }
    public required int TotalItems { get; init; }
    public int TotalPages => PageSize == 0 ? 0 : (int)Math.Ceiling(TotalItems / (double)PageSize);
}

public class PageRequest
{
    public const int MaxPageSize = 200;
    public const int DefaultPageSize = 50;

    private int _page = 1;
    private int _pageSize = DefaultPageSize;

    public int Page
    {
        get => _page;
        set => _page = value < 1 ? 1 : value;
    }

    public int PageSize
    {
        get => _pageSize;
        set => _pageSize = value switch
        {
            < 1 => DefaultPageSize,
            > MaxPageSize => MaxPageSize,
            _ => value
        };
    }
}

public static class QueryablePagingExtensions
{
    public static PagedResult<T> ToPagedResult<T>(this IQueryable<T> query, PageRequest request)
    {
        var pageNumber = Math.Max(1, request.Page);
        var pageSize = Math.Clamp(request.PageSize, 1, PageRequest.MaxPageSize);

        var total = query.Count();
        var items = query
            .Skip((pageNumber - 1) * pageSize)
            .Take(pageSize)
            .ToList();

        return new PagedResult<T>
        {
            Items = items,
            Page = pageNumber,
            PageSize = pageSize,
            TotalItems = total
        };
    }
}
