using Books.Application.Abstractions.Infrastructure;
using Books.Application.Exceptions;
using Books.Application.UseCase.Books.Commands.CreateBook;
using Books.Domain;
using MitMediator;

namespace Books.Application.UseCase.Books.Commands.UpdateBook;

/// <summary>
/// Handler for <see cref="CreateBookCommand"/>
/// </summary>
/// <remarks>
/// Initializes a new instance of the <see cref="CreateBookCommandHandler"/>.
/// </remarks>
/// <param name="booksRepository">Books repository.</param>
/// <param name="authorsRepository">Authors repository.</param>
/// <param name="genresRepository">Genres repository.</param>
public class UpdateBookCommandHandler(
    IBaseRepository<Book> booksRepository,
    IBaseRepository<Author> authorsRepository,
    IBaseRepository<Genre> genresRepository) : IRequestHandler<UpdateBookCommand, Book>
{
    /// <inheritdoc/>
    public async ValueTask<Book> HandleAsync(UpdateBookCommand request, CancellationToken cancellationToken)
    {
        var book = await booksRepository.FirstOrDefaultAsync(b => b.BookId == request.BookId, cancellationToken);
        if (book is null)
        {
            throw new NotFoundException();
        }
        
        var author = await authorsRepository.FirstOrDefaultAsync(a => a.AuthorId == request.AuthorId, cancellationToken);
        if (author is null)
        {
            throw new BadOperationException("Author not found");
        }
        var genre = await genresRepository.FirstOrDefaultAsync(g => g.GenreName == request.GenreName.Trim().ToUpperInvariant(), cancellationToken);
        if (genre is null)
        {
            throw new BadOperationException("Genre not found");
        }
        
        book.Update(request.Title, author, genre);

        await booksRepository.UpdateAsync(book, cancellationToken);
        return book;
    }
}