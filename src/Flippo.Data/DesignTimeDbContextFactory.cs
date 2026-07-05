using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Design;

namespace Flippo.Data;

/// <summary>Nur für <c>dotnet ef migrations</c> — nutzt eine Wegwerf-DB-Datei.</summary>
public class DesignTimeDbContextFactory : IDesignTimeDbContextFactory<FlippoDbContext>
{
    public FlippoDbContext CreateDbContext(string[] args)
    {
        var options = new DbContextOptionsBuilder<FlippoDbContext>()
            .UseSqlite("Data Source=flippo-design.db")
            .Options;
        return new FlippoDbContext(options);
    }
}
