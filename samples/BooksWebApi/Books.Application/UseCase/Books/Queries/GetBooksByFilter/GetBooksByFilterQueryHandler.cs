using Books.Application.Abstractions.Infrastructure;
using Books.Application.UseCase.Authors.Queries.GetAuthorsByFilter;
using Books.Domain;
using MitMediator;

namespace Books.Application.UseCase.Books.Queries.GetBooksByFilter;

/// <summary>
/// Handler for <see cref="GetBooksByFilterQuery"/>.
/// </summary>
/// <remarks>
/// Initializes a new instance of the <see cref="GetAuthorsByFilterQueryHandler"/>.
/// </remarks>
/// <param name="booksProvider">Books provider.</param>
internal sealed class GetBooksByFilterQueryHandler(IBaseProvider<Book> booksProvider) : IRequestHandler<GetBooksByFilterQuery, Book[]>
{
    /// <inheritdoc/>
    public ValueTask<Book[]> HandleAsync(GetBooksByFilterQuery request, CancellationToken cancellationToken)
    {
        Thread.Sleep(1000);
        var freeText = request.FreeText?.Trim().ToUpperInvariant();
        return booksProvider.SearchAsync(
            freeText is null
                ? null
                : q => q.Author.FirstName.Contains(freeText) ||
                        q.Author.LastName.Contains(freeText) ||
                        q.Title.Contains(freeText) ||
                        q.Genre.GenreName.Contains(freeText),
            o => o.BookId,
            request.Limit,
            request.Offset,
            cancellationToken
        );
    }
}