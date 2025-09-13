using Books.Application.Abstractions.Infrastructure;
using Books.Application.Exceptions;
using Books.Application.UseCase.Books.Commands.CreateBook;
using Books.Application.UseCase.Books.Queries.GetBook;
using Books.Domain;
using MitMediator;
using MitMediator.InMemoryCache;

namespace Books.Application.UseCase.Books.Commands.UpdateBook;

/// <summary>
/// Handler for <see cref="CreateBookCommand"/>
/// </summary>
public class UpdateBookCommandHandler : IRequestHandler<UpdateBookCommand, Book>
{
    private readonly IBaseRepository<Book> _booksRepository;
    
    private readonly IBaseRepository<Author> _authorsRepository;
    
    private readonly IBaseRepository<Genre> _genresRepository;
    
    private readonly IMediator _mediator;

    /// <summary>
    /// Initializes a new instance of the <see cref="CreateBookCommandHandler"/>.
    /// </summary>
    /// <param name="booksRepository">Books repository.</param>
    /// <param name="authorsRepository">Authors repository.</param>
    /// <param name="genresRepository">Genres repository.</param>
    public UpdateBookCommandHandler(
        IBaseRepository<Book> booksRepository, 
        IBaseRepository<Author> authorsRepository,
        IBaseRepository<Genre> genresRepository,
        IMediator mediator)
    {
        _booksRepository = booksRepository;
        _authorsRepository = authorsRepository;
        _genresRepository = genresRepository;
        _mediator = mediator;
    }
    
    /// <inheritdoc/>
    public async ValueTask<Book> HandleAsync(UpdateBookCommand command, CancellationToken cancellationToken)
    {
        var book = await _booksRepository.FirstOrDefaultAsync(b => b.BookId == command.BookId, cancellationToken);
        if (book is null)
        {
            throw new NotFoundException();
        }
        
        var author = await _authorsRepository.FirstOrDefaultAsync(a => a.AuthorId == command.AuthorId, cancellationToken);
        if (author is null)
        {
            throw new BadOperationException("Author not found");
        }
        var genre = await _genresRepository.FirstOrDefaultAsync(g => g.GenreName == command.GenreName.Trim().ToUpperInvariant(), cancellationToken);
        if (genre is null)
        {
            throw new BadOperationException("Genre not found");
        }
        
        book.SetTitle(command.Title);
        book.SetAuthor(author);
        book.SetGenre(genre);

        await _booksRepository.UpdateAsync(book, cancellationToken);
        await _mediator.ClearResponseCacheAsync(new GetBookQuery() { BookId = command.BookId }, cancellationToken);
        await _mediator.ClearAllResponseCacheAsync<GetBookQuery>(cancellationToken);
        return book;
    }
}