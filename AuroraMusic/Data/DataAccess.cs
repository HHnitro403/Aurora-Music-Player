using AuroraMusic.Models;
using Microsoft.EntityFrameworkCore;
using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace AuroraMusic.Data
{
    /// <summary>
    /// The Entity Framework database context for our application.
    /// </summary>
    public class MusicDbContext : DbContext
    {
        public DbSet<AppSettings> Settings { get; set; }

        private readonly string _databasePath;

        public MusicDbContext()
        {
            // Store the database in the user's local app data folder for persistence.
            var folder = Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData);
            var appFolder = Path.Combine(folder, "AuroraMusic");
            Directory.CreateDirectory(appFolder); // Ensure the directory exists
            _databasePath = Path.Combine(appFolder, "Settings.db");
        }

        // Configure the context to use the SQLite database.
        protected override void OnConfiguring(DbContextOptionsBuilder options)
            => options.UseSqlite($"Data Source={_databasePath}");
    }
}