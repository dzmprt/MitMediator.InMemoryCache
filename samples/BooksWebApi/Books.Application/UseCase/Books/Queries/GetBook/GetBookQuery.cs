using Books.Domain;
using MitMediator;
using MitMediator.InMemoryCache;

namespace Books.Application.UseCase.Books.Queries.GetBook;

/// <summary>
/// Get book query.
/// </summary>
[CacheForSeconds(10)]
public struct GetBookQuery : IRequest<Book>
{
    /// <summary>
    /// Book id.
    /// </summary>
    public int BookId { get; set; }
}