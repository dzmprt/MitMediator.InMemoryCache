using Books.Application.Abstractions.Infrastructure;
using Books.Domain;
using MitMediator;

namespace Books.Application.UseCase.Genres.Queries.GetGenres;

/// <summary>
/// Handler for <see cref="GetGenresQuery"/>.
/// </summary>
/// <remarks>
/// Initializes a new instance of the <see cref="GetGenresQueryHandler"/>.
/// </remarks>
/// <param name="genreProvider">Genre provider.</param>
internal sealed class GetGenresQueryHandler(IBaseProvider<Genre> genreProvider) : IRequestHandler<GetGenresQuery, Genre[]>
{
    /// <inheritdoc/>
    public ValueTask<Genre[]> HandleAsync(GetGenresQuery request, CancellationToken cancellationToken)
    {
        Thread.Sleep(1000);
        return genreProvider.GetAllAsync(cancellationToken);
    }
}