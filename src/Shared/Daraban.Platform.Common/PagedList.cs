namespace Daraban.Platform.Common;

/// <summary>Matches the page-based response envelope from Task 1.4 SS5/SS7.1.</summary>
public sealed class PagedList<T>
{
    public IReadOnlyList<T> Items { get; }
    public int Page { get; }
    public int PageSize { get; }
    public int TotalCount { get; }
    public int TotalPages => (int)Math.Ceiling(TotalCount / (double)PageSize);

    public PagedList(IReadOnlyList<T> items, int page, int pageSize, int totalCount)
    {
        Items = items;
        Page = page;
        PageSize = pageSize;
        TotalCount = totalCount;
    }
}

/// <summary>Matches the keyset/cursor response envelope from Task 1.4 SS7.2.</summary>
public sealed class CursorPagedList<T>
{
    public IReadOnlyList<T> Items { get; }
    public string? NextCursor { get; }
    public bool HasMore { get; }

    public CursorPagedList(IReadOnlyList<T> items, string? nextCursor, bool hasMore)
    {
        Items = items;
        NextCursor = nextCursor;
        HasMore = hasMore;
    }
}
