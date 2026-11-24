using Books.Application.Abstractions.Infrastructure;
using Books.Application.Exceptions;
using Books.Domain;
using MitMediator;

namespace Books.Application.UseCase.Genres.Commands.DeleteGenre;

/// <summary>
/// Handler for <see cref="DeleteGenreCommand"/>.
/// </summary>
/// <remarks>
/// Initializes a new instance of the <see cref="DeleteGenreCommandHandler"/>.
/// </remarks>
/// <param name="genreRepository">Genre repository.</param>
internal sealed class DeleteGenreCommandHandler(IBaseRepository<Genre> genreRepository) : IRequestHandler<DeleteGenreCommand>
{
    /// <inheritdoc/>
    public async ValueTask<Unit> HandleAsync(DeleteGenreCommand command, CancellationToken cancellationToken)
    {
        var genreName = command.GenreName.ToUpperInvariant();
        var genre = await genreRepository.FirstOrDefaultAsync(q => q.GenreName == genreName, cancellationToken);
        if (genre is null)
        {
            throw new NotFoundException();
        }
        genre.MarkDeleted();
        await genreRepository.RemoveAsync(genre, cancellationToken);
        return Unit.Value;
    }
}