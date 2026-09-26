using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Design;
using Npgsql;

namespace LoDb.Infrastructure.Persistence;

/// <summary>
/// The context <c>dotnet ef</c> builds to add a migration or print its script, with this
/// project as the startup project.
/// </summary>
/// <remarks>
/// Neither command opens a connection: the server named here never has to exist.
/// </remarks>
public sealed class LoDbDesignTimeFactory : IDesignTimeDbContextFactory<LoDbDbContext>
{
    private const string DesignConnectionString = "Host=localhost;Database=lodb_design";

    public LoDbDbContext CreateDbContext(string[] args)
    {
        var options = new DbContextOptionsBuilder<LoDbDbContext>();
        options.UseLoDb(NpgsqlDataSource.Create(DesignConnectionString));
        return new LoDbDbContext(options.Options);
    }
}
