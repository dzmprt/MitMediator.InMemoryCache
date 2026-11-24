using Books.Domain;
using Books.Application.UseCase.Genres.Commands.CreateGenre;
using Books.Application.UseCase.Genres.Commands.DeleteGenre;
using MitMediator;
using MitMediator.InMemoryCache;

namespace Books.Application.UseCase.Genres.Queries.GetGenres;

/// <summary>
/// Get genres query.
/// </summary>
[CacheResponseInMemory(typeof(CreateGenreCommand), typeof(DeleteGenreCommand))]
public struct GetGenresQuery : IRequest<Genre[]>;