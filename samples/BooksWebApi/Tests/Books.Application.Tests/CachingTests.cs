using System.Diagnostics;
using System.Linq.Expressions;
using Books.Application.Abstractions.Infrastructure;
using Books.Application.UseCase.Authors.Commands.DeleteAuthor;
using Books.Application.UseCase.Authors.Commands.UpdateAuthor;
using Books.Application.UseCase.Authors.Queries.GetAuthor;
using Books.Application.UseCase.Authors.Queries.GetAuthorsByFilter;
using Books.Application.UseCase.Books.Commands.CreateBook;
using Books.Application.UseCase.Books.Commands.DeleteBook;
using Books.Application.UseCase.Books.Commands.UpdateBook;
using Books.Application.UseCase.Books.Queries.GetBook;
using Books.Application.UseCase.Books.Queries.GetBooksByFilter;
using Books.Application.UseCase.Genres.Commands.CreateGenre;
using Books.Application.UseCase.Genres.Commands.DeleteGenre;
using Books.Application.UseCase.Genres.Queries.GetGenres;
using Books.Domain;
using Microsoft.Extensions.DependencyInjection;
using MitMediator;
using Moq;

namespace Books.Application.Tests;

public sealed class CachingTests
{
    private const int HandlerDelayMilliseconds = 1_000;
    private static readonly TimeSpan HandlerDelayTolerance = TimeSpan.FromMilliseconds(HandlerDelayMilliseconds - 500);

    [Fact]
    public async Task GetBooksByFilter_CachesResponseForEachRequest()
    {
        var provider = CreateBookProvider(new Book(
            "Clean Architecture",
            new Author("Robert", "Martin"),
            new Genre("Software")));
        await using var serviceProvider = CreateServiceProvider(
            services => services.AddSingleton(provider.Object));
        var mediator = serviceProvider.GetRequiredService<IMediator>();
        var query = new GetBooksByFilterQuery { Limit = 10, Offset = 0, FreeText = "architecture" };

        await AssertUsesCachedResponseAsync(
            () => mediator.SendAsync<GetBooksByFilterQuery, Book[]>(query, CancellationToken.None));

        await mediator.SendAsync<GetBooksByFilterQuery, Book[]>(
            query with { FreeText = "clean" },
            CancellationToken.None);

        provider.Verify(
            x => x.SearchAsync(
                It.IsAny<Expression<Func<Book, bool>>?>(),
                It.IsAny<Expression<Func<Book, int>>>(),
                10,
                0,
                It.IsAny<CancellationToken>()),
            Times.Exactly(2));
    }

    [Fact]
    public async Task GetAuthorsByFilter_CachesSlowHandlerResponse()
    {
        var author = new Author("Ada", "Lovelace");
        var provider = CreateAuthorProvider(author);
        await using var serviceProvider = CreateServiceProvider(
            services => services.AddSingleton(provider.Object));
        var mediator = serviceProvider.GetRequiredService<IMediator>();
        var query = new GetAuthorsByFilterQuery
        {
            Limit = 10,
            Offset = 0,
            FreeText = "Ada"
        };

        await AssertUsesCachedResponseAsync(
            () => mediator.SendAsync<GetAuthorsByFilterQuery, Author[]>(query, CancellationToken.None));

        provider.Verify(
            x => x.SearchAsync(
                It.IsAny<Expression<Func<Author, bool>>?>(),
                It.IsAny<Expression<Func<Author, int>>>(),
                10,
                0,
                It.IsAny<CancellationToken>()),
            Times.Once);
    }

    [Theory]
    [InlineData(BookChangingCommand.Create)]
    [InlineData(BookChangingCommand.Update)]
    [InlineData(BookChangingCommand.Delete)]
    public async Task BookChangingCommand_ClearsCachedBookList(BookChangingCommand commandToSend)
    {
        var author = new Author("Robert", "Martin");
        var genre = new Genre("Software");
        var book = new Book("Clean Architecture", author, genre);
        var provider = CreateBookProvider(book);
        var books = CreateRepository(book);
        var authors = CreateRepository(author);
        var genres = CreateRepository(genre);

        await using var serviceProvider = CreateServiceProvider(services =>
        {
            services.AddSingleton(provider.Object);
            services.AddSingleton(books.Object);
            services.AddSingleton(authors.Object);
            services.AddSingleton(genres.Object);
        });
        var mediator = serviceProvider.GetRequiredService<IMediator>();
        var query = new GetBooksByFilterQuery { Limit = 10, Offset = 0 };

        await AssertUsesCachedResponseAsync(
            () => mediator.SendAsync<GetBooksByFilterQuery, Book[]>(query, CancellationToken.None));

        provider.Verify(
            x => x.SearchAsync(
                It.IsAny<Expression<Func<Book, bool>>?>(),
                It.IsAny<Expression<Func<Book, int>>>(),
                10,
                0,
                It.IsAny<CancellationToken>()),
            Times.Once);

        switch (commandToSend)
        {
            case BookChangingCommand.Create:
                await mediator.SendAsync<CreateBookCommand, Book>(
                    new CreateBookCommand
                    {
                        Title = "Clean Code",
                        AuthorId = 1,
                        GenreName = genre.GenreName
                    },
                    CancellationToken.None);
                break;
            case BookChangingCommand.Update:
                await mediator.SendAsync<UpdateBookCommand, Book>(
                    new UpdateBookCommand
                    {
                        BookId = 1,
                        Title = "Clean Code",
                        AuthorId = 1,
                        GenreName = genre.GenreName
                    },
                    CancellationToken.None);
                break;
            case BookChangingCommand.Delete:
                await mediator.SendAsync(new DeleteBookCommand { BookId = 1 }, CancellationToken.None);
                break;
            default:
                throw new ArgumentOutOfRangeException(nameof(commandToSend), commandToSend, null);
        }

        await mediator.SendAsync<GetBooksByFilterQuery, Book[]>(query, CancellationToken.None);
        provider.Verify(
            x => x.SearchAsync(
                It.IsAny<Expression<Func<Book, bool>>?>(),
                It.IsAny<Expression<Func<Book, int>>>(),
                10,
                0,
                It.IsAny<CancellationToken>()),
            Times.Exactly(2));
    }

    [Fact]
    public async Task UpdateBook_ClearsCachedBookById()
    {
        var author = new Author("Robert", "Martin");
        var genre = new Genre("Software");
        var book = new Book("Clean Architecture", author, genre);
        var provider = CreateBookProvider(book);
        var books = CreateRepository(book);
        var authors = CreateRepository(author);
        var genres = CreateRepository(genre);

        await using var serviceProvider = CreateServiceProvider(services =>
        {
            services.AddSingleton(provider.Object);
            services.AddSingleton(books.Object);
            services.AddSingleton(authors.Object);
            services.AddSingleton(genres.Object);
        });
        var mediator = serviceProvider.GetRequiredService<IMediator>();
        var query = new GetBookQuery { BookId = 1 };

        await AssertUsesCachedResponseAsync(
            () => mediator.SendAsync<GetBookQuery, Book>(query, CancellationToken.None));

        await mediator.SendAsync<UpdateBookCommand, Book>(
            new UpdateBookCommand
            {
                BookId = 1,
                Title = "Clean Code",
                AuthorId = 1,
                GenreName = genre.GenreName
            },
            CancellationToken.None);
        await mediator.SendAsync<GetBookQuery, Book>(query, CancellationToken.None);

        provider.Verify(
            x => x.FirstOrDefaultAsync(
                It.IsAny<Expression<Func<Book, bool>>?>(),
                It.IsAny<CancellationToken>()),
            Times.Exactly(2));
    }

    [Fact]
    public async Task UpdateAuthor_ClearsCachedAuthorById()
    {
        var author = new Author("Ada", "Lovelace");
        var provider = CreateProvider(author);
        var authors = CreateRepository(author);

        await using var serviceProvider = CreateServiceProvider(services =>
        {
            services.AddSingleton(provider.Object);
            services.AddSingleton(authors.Object);
        });
        var mediator = serviceProvider.GetRequiredService<IMediator>();
        var query = new GetAuthorQuery { AuthorId = 1 };

        await AssertUsesCachedResponseAsync(
            () => mediator.SendAsync<GetAuthorQuery, Author>(query, CancellationToken.None));

        await mediator.SendAsync<UpdateAuthorCommand, Author>(
            new UpdateAuthorCommand
            {
                AuthorId = 1,
                FirstName = "Augusta Ada",
                LastName = "Lovelace"
            },
            CancellationToken.None);
        await mediator.SendAsync<GetAuthorQuery, Author>(query, CancellationToken.None);

        provider.Verify(
            x => x.FirstOrDefaultAsync(
                It.IsAny<Expression<Func<Author, bool>>?>(),
                It.IsAny<CancellationToken>()),
            Times.Exactly(2));
    }

    [Theory]
    [InlineData(GenreChangingCommand.Create)]
    [InlineData(GenreChangingCommand.Delete)]
    public async Task GenreChangingCommand_ClearsCachedGenres(GenreChangingCommand commandToSend)
    {
        var genre = new Genre("Software");
        var provider = CreateProvider<Genre>([genre], null);
        var genres = CreateRepository(genre);
        genres.Setup(x => x.AnyAsync(
                It.IsAny<Expression<Func<Genre, bool>>?>(),
                It.IsAny<CancellationToken>()))
            .Returns(new ValueTask<bool>(false));

        await using var serviceProvider = CreateServiceProvider(services =>
        {
            services.AddSingleton(provider.Object);
            services.AddSingleton(genres.Object);
        });
        var mediator = serviceProvider.GetRequiredService<IMediator>();

        await AssertUsesCachedResponseAsync(
            () => mediator.SendAsync<GetGenresQuery, Genre[]>(
                new GetGenresQuery(),
                CancellationToken.None));

        if (commandToSend == GenreChangingCommand.Create)
        {
            await mediator.SendAsync<CreateGenreCommand, Genre>(
                new CreateGenreCommand { GenreName = "Fantasy" },
                CancellationToken.None);
        }
        else
        {
            await mediator.SendAsync(
                new DeleteGenreCommand { GenreName = genre.GenreName },
                CancellationToken.None);
        }

        await mediator.SendAsync<GetGenresQuery, Genre[]>(
            new GetGenresQuery(),
            CancellationToken.None);
        provider.Verify(
            x => x.GetAllAsync(It.IsAny<CancellationToken>()),
            Times.Exactly(2));
    }

    public enum BookChangingCommand
    {
        Create,
        Update,
        Delete
    }

    public enum GenreChangingCommand
    {
        Create,
        Delete
    }

    private static ServiceProvider CreateServiceProvider(Action<IServiceCollection> registerDependencies)
    {
        var services = new ServiceCollection();
        services.AddApplicationServices();
        registerDependencies(services);
        return services.BuildServiceProvider();
    }

    private static async Task AssertUsesCachedResponseAsync<TResponse>(Func<ValueTask<TResponse>> send)
        where TResponse : class
    {
        var (first, firstDuration) = await MeasureAsync(send);
        var (cached, cachedDuration) = await MeasureAsync(send);

        Assert.Same(first, cached);
        Assert.True(
            firstDuration >= HandlerDelayTolerance,
            $"The first response should execute the delayed handler, but took {firstDuration.TotalMilliseconds:F0} ms.");
        Assert.True(
            cachedDuration < HandlerDelayTolerance,
            $"The cached response should skip the delayed handler, but took {cachedDuration.TotalMilliseconds:F0} ms.");
    }

    private static async Task<(TResponse Response, TimeSpan Duration)> MeasureAsync<TResponse>(
        Func<ValueTask<TResponse>> action)
    {
        var stopwatch = Stopwatch.StartNew();
        var response = await action();
        stopwatch.Stop();
        return (response, stopwatch.Elapsed);
    }

    private static Mock<IBaseProvider<Book>> CreateBookProvider(Book book)
    {
        var provider = CreateProvider(book);
        provider.Setup(x => x.SearchAsync(
                It.IsAny<Expression<Func<Book, bool>>?>(),
                It.IsAny<Expression<Func<Book, int>>?>(),
                It.IsAny<int?>(),
                It.IsAny<int?>(),
                It.IsAny<CancellationToken>()))
            .Returns(new ValueTask<Book[]>([book]));
        return provider;
    }

    private static Mock<IBaseProvider<Author>> CreateAuthorProvider(Author author)
    {
        var provider = new Mock<IBaseProvider<Author>>();
        provider.Setup(x => x.SearchAsync(
                It.IsAny<Expression<Func<Author, bool>>?>(),
                It.IsAny<Expression<Func<Author, int>>?>(),
                It.IsAny<int?>(),
                It.IsAny<int?>(),
                It.IsAny<CancellationToken>()))
            .Returns(new ValueTask<Author[]>([author]));
        return provider;
    }

    private static Mock<IBaseProvider<T>> CreateProvider<T>(T value)
        where T : class
    {
        var provider = new Mock<IBaseProvider<T>>();
        provider.Setup(x => x.FirstOrDefaultAsync(
                It.IsAny<Expression<Func<T, bool>>?>(),
                It.IsAny<CancellationToken>()))
            .Returns(new ValueTask<T?>(value));
        return provider;
    }

    private static Mock<IBaseProvider<T>> CreateProvider<T>(T[] values, T? _)
        where T : class
    {
        var provider = new Mock<IBaseProvider<T>>();
        provider.Setup(x => x.GetAllAsync(It.IsAny<CancellationToken>()))
            .Returns(new ValueTask<T[]>(values));
        return provider;
    }

    private static Mock<IBaseRepository<T>> CreateRepository<T>(T value)
        where T : class
    {
        var repository = new Mock<IBaseRepository<T>>();
        repository.Setup(x => x.FirstOrDefaultAsync(
                It.IsAny<Expression<Func<T, bool>>?>(),
                It.IsAny<CancellationToken>()))
            .Returns(new ValueTask<T?>(value));
        repository.Setup(x => x.AddAsync(It.IsAny<T>(), It.IsAny<CancellationToken>()))
            .Returns((T entity, CancellationToken _) => new ValueTask<T>(entity));
        repository.Setup(x => x.UpdateAsync(It.IsAny<T>(), It.IsAny<CancellationToken>()))
            .Returns((T entity, CancellationToken _) => new ValueTask<T>(entity));
        repository.Setup(x => x.RemoveAsync(It.IsAny<T>(), It.IsAny<CancellationToken>()))
            .Returns(ValueTask.CompletedTask);
        return repository;
    }
}
