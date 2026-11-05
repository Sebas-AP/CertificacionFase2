using Microsoft.EntityFrameworkCore;
using RioClaro.Domain;
using RioClaro.Domain.Entities;

namespace RioClaro.Infrastructure.Persistence;

public static class DemoDataSeeder
{
    public static async Task SeedAsync(
        RioClaroDbContext dbContext,
        CancellationToken cancellationToken = default)
    {
        if (!await dbContext.Organizations.AnyAsync(
                organization => organization.Id == DemoIdentity.OrganizationId,
                cancellationToken))
        {
            dbContext.Organizations.Add(
                new Organization(DemoIdentity.OrganizationId, DemoIdentity.OrganizationName));
        }

        if (!await dbContext.Users.AnyAsync(
                user => user.Id == DemoIdentity.UserId,
                cancellationToken))
        {
            dbContext.Users.Add(new ApplicationUser(
                DemoIdentity.UserId,
                DemoIdentity.OrganizationId,
                DemoIdentity.UserDisplayName,
                DemoIdentity.Role));
        }

        await dbContext.SaveChangesAsync(cancellationToken);
    }
}
