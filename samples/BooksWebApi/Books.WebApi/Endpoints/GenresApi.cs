using Books.Application.UseCase.Genres.Queries.GetGenres;
using Books.Domain;
using Microsoft.AspNetCore.Mvc;
using MitMediator;

namespace Books.WebApi.Endpoints;

/// <summary>
/// Genres api endpoints.
/// </summary>
public static class GenresApi
{
    private const string Tag = "genres";
    
    private const string ApiUrl = "api";
    
    private const string Version = "v1";
    
    /// <summary>
    /// Use genres api endpoints.
    /// </summary>
    /// <param name="app"></param>
    /// <returns></returns>
    public static WebApplication UseGenresApi(this WebApplication app)
    {
        #region Queries
        
        app.MapGet($"{ApiUrl}/{Version}/{Tag}", GetAllGenresAsync)
            .WithTags(Tag)
            .WithName("Get all genres.")
            .WithGroupName(Version)
            .Produces<Genre[]>();
        
        #endregion

        return app;
    }
    
    private static ValueTask<Genre[]> GetAllGenresAsync([FromServices] IMediator mediator, CancellationToken cancellationToken)
    {
        return mediator.SendAsync<GetGenresQuery, Genre[]>(new GetGenresQuery(), cancellationToken);
    }
}