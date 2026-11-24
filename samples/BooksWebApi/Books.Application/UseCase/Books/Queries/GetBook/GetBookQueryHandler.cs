using Books.Application.Abstractions.Infrastructure;
using Books.Application.Exceptions;
using Books.Domain;
using MitMediator;

namespace Books.Application.UseCase.Books.Queries.GetBook;

/// <summary>
/// Handler for <see cref="GetBookQuery"/>.
/// </summary>
/// <remarks>
/// Initializes a new instance of the <see cref="GetBookQueryHandler"/>.
/// </remarks>
/// <param name="bookProvider">Books provider.</param>
internal sealed class GetBookQueryHandler(IBaseProvider<Book> bookProvider) : IRequestHandler<GetBookQuery, Book>
{
    /// <inheritdoc/>
    public async ValueTask<Book> HandleAsync(GetBookQuery query, CancellationToken cancellationToken)
    {
        Thread.Sleep(1000);
        var book = await bookProvider.FirstOrDefaultAsync(q => q.BookId == query.BookId, cancellationToken);
        if (book is null)
        {
            throw new NotFoundException();
        }

        return book;
    }
}