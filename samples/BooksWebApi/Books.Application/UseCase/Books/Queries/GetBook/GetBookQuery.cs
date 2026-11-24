using Books.Domain;
using Books.Application.UseCase.Books.Commands.DeleteBook;
using Books.Application.UseCase.Books.Commands.UpdateBook;
using MitMediator;
using MitMediator.InMemoryCache;

namespace Books.Application.UseCase.Books.Queries.GetBook;

/// <summary>
/// Get book query.
/// </summary>
[CacheResponseInMemory(typeof(UpdateBookCommand), typeof(DeleteBookCommand))]
public struct GetBookQuery : IRequest<Book>
{
    /// <summary>
    /// Book id.
    /// </summary>
    public int BookId { get; set; }
}