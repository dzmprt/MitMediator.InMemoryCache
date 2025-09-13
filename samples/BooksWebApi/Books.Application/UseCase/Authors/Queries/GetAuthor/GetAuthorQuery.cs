using Books.Domain;
using MitMediator;
using MitMediator.InMemoryCache;

namespace Books.Application.UseCase.Authors.Queries.GetAuthor;

/// <summary>
/// Get author query.
/// </summary>
[CacheResponse]
public struct GetAuthorQuery : IRequest<Author>
{
    /// <summary>
    /// Author id.
    /// </summary>
    public int AuthorId { get; init; }
}