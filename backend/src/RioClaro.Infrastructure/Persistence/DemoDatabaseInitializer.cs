using Microsoft.EntityFrameworkCore;

namespace RioClaro.Infrastructure.Persistence;

public sealed class DemoDatabaseInitializer
{
    private readonly RioClaroDbContext _dbContext;

    public DemoDatabaseInitializer(RioClaroDbContext dbContext)
    {
        _dbContext = dbContext;
    }

    public async Task InitializeAsync(CancellationToken cancellationToken = default)
    {
        await _dbContext.Database.MigrateAsync(cancellationToken);
        await DemoDataSeeder.SeedAsync(_dbContext, cancellationToken);
    }
}
