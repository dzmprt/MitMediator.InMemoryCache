using Books.Application.Abstractions.Infrastructure;
using Books.Application.Exceptions;
using Books.Domain;
using MitMediator;

namespace Books.Application.UseCase.Authors.Queries.GetAuthor;

/// <summary>
/// Handler for <see cref="GetAuthorQuery"/>.
/// </summary>
internal sealed class GetAuthorQueryHandler : IRequestHandler<GetAuthorQuery, Author>
{
    private readonly IBaseProvider<Author> _authorProvider;

    /// <summary>
    /// Initializes a new instance of the <see cref="GetAuthorQueryHandler"/>.
    /// </summary>
    /// <param name="authorProvider">Author provider.</param>
    public GetAuthorQueryHandler(IBaseProvider<Author> authorProvider)
    {
        _authorProvider = authorProvider;
    }
    
    /// <inheritdoc/>
    public async ValueTask<Author> HandleAsync(GetAuthorQuery query, CancellationToken cancellationToken)
    {
        Thread.Sleep(2000);
        var author = await _authorProvider.FirstOrDefaultAsync(q => q.AuthorId == query.AuthorId, cancellationToken);
        if (author is null)
        {
            throw new NotFoundException();
        }

        return author;
    }
}