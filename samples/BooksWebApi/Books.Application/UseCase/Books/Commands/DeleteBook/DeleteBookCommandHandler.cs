using Books.Application.UseCase.Authors.Commands.DeleteAuthor;
using Books.Application.Abstractions.Infrastructure;
using Books.Application.Exceptions;
using Books.Domain;
using MitMediator;

namespace Books.Application.UseCase.Books.Commands.DeleteBook;

/// <summary>
/// Handler for <see cref="DeleteBookCommand"/>.
/// </summary>
/// <remarks>
/// Initializes a new instance of the <see cref="DeleteBookCommandHandler"/>.
/// </remarks>
/// <param name="booksRepository">Books repository.</param>
internal sealed class DeleteBookCommandHandler(IBaseRepository<Book> booksRepository) : IRequestHandler<DeleteBookCommand>
{
    /// <inheritdoc/>
    public async ValueTask<Unit> HandleAsync(DeleteBookCommand command, CancellationToken cancellationToken)
    {
        var book = await booksRepository.FirstOrDefaultAsync(q => q.BookId == command.BookId, cancellationToken);
        if (book is null)
        {
            throw new NotFoundException();
        }
        book.MarkDeleted();
        await booksRepository.RemoveAsync(book, cancellationToken);
        return Unit.Value;
    }
}