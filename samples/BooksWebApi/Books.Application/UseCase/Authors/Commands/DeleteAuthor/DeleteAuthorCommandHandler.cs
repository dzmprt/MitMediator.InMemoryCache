using Books.Application.Abstractions.Infrastructure;
using Books.Application.Exceptions;
using Books.Domain;
using MitMediator;

namespace Books.Application.UseCase.Authors.Commands.DeleteAuthor;

/// <summary>
/// Handler for <see cref="DeleteAuthorCommand"/>.
/// </summary>
/// <remarks>
/// Initializes a new instance of the <see cref="DeleteAuthorCommandHandler"/>.
/// </remarks>
/// <param name="authorRepository">Author repository.</param>
internal sealed class DeleteAuthorCommandHandler(IBaseRepository<Author> authorRepository) : IRequestHandler<DeleteAuthorCommand>
{
    /// <inheritdoc/>
    public async ValueTask<Unit> HandleAsync(DeleteAuthorCommand command, CancellationToken cancellationToken)
    {
        var author = await authorRepository.FirstOrDefaultAsync(q => q.AuthorId == command.AuthorId, cancellationToken);
        if (author is null)
        {
            throw new NotFoundException();
        }
        author.MarkDeleted();
        await authorRepository.RemoveAsync(author, cancellationToken);
        return Unit.Value;
    }
}