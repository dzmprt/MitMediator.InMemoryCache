using Books.Application.Abstractions.Infrastructure;
using Books.Application.Exceptions;
using Books.Application.UseCase.Authors.Queries.GetAuthor;
using Books.Domain;
using MitMediator;
using MitMediator.InMemoryCache;

namespace Books.Application.UseCase.Authors.Commands.DeleteAuthor;

/// <summary>
/// Handler for <see cref="DeleteAuthorCommand"/>.
/// </summary>
internal sealed class DeleteAuthorCommandHandler : IRequestHandler<DeleteAuthorCommand>
{
    private readonly IBaseRepository<Author> _authorRepository;
    private readonly IMediator _mediator;

    /// <summary>
    /// Initializes a new instance of the <see cref="DeleteAuthorCommandHandler"/>.
    /// </summary>
    /// <param name="authorRepository">Author repository.</param>
    public DeleteAuthorCommandHandler(IBaseRepository<Author> authorRepository, IMediator mediator)
    {
        _authorRepository = authorRepository;
        _mediator = mediator;
    }

    /// <inheritdoc/>
    public async ValueTask<Unit> HandleAsync(DeleteAuthorCommand command, CancellationToken cancellationToken)
    {
        var author = await _authorRepository.FirstOrDefaultAsync(q => q.AuthorId == command.AuthorId, cancellationToken);
        if (author is null)
        {
            throw new NotFoundException();
        }

        await _authorRepository.RemoveAsync(author, cancellationToken);
        await _mediator.ClearResponseCacheAsync(new GetAuthorQuery { AuthorId = command.AuthorId }, cancellationToken);
        return Unit.Value;
    }
}