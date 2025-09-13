using Books.Application.Abstractions.Infrastructure;
using Books.Application.Exceptions;
using Books.Application.UseCase.Authors.Queries.GetAuthor;
using Books.Domain;
using MitMediator;
using MitMediator.InMemoryCache;

namespace Books.Application.UseCase.Authors.Commands.UpdateAuthor;

/// <summary>
/// Handler for <see cref="UpdateAuthorCommand"/>.
/// </summary>
internal sealed class UpdateAuthorCommandHandler : IRequestHandler<UpdateAuthorCommand, Author>
{
    private readonly IBaseRepository<Author> _authorRepository;
    private readonly IMediator _mediator;

    /// <summary>
    /// Initializes a new instance of the <see cref="UpdateAuthorCommandHandler"/>.
    /// </summary>
    /// <param name="authorRepository">Author repository.</param>
    public UpdateAuthorCommandHandler(IBaseRepository<Author> authorRepository, IMediator mediator)
    {
        _authorRepository = authorRepository;
        _mediator = mediator;
    }

    /// <inheritdoc/>
    /// <returns>The updated author.</returns>
    public async ValueTask<Author> HandleAsync(UpdateAuthorCommand command, CancellationToken cancellationToken)
    {
        var author =
            await _authorRepository.FirstOrDefaultAsync(q => q.AuthorId == command.AuthorId, cancellationToken);
        if (author is null)
        {
            throw new NotFoundException();
        }

        author.UpdateFirstName(command.FirstName);
        author.UpdateLastName(command.LastName);
        await _authorRepository.UpdateAsync(author, cancellationToken);
        await _mediator.ClearResponseCacheAsync(new GetAuthorQuery { AuthorId = command.AuthorId }, cancellationToken);
        return author;
    }
}