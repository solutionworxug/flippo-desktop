using Microsoft.EntityFrameworkCore;

namespace Flippo.Data;

/// <summary>Migration beim Start + WAL-Modus (persistiert im DB-Header, einmal genügt).</summary>
public static class DatabaseInitializer
{
    public static void Initialize(FlippoDbContext db)
    {
        db.Database.Migrate();
        db.Database.ExecuteSqlRaw("PRAGMA journal_mode=WAL;");
    }
}
