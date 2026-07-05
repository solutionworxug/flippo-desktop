using Flippo.Data.Services;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;

namespace Flippo.Data;

public static class DependencyInjection
{
    /// <summary>Registriert DbContext-Factory (Plan: AddDbContextFactory) + die drei fachlichen Stores.</summary>
    public static IServiceCollection AddFlippoData(this IServiceCollection services, string connectionString)
    {
        services.AddDbContextFactory<FlippoDbContext>(options => options.UseSqlite(connectionString));
        services.AddSingleton<VocabularyStore>();
        services.AddSingleton<SessionStore>();
        services.AddSingleton<SettingsService>();
        services.AddSingleton<BackupService>();
        services.AddSingleton<FileImportService>();
        services.AddSingleton<UserDictionaryStore>();
        return services;
    }
}
