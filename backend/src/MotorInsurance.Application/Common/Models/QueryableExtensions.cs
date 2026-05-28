using Microsoft.EntityFrameworkCore;

namespace MotorInsurance.Application.Common.Models;

public static class QueryableExtensions
{
    private const int DefaultPageSize = 20;
    private const int MaxPageSize = 100;

    private static (int page, int size) Normalize(int page, int pageSize)
    {
        page = page < 1 ? 1 : page;
        pageSize = pageSize < 1 ? DefaultPageSize : (pageSize > MaxPageSize ? MaxPageSize : pageSize);
        return (page, pageSize);
    }

    /// <summary>Counts, skips/takes, and returns a PagedResult. Projection happens in SQL.</summary>
    public static async Task<PagedResult<T>> ToPagedResultAsync<T>(
        this IQueryable<T> source, int page, int pageSize, CancellationToken ct = default)
    {
        (page, pageSize) = Normalize(page, pageSize);
        var total = await source.CountAsync(ct);
        var items = await source.Skip((page - 1) * pageSize).Take(pageSize).ToListAsync(ct);
        return new PagedResult<T>(items, page, pageSize, total);
    }

    /// <summary>
    /// Pages an entity/anonymous query, then maps each row in memory. Use when the
    /// projection can't be translated to SQL (e.g. converted enum.ToString()).
    /// </summary>
    public static async Task<PagedResult<TOut>> ToPagedResultAsync<TIn, TOut>(
        this IQueryable<TIn> source, int page, int pageSize, Func<TIn, TOut> map, CancellationToken ct = default)
    {
        (page, pageSize) = Normalize(page, pageSize);
        var total = await source.CountAsync(ct);
        var rows = await source.Skip((page - 1) * pageSize).Take(pageSize).ToListAsync(ct);
        return new PagedResult<TOut>(rows.Select(map).ToList(), page, pageSize, total);
    }
}
