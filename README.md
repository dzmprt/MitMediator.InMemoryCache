# MitMediator.InMemoryCache

## An attribute-driven in-memory caching extension for the [MitMediator](https://github.com/dzmprt/MitMediator)

[![Build and Test](https://github.com/dzmprt/MitMediator.InMemoryCache/actions/workflows/dotnet.yml/badge.svg)](https://github.com/dzmprt/MitMediator.InMemoryCache/actions/workflows/dotnet.yml)
![NuGet](https://img.shields.io/nuget/v/MitMediator.InMemoryCache)
![.NET 9.0](https://img.shields.io/badge/Version-.NET%209.0-informational?style=flat&logo=dotnet)
![NuGet Downloads](https://img.shields.io/nuget/dt/MitMediator.InMemoryCache)
![License](https://img.shields.io/github/license/dzmprt/MitMediator.InMemoryCache)

## Installation

### 1. Add package

```sh
 dotnet add package MitMediator.InMemoryCache -v 9.0.0-alfa-2
```

### 2. Register services

```csharp
// Register handlers and IMediator
builder.Services.AddMitMediator(); 

// Register MemoryCache, InMemoryCacheBehavior,
// scan information about all IRequest<>
builder.Services.AddRequestsInMemoryCache()
```

> [!WARNING]
> Make sure `.AddRequestsInMemoryCache()` is registered as the last `IPipelineBehavior`. Cached responses will short-circuit the pipeline and prevent further execution**

To customize `MemoryCache` options and specify assemblies to scan:

```csharp
builder.Services.AddRequestsInMemoryCache(
    new MemoryCacheOptions { SizeLimit = 1000 }, 
    new []{typeof(GetQuery).Assembly});`
```

## Usage

Decorate your request classes with attribute `[CacheResponse]`

Requests decorated with the `[CacheResponse]` attribute will have their responses cached in memory. You can control expiration, entry size, and define which requests should invalidate the cache

### CacheResponseAttribute params

| Name                   | Description                                                                                           |
|------------------------|-------------------------------------------------------------------------------------------------------|
| `expirationSeconds`    | Absolute expiration time in seconds, relative to the current moment. Set null for indefinitely        |
| `entrySize`            | The size of the cache entry item. For collections, size is calculated per element. Default value is 1 |
| `requestsToClearCache` | Types of requests that will trigger cache invalidation                                                |

Use `IMediator` extension methods to clear cached responses:

Clear cache for specific request data:
```csharp
mediator.ClearResponseCacheAsync(request, ct);
```

Clear all cached responses for a request type
```csharp
mediator.ClearAllResponseCacheAsync<GetBookQuery>(ct);
```

## Example usage

Cache indefinitely:
```csharp
[CacheResponse]
public struct GetGenresQuery : IRequest<Genre[]>;
```

> Default `entrySize` is 1

Cache for 10 seconds:
```csharp
[CacheResponse(10)]
public struct GetBookQuery : IRequest<Book>
{
    public int BookId { get; set; }
}
```

Invalidate cache on specific requests:
```csharp
[CacheResponse(typeof(DeleteAuthorCommand), typeof(UpdateAuthorCommand))]
public struct GetAuthorsByFilterQuery : IRequest<Author[]>
{
    public int? Limit { get; init; }
    
    public int? Offset { get; init; }
    
    public string? FreeText { get; init; }
}
```

Set custom entry size and time (30s)
```csharp
[CacheResponse(30, 4)]
public struct GetBooksByFilterQuery : IRequest<Book[]>
{
    public int? Limit { get; init; }
    
    public int? Offset { get; init; }
    
    public string? FreeText { get; init; }
}
```

> For `ICollection` types, the cache entry size is `response.Count * entrySize`

Clear cache after updating data:
```csharp
public async ValueTask<Book> HandleAsync(UpdateBookTitleCommand command, CancellationToken cancellationToken)
{
    var book = await _booksRepository.FirstOrDefaultAsync(b => b.BookId == command.BookId, cancellationToken);
        
    book.SetTitle(command.Title);

    await _booksRepository.UpdateAsync(book, cancellationToken);
    
    // Clear cached response for the updated book
    await _mediator.ClearResponseCacheAsync(new GetBookQuery() { BookId = command.BookId }, cancellationToken);
    return book;
}
```

> Responses are cached per unique request data.
> For example, `GetBookQuery` caches a separate response for each `BookId`

## See [samples](./samples)

## License

MIT




