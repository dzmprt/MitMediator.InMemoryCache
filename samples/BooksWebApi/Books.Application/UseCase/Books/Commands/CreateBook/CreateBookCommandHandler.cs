using Books.Application.Abstractions.Infrastructure;
using Books.Application.Exceptions;
using Books.Domain;
using MitMediator;

namespace Books.Application.UseCase.Books.Commands.CreateBook;

/// <summary>
/// Handler for <see cref="CreateBookCommand"/>
/// </summary>
/// <remarks>
/// Initializes a new instance of the <see cref="CreateBookCommandHandler"/>.
/// </remarks>
/// <param name="booksRepository">Books repository.</param>
/// <param name="authorsRepository">Authors repository.</param>
/// <param name="genresRepository">Genres repository.</param>
public class CreateBookCommandHandler(
    IBaseRepository<Book> booksRepository,
    IBaseRepository<Author> authorsRepository,
    IBaseRepository<Genre> genresRepository) : IRequestHandler<CreateBookCommand, Book>
{
    /// <inheritdoc/>
    /// <returns>The created book.</returns>
    public async ValueTask<Book> HandleAsync(CreateBookCommand request, CancellationToken cancellationToken)
    {
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
        var book = new Book(request.Title, author, genre);
        await booksRepository.AddAsync(book, cancellationToken);
        return book;
    }
}