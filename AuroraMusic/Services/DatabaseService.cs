using AuroraMusic.Data;
using AuroraMusic.Models;
using Microsoft.EntityFrameworkCore;
using System.Threading.Tasks;

namespace AuroraMusic.Services
{
    /// <summary>
    /// A service to abstract away database operations, including version checking and migrations.
    /// </summary>
    public class DatabaseService
    {
        private MusicDbContext GetContext() => new MusicDbContext();

        /// <summary>
        /// This is the main entry point for database setup. It ensures the database exists,
        /// is migrated to the latest version, and returns the application settings.
        /// </summary>
        /// <param name="appVersion">The current version of the application.</param>
        /// <returns>The application settings, either loaded or newly created.</returns>
        public async Task<AppSettings> InitializeDatabaseAsync(string appVersion)
        {
            using var context = GetContext();

            // Step 1: Ensure the database file and schema are created.
            // This is safe to call every time. It will create the DB if it doesn't exist,
            // and apply any pending migrations if it does. This fixes the "no such table" error.
            await context.Database.MigrateAsync();

            // Step 2: Get the current settings. If they don't exist, create them.
            var settings = await context.Settings.FirstOrDefaultAsync(s => s.Id == 1);
            if (settings == null)
            {
                settings = new AppSettings();
                context.Settings.Add(settings);
            }

            // Step 3: Check if the database version needs to be updated.
            if (new System.Version(settings.DatabaseVersion) < new System.Version(appVersion))
            {
                settings.DatabaseVersion = appVersion;
            }

            // Step 4: Save any changes (like creating the initial settings or updating the version).
            await context.SaveChangesAsync();
            return settings;
        }

        /// <summary>
        /// Saves the provided settings object to the database.
        /// </summary>
        public async Task SaveSettingsAsync(AppSettings settings)
        {
            using var context = GetContext();
            var existingSettings = await context.Settings.FindAsync(1);
            if (existingSettings != null)
            {
                context.Entry(existingSettings).CurrentValues.SetValues(settings);
            }
            else
            {
                context.Settings.Add(settings);
            }
            await context.SaveChangesAsync();
        }
    }
}