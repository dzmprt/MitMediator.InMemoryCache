using Books.Domain;
using MitMediator;
using MitMediator.InMemoryCache;

namespace Books.Application.UseCase.Books.Queries.GetBooksByFilter;

/// <summary>
/// Get books query.
/// </summary>
[CacheForSeconds(10)]
public struct GetBooksByFilterQuery : IRequest<Book[]>
{
    /// <summary>
    /// Limit.
    /// </summary>
    public int? Limit { get; init; }
    
    /// <summary>
    /// Offset.
    /// </summary>
    public int? Offset { get; init; }
    
    /// <summary>
    /// Free text.
    /// </summary>
    public string? FreeText { get; init; }
}