using Books.Domain;
using Books.Application.UseCase.Authors.Commands.DeleteAuthor;
using Books.Application.UseCase.Authors.Commands.UpdateAuthor;
using MitMediator;
using MitMediator.InMemoryCache;

namespace Books.Application.UseCase.Authors.Queries.GetAuthor;

/// <summary>
/// Get author query.
/// </summary>
[CacheResponseInMemory(typeof(UpdateAuthorCommand), typeof(DeleteAuthorCommand))]
public struct GetAuthorQuery : IRequest<Author>
{
    /// <summary>
    /// Author id.
    /// </summary>
    public int AuthorId { get; init; }
}