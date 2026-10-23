using Books.Application.UseCase.Books.Commands.CreateBook;
using Books.Application.UseCase.Books.Commands.DeleteBook;
using Books.Application.UseCase.Books.Commands.UpdateBook;
using Books.Domain;
using MitMediator;
using MitMediator.InMemoryCache;

namespace Books.Application.UseCase.Books.Queries.GetBooksByFilter;

/// <summary>
/// Get books query.
/// </summary>
[CacheResponseInMemory(30, 2, typeof(CreateBookCommand), typeof(UpdateBookCommand), typeof(DeleteBookCommand))]
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