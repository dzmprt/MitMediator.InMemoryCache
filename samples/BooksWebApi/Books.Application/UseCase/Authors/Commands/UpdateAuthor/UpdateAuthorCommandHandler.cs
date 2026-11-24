using Books.Application.Abstractions.Infrastructure;
using Books.Application.Exceptions;
using Books.Domain;
using MitMediator;

namespace Books.Application.UseCase.Authors.Commands.UpdateAuthor;

/// <summary>
/// Handler for <see cref="UpdateAuthorCommand"/>.
/// </summary>
/// <remarks>
/// Initializes a new instance of the <see cref="UpdateAuthorCommandHandler"/>.
/// </remarks>
/// <param name="authorRepository">Author repository.</param>
internal sealed class UpdateAuthorCommandHandler(IBaseRepository<Author> authorRepository) : IRequestHandler<UpdateAuthorCommand, Author>
{
    /// <inheritdoc/>
    /// <returns>The updated author.</returns>
    public async ValueTask<Author> HandleAsync(UpdateAuthorCommand command, CancellationToken cancellationToken)
    {
        var author = await authorRepository.FirstOrDefaultAsync(q => q.AuthorId == command.AuthorId, cancellationToken);
        if (author is null)
        {
            throw new NotFoundException();
        }
        author.UpdateName(command.FirstName, command.LastName);
        await authorRepository.UpdateAsync(author, cancellationToken);
        return author;
    }
}