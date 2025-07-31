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
        public DbSet<Song> Songs { get; set; }
        public DbSet<Album> Albums { get; set; }
        public DbSet<Artist> Artists { get; set; }

        private readonly string _databasePath;

        public MusicDbContext()
        {
            var folder = Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData);
            var appFolder = Path.Combine(folder, "AuroraMusic");
            Directory.CreateDirectory(appFolder);
            _databasePath = Path.Combine(appFolder, "settings.db");
        }

        protected override void OnConfiguring(DbContextOptionsBuilder options)
            => options.UseSqlite($"Data Source={_databasePath}");

        // This method defines relationships and constraints for our tables.
        protected override void OnModelCreating(ModelBuilder modelBuilder)
        {
            // Ensure that artist names are unique.
            modelBuilder.Entity<Artist>()
                .HasIndex(a => a.Name)
                .IsUnique();

            // Ensure that an album title is unique for a given artist.
            modelBuilder.Entity<Album>()
                .HasIndex(a => new { a.Title, a.ArtistId })
                .IsUnique();

            // Ensure that we don't import the same song file twice.
            modelBuilder.Entity<Song>()
                .HasIndex(s => s.FilePath)
                .IsUnique();
        }
    }
}