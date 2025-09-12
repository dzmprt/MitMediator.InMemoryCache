using Books.Domain;
using MitMediator;
using MitMediator.InMemoryCache;

namespace Books.Application.UseCase.Genres.Queries.GetGenres;

/// <summary>
/// Get genres query.
/// </summary>
[CacheForever]
public struct GetGenresQuery : IRequest<Genre[]>;