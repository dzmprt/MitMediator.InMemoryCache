using Books.Application.UseCase.Authors.Commands.DeleteAuthor;
using Books.Application.UseCase.Authors.Commands.UpdateAuthor;
using Books.Domain;
using MitMediator;
using MitMediator.InMemoryCache;

namespace Books.Application.UseCase.Authors.Queries.GetAuthor;

/// <summary>
/// Get author query.
/// </summary>
[CacheUntilSent(typeof(DeleteAuthorCommand), typeof(UpdateAuthorCommand))]
public struct GetAuthorQuery : IRequest<Author>
{
    /// <summary>
    /// Author id.
    /// </summary>
    public int AuthorId { get; init; }
}