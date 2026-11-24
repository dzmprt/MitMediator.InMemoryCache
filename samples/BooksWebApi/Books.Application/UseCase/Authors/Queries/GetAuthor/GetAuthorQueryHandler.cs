using Books.Application.Abstractions.Infrastructure;
using Books.Application.Exceptions;
using Books.Domain;
using MitMediator;

namespace Books.Application.UseCase.Authors.Queries.GetAuthor;

/// <summary>
/// Handler for <see cref="GetAuthorQuery"/>.
/// </summary>
/// <remarks>
/// Initializes a new instance of the <see cref="GetAuthorQueryHandler"/>.
/// </remarks>
/// <param name="authorProvider">Author provider.</param>
internal sealed class GetAuthorQueryHandler(IBaseProvider<Author> authorProvider) : IRequestHandler<GetAuthorQuery, Author>
{
    /// <inheritdoc/>
    public async ValueTask<Author> HandleAsync(GetAuthorQuery query, CancellationToken cancellationToken)
    {
        Thread.Sleep(1000);
        var author = await authorProvider.FirstOrDefaultAsync(q => q.AuthorId == query.AuthorId, cancellationToken);
        if (author is null)
        {
            throw new NotFoundException();
        }

        return author;
    }
}