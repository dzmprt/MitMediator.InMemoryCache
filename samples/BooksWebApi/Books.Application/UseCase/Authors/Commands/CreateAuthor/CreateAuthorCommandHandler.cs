using Books.Application.Abstractions.Infrastructure;
using Books.Domain;
using MitMediator;

namespace Books.Application.UseCase.Authors.Commands.CreateAuthor;

/// <summary>
/// Handler for <see cref="CreateAuthorCommand"/>.
/// </summary>
/// <remarks>
/// Initializes a new instance of the <see cref="CreateAuthorCommandHandler"/>.
/// </remarks>
/// <param name="authorRepository">Author repository.</param>
internal sealed class CreateAuthorCommandHandler(IBaseRepository<Author> authorRepository) : IRequestHandler<CreateAuthorCommand, Author>
{
    /// <inheritdoc/>
    /// <returns>The created author.</returns>
    public async ValueTask<Author> HandleAsync(CreateAuthorCommand command, CancellationToken cancellationToken)
    {
        var author = new Author(command.FirstName, command.LastName);
        await authorRepository.AddAsync(author, cancellationToken);
        return author;
    }
}