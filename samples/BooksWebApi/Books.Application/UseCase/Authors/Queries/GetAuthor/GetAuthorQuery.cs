using Books.Domain;
using MitMediator;

namespace Books.Application.UseCase.Authors.Queries.GetAuthor;

/// <summary>
/// Get author query.
/// </summary>
[CacheResponseInMemory]
public struct GetAuthorQuery : IRequest<Author>
{
    /// <summary>
    /// Author id.
    /// </summary>
    public int AuthorId { get; init; }
}