using HotChocolate.Pagination;
using HotChocolate.Pagination.Expressions;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Diagnostics;

namespace HotChocolateCursorPagingBugRepro;

public class UnitTest
{
    [Fact]
    public async Task CursorParserParse_WhenOrderByValueContainsColon()
    {
        await using var context = new MyDbContext(new DbContextOptionsBuilder<MyDbContext>()
            .UseInMemoryDatabase("Test")
            .ConfigureWarnings(b => b.Ignore(InMemoryEventId.TransactionIgnoredWarning))
            .Options);

        await context.Database.EnsureDeletedAsync();
        await context.Database.EnsureCreatedAsync();

        context.PagingEntities.Add(new PagingEntity { Title = "test title" });
        context.PagingEntities.Add(new PagingEntity { Title = "A. Title : I contain a colon" });

        await context.SaveChangesAsync();

        var query = context.PagingEntities
            .OrderBy(x => x.Title)
            .ThenBy(x => x.Id);

        var firstPage = await query.ToPageAsync(new PagingArguments { First = 1 });
        var cursor = firstPage.CreateCursor(firstPage.First!);

        var parser = new CursorKeyParser();
        parser.Visit(query.Expression);
        
        // CursorParse.Parse ignores the escaped colon 
        // so it reads the first key as: A. Title \
        // the second key as: I contain a colon
        var parsedCursor = CursorParser.Parse(cursor,  parser.Keys.ToArray());
    }

    private record PagingEntity
    {
        public Guid Id { get; init; } = Guid.NewGuid();
        
        public required string Title { get; init; }
    }
    
    private class MyDbContext(DbContextOptions options)
        : DbContext(options)
    {
        public DbSet<PagingEntity> PagingEntities { get; init; }
    }
}
