# MitMediator.InMemoryCache

## An attribute-driven in-memory caching extension for the [MitMediator](https://github.com/dzmprt/MitMediator)
[![Build and Test](https://github.com/dzmprt/MitMediator.InMemoryCache/actions/workflows/dotnet.yml/badge.svg)](https://github.com/dzmprt/MitMediator.InMemoryCache/actions/workflows/dotnet.yml)
![NuGet](https://img.shields.io/nuget/v/MitMediator.InMemoryCache)
![.NET 9.0](https://img.shields.io/badge/Version-.NET%209.0-informational?style=flat&logo=dotnet)
![NuGet Downloads](https://img.shields.io/nuget/dt/MitMediator.InMemoryCache)
![License](https://img.shields.io/github/license/dzmprt/MitMediator.InMemoryCache)

## Installation

### 1. Install the package

```sh
 dotnet add package MitMediator.InMemoryCache -v 9.0.0
```

### 2. Use extension for `IServiceCollection`

```csharp
// Register handlers and IMediator
builder.Services.AddMitMediator(); 

// Register MemoryCache and InMemoryCacheBehavior
// Read information about all IRequest<>
builder.Services.AddRequestsInMemoryCache()
```
⚠️⚠️⚠️ **Make sure to register `.AddRequestsInMemoryCache()` as the last `IPipelineBehavior`. Cached responses will prevent further pipeline execution**

To customize `MemoryCache` options and specify assemblies to scan:

```csharp
builder.Services.AddRequestsInMemoryCache(
    new MemoryCacheOptions { SizeLimit = 1000 }, 
    new []{typeof(GetQuery).Assembly});`
```

> For `ICollection` types, the cache entry size is `response.Count`; for all other types, it is 1

## Usage

Decorate your request classes with caching attributes:

```csharp
using MitMediator.InMemoryCache;

[CacheForever]
public struct GetGenresQuery : IRequest<Genre[]>;

[CacheForSeconds(10)]
public struct GetBookQuery : IRequest<Book>
{
    public int BookId { get; set; }
}

[CacheUntilSent(typeof(DeleteAuthorCommand), typeof(UpdateAuthorCommand))]
public struct GetAuthorQuery : IRequest<Author>
{
    public int AuthorId { get; init; }
}
```
## Available Attributes

- `[CacheForever]` - caches the response indefinitely
- `[CacheForSeconds(int seconds)]` - caches the response for the specified seconds
- `[CacheUntilSent(params Type[] triggers)]` - caches the response until one of the specified request types is sent

> Responses are cached per unique request data.  
For example, GetAuthorQuery will cache a separate response for each AuthorId

## See [samples](./samples)

## License
MIT
