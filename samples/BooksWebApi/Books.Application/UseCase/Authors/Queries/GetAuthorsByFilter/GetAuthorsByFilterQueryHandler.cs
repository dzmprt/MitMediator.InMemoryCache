using Books.Application.Abstractions.Infrastructure;
using Books.Domain;
using MitMediator;

namespace Books.Application.UseCase.Authors.Queries.GetAuthorsByFilter;

/// <summary>
/// Handler for <see cref="GetAuthorsByFilterQuery"/>.
/// </summary>
/// <remarks>
/// Initializes a new instance of the <see cref="GetAuthorsByFilterQueryHandler"/>.
/// </remarks>
/// <param name="authorProvider">Author provider.</param>
internal sealed class GetAuthorsByFilterQueryHandler(IBaseProvider<Author> authorProvider) : IRequestHandler<GetAuthorsByFilterQuery, Author[]>
{
    /// <inheritdoc/>
    public ValueTask<Author[]> HandleAsync(GetAuthorsByFilterQuery request, CancellationToken cancellationToken)
    {
        Thread.Sleep(1000);
        var freeText = request.FreeText?.Trim().ToUpperInvariant();
        return authorProvider.SearchAsync(
            freeText is null
                ? null
                : q => q.FirstName.Contains(freeText) || q.LastName.Contains(freeText),
            o => o.AuthorId,
            request.Limit,
            request.Offset,
            cancellationToken
        );
    }
}