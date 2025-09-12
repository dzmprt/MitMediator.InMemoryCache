using Books.Application.UseCase.Authors.Commands.CreateAuthor;
using Books.Application.UseCase.Authors.Commands.DeleteAuthor;
using Books.Application.UseCase.Authors.Commands.UpdateAuthor;
using Books.Domain;
using MitMediator;
using MitMediator.InMemoryCache;

namespace Books.Application.UseCase.Authors.Queries.GetAuthorsByFilter;

/// <summary>
/// Get authors query.
/// </summary>
[CacheUntilSent(typeof(CreateAuthorCommand), typeof(DeleteAuthorCommand), typeof(UpdateAuthorCommand))]
public struct GetAuthorsByFilterQuery : IRequest<Author[]>
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