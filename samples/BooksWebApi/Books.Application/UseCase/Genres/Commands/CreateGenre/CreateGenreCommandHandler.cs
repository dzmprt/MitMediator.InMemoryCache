using Books.Application.Abstractions.Infrastructure;
using Books.Application.Exceptions;
using Books.Application.UseCase.Authors.Commands.CreateAuthor;
using Books.Domain;
using MitMediator;

namespace Books.Application.UseCase.Genres.Commands.CreateGenre;

/// <summary>
/// Handler for <see cref="CreateGenreCommand"/>.
/// </summary>
/// <remarks>
/// Initializes a new instance of the <see cref="CreateAuthorCommandHandler"/>.
/// </remarks>
/// <param name="genreRepository">Genre repository.</param>
internal sealed class CreateGenreCommandHandler(IBaseRepository<Genre> genreRepository) : IRequestHandler<CreateGenreCommand, Genre>
{
    /// <inheritdoc/>
    public async ValueTask<Genre> HandleAsync(CreateGenreCommand command, CancellationToken cancellationToken)
    {
        var isGenreExists = await genreRepository.AnyAsync(g => g.GenreName == command.GenreName.Trim().ToUpperInvariant(), cancellationToken);
        if (isGenreExists)
        {
            throw new BadOperationException($"Genre '{command.GenreName}' already exists.");
        }
        var genre = new Genre(command.GenreName);
        await genreRepository.AddAsync(genre, cancellationToken);
        return genre;
    }
}