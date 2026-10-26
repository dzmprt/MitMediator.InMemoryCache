# MitMediator.InMemoryCache

In-memory response caching for [MitMediator](https://github.com/dzmprt/MitMediator)

[![Build and Test](https://github.com/dzmprt/MitMediator.InMemoryCache/actions/workflows/dotnet.yml/badge.svg)](https://github.com/dzmprt/MitMediator.InMemoryCache/actions/workflows/dotnet.yml)
![NuGet](https://img.shields.io/nuget/v/MitMediator.InMemoryCache)
![.NET 10.0](https://img.shields.io/badge/.NET-10.0-informational?style=flat&logo=dotnet)
![License](https://img.shields.io/github/license/dzmprt/MitMediator.InMemoryCache)

## Setup

### 1. Add package

```sh
dotnet add package MitMediator.InMemoryCache -v 10.0.0
```

For a project that only declares requests, use `MitMediator.InMemoryCache.Abstractions` instead:

```sh
dotnet add package MitMediator.InMemoryCache.Abstractions -v 10.0.0
```

### 2. Register services

```csharp
using MitMediator;

builder.Services.AddMitMediator();
builder.Services.AddMitMediatorInMemoryCache();
```

By default, registration scans all loaded assemblies for cache clearing rules.
Only the first call per service collection takes effect. Rules and locks are local to each service provider.

> [!WARNING]
> Make sure `.AddRequestsInMemoryCache()` is registered as the last `IPipelineBehavior`. Cached responses will short-circuit the pipeline and prevent further execution

Optional settings (use instead of the default cache registration):

```csharp
using Microsoft.Extensions.Caching.Memory;

builder.Services.AddMitMediatorInMemoryCache(
    new MemoryCacheOptions { SizeLimit = 1000 },
    lockStripingGatesCount: 128,
    assemblies: [typeof(GetBookQuery).Assembly]);
```

Include all assemblies with decorated requests. The lock count defaults to 64 per request type and must be positive.

## Usage

Add the attribute to a request class or struct. All extensions and the attribute use `MitMediator`:

### No time limit

```csharp
using MitMediator;

[CacheResponseInMemory]
public readonly record struct GetGenresQuery : IRequest<Genre[]>;
```

Stays cached until cleared or evicted.

### Fixed lifetime

```csharp
[CacheResponseInMemory(30)]
public readonly record struct GetBookQuery : IRequest<Book>
{
    public int BookId { get; init; }
}
```

Expires after 30 seconds. Cache hits do not extend this time.

### Collection with a time limit

```csharp
[CacheResponseInMemory(30, 4)]
public readonly record struct GetBooksQuery : IRequest<Book[]>;
```

Expires after 30 seconds. Each book counts as 4 size units: 5 books use 20 units.

### Time limit and clearing on another command

```csharp
[CacheResponseInMemory(30, 1, typeof(UpdateAuthorCommand))]
public readonly record struct GetAuthorQuery : IRequest<Author>
{
    public int AuthorId { get; init; }
}
```

Expires after 30 seconds. A successful `UpdateAuthorCommand` clears all cached authors sooner.

`expirationSeconds` is positive, or `-1` for no time limit.
`entrySize` defaults to 1. For `System.Collections.ICollection`, size is `Count * entrySize`.
`SizeLimit` uses these units, not bytes. There is no capacity limit by default.
The attribute is not inherited.

## Clear the cache

Clear one request value or all values of a request type:

```csharp
await mediator.ClearResponseCacheAsync(
    new GetBookQuery { BookId = bookId },
    cancellationToken);

await mediator.ClearAllResponseCacheAsync<GetBookQuery>(cancellationToken);
```

Await clearing after a successful data change.
Automatic clearing also runs on a cached trigger, but not when downstream execution throws.
The trigger needs no caching attribute.
For `Query<T>`, automatic clearing covers all used variants, such as `Query<int>` and `Query<string>`, plus matching entries added directly to the cache.
Clearing waits for active writes before removing entries.

## Important

- Cache hits skip the handler and later pipeline behaviors. Register validation and authorization before caching.
- Keys use the runtime type's `FullName` and default JSON serialization. Use properties, not fields, for request data. Assembly identity is not part of the type prefix.
- `null` results and exceptions are not cached. Cached objects are not copied; avoid changing them.
- Concurrent misses for the same key run one handler if its result is cached. Different keys may share a lock.
- Cancellation while waiting for a lock stops that caller without running its handler.
- Do not await clearing your own cache inside a cached handler, even through another command. Nested cached requests can also deadlock.
- The cache is local to one process and is lost on restart.

## Samples

- [SimpleConsoleApp](./samples/SimpleConsoleApp)
- [BooksWebApi](./samples/BooksWebApi)

## License

[MIT](./LICENSE)