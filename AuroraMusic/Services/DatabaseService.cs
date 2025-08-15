using System;
using AuroraMusic.Data;
using AuroraMusic.Models;
using Microsoft.EntityFrameworkCore;
using System.Collections.Generic;
using System.Threading.Tasks;
using System.Linq;

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
        public async Task<(AppSettings settings, bool changesApplied)> InitializeDatabaseAsync(string appVersion)
        {
            using var context = GetContext();
            bool changesApplied = false;

            // Step 1: Ensure the database file and schema are created and migrations applied.
            // This will create the DB if it doesn't exist, and apply any pending migrations.
            // We can't directly check if migrations were applied, so we'll assume if the DB was just created or version updated, it counts.
            var pendingMigrations = await context.Database.GetPendingMigrationsAsync();
            if (pendingMigrations.Any())
            {
                await context.Database.MigrateAsync();
                changesApplied = true;
            }
            else
            {
                // Ensure database is created even if no pending migrations (e.g., first run with no migrations yet)
                await context.Database.EnsureCreatedAsync();
            }

            // Step 2: Get the current settings. If they don't exist, create them.
            var settings = await context.Settings.FirstOrDefaultAsync(s => s.Id == 1);
            if (settings == null)
            {
                settings = new AppSettings();
                context.Settings.Add(settings);
                changesApplied = true; // New settings created
            }

            // Step 3: Check if the database version needs to be updated.
            if (settings.DatabaseVersion == null || new System.Version(settings.DatabaseVersion) < new System.Version(appVersion))
            {
                settings.DatabaseVersion = appVersion;
                changesApplied = true; // Database version updated
            }

            // Step 4: Save any changes (like creating the initial settings or updating the version).
            if (changesApplied)
            {
                await context.SaveChangesAsync();
            }
            return (settings, changesApplied);
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

        public async Task AddFolderAsync(string path)
        {
            using var context = GetContext();
            // Check if the folder already exists
            if (!await context.Folders.AnyAsync(f => f.Path == path))
            {
                var folder = new Folder { Path = path };
                context.Folders.Add(folder);
                await context.SaveChangesAsync();
            }
        }

        public async Task RemoveFolderAsync(string path)
        {
            using var context = GetContext();
            var folderToRemove = await context.Folders.FirstOrDefaultAsync(f => f.Path == path);
            if (folderToRemove != null)
            {
                context.Folders.Remove(folderToRemove);
                // Remove songs associated with this folder
                var songsToRemove = await context.Songs.Where(s => s.FilePath != null && s.FilePath.StartsWith(path)).ToListAsync();
                context.Songs.RemoveRange(songsToRemove);
                await context.SaveChangesAsync();
            }
        }

        public async Task<List<Folder>> GetAllFoldersAsync()
        {
            using var context = GetContext();
            return await context.Folders.ToListAsync();
        }

        public async Task AddPlaylistAsync(Playlist playlist)
        {
            using var context = GetContext();
            context.Playlists.Add(playlist);
            await context.SaveChangesAsync();
        }

        public async Task<List<Playlist>> GetPlaylistsAsync()
        {
            using var context = GetContext();
            return await context.Playlists.Include(p => p.PlaylistItems).ThenInclude(pi => pi.Song).ToListAsync();
        }

        public async Task<Playlist?> GetPlaylistByIdAsync(int id)
        {
            using var context = GetContext();
            return await context.Playlists.Include(p => p.PlaylistItems).ThenInclude(pi => pi.Song).FirstOrDefaultAsync(p => p.Id == id);
        }

        public async Task UpdatePlaylistAsync(Playlist playlist)
        {
            using var context = GetContext();
            context.Playlists.Update(playlist);
            await context.SaveChangesAsync();
        }

        public async Task DeletePlaylistAsync(int id)
        {
            using var context = GetContext();
            var playlistToRemove = await context.Playlists.FindAsync(id);
            if (playlistToRemove != null)
            {
                context.Playlists.Remove(playlistToRemove);
                await context.SaveChangesAsync();
            }
        }

        public async Task AddSongToPlaylistAsync(int playlistId, int songId)
        {
            using var context = GetContext();
            var playlist = await context.Playlists.Include(p => p.PlaylistItems).FirstOrDefaultAsync(p => p.Id == playlistId);
            var song = await context.Songs.FindAsync(songId);

            if (playlist != null && song != null)
            {
                playlist.PlaylistItems.Add(new PlaylistItem { PlaylistId = playlistId, SongId = songId });
                await context.SaveChangesAsync();
            }
        }

        public async Task RemoveSongFromPlaylistAsync(int playlistId, int songId)
        {
            using var context = GetContext();
            var playlistItem = await context.PlaylistItems.FirstOrDefaultAsync(pi => pi.PlaylistId == playlistId && pi.SongId == songId);
            if (playlistItem != null)
            {
                context.PlaylistItems.Remove(playlistItem);
                await context.SaveChangesAsync();
            }
        }

        public async Task<Artist> GetOrCreateArtistAsync(string name)
        {
            using var context = GetContext();
            var artist = await context.Artists.FirstOrDefaultAsync(a => a.Name == name);
            if (artist == null)
            {
                artist = new Artist { Name = name };
                context.Artists.Add(artist);
                await context.SaveChangesAsync();
            }
            return artist;
        }

        public async Task<Album> GetOrCreateAlbumAsync(string title, int artistId, byte[]? albumArt = null)
        {
            using var context = GetContext();
            var album = await context.Albums.FirstOrDefaultAsync(a => a.Title == title && a.ArtistId == artistId);
            if (album == null)
            {
                album = new Album { Title = title, ArtistId = artistId, AlbumArt = albumArt };
                context.Albums.Add(album);
                await context.SaveChangesAsync();
            }
            else if (albumArt != null && album.AlbumArt == null)
            {
                // Update album art if it's missing
                album.AlbumArt = albumArt;
                await context.SaveChangesAsync();
            }
            return album;
        }

        #pragma warning disable CS8602 // Desreferência de uma referência possivelmente nula.
        public async Task<Song> GetOrCreateSongAsync(string title, string filePath, int artistId, int albumId, TimeSpan duration)
        {
            using var context = GetContext();
            var song = await context.Songs
                .Include(s => s.Album)
                    .ThenInclude(a => a.Artist) // Eagerly load Artist within Album
                                .Include(s => s.Artist!) // Eagerly load the Song's direct Artist
                .FirstOrDefaultAsync(s => s.FilePath == filePath);
            if (song == null)
            {
                song = new Song
                {
                    Title = title,
                    FilePath = filePath,
                    ArtistId = artistId,
                    AlbumId = albumId,
                    Duration = duration
                };
                context.Songs.Add(song);
                await context.SaveChangesAsync();
            }
            return song;
        }
#pragma warning restore CS8602 // Desreferência de uma referência possivelmente nula.

        public async Task<IEnumerable<Song>> GetAllSongsAsync()
        {
            using var context = GetContext();
            return await context.Songs.Include(s => s.Artist).ToListAsync();
        }

        public async Task<List<Album>> GetAlbumsAsync()
        {
            using var context = GetContext();
            return await context.Albums
                .Include(a => a.Artist)
                .Where(a => a.Songs.Any())
                .ToListAsync();
        }

        public async Task<List<Artist>> GetArtistsAsync()
        {
            using var context = GetContext();
            return await context.Artists
                .Where(a => a.Songs.Any())
                .ToListAsync();
        }
    }
}