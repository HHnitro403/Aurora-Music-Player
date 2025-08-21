
using AuroraMusic.Models;
using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Threading.Tasks;
using Avalonia.Media.Imaging;
using TagLib;
using Microsoft.EntityFrameworkCore;

namespace AuroraMusic.Services
{
    public class PlaylistManager
    {
        private readonly DatabaseService _dbService;
        private readonly Random _random;
        private List<PlaylistItem> _masterPlaylist = new List<PlaylistItem>();
        private List<PlaylistItem> _currentQueue = new List<PlaylistItem>();
        private int _currentQueueIndex = -1;
        private bool _isShuffleActive = false;

        public IReadOnlyList<PlaylistItem> CurrentQueue => _currentQueue;
        public int CurrentQueueIndex => _currentQueueIndex;

        public PlaylistManager(DatabaseService dbService)
        {
            _dbService = dbService;
            _random = new Random();
        }

        public async Task LoadMusicFilesAsync(IEnumerable<Folder> folders)
        {
            var supportedExtensions = new[] { ".mp3", ".flac", ".m4a", ".mp4" };
            var loadedPlaylist = new List<PlaylistItem>();

            foreach (var folder in folders)
            {
                if (Directory.Exists(folder.Path))
                {
                    var files = Directory.EnumerateFiles(folder.Path, "*.*", SearchOption.AllDirectories)
                                         .Where(f => supportedExtensions.Contains(Path.GetExtension(f).ToLower()));

                    foreach (var file in files)
                    {
                        try
                        {
                            using var tagFile = TagLib.File.Create(file);

                            var artistName = string.IsNullOrWhiteSpace(tagFile.Tag.FirstPerformer) ? "Unknown Artist" : tagFile.Tag.FirstPerformer;
                            var albumTitle = string.IsNullOrWhiteSpace(tagFile.Tag.Album) ? "Unknown Album" : tagFile.Tag.Album;
                            var albumArtistName = string.IsNullOrWhiteSpace(tagFile.Tag.FirstAlbumArtist) ? "Unknown Artist" : tagFile.Tag.FirstAlbumArtist;

                            // Get or create Artist
                            var artist = await _dbService.GetOrCreateArtistAsync(artistName);

                            // Get or create Album Artist
                            var albumArtist = await _dbService.GetOrCreateArtistAsync(albumArtistName);

                            // Get or create Album
                            var album = await _dbService.GetOrCreateAlbumAsync(albumTitle, albumArtist.Id, tagFile.Tag.Pictures.Length > 0 ? tagFile.Tag.Pictures[0].Data.Data : null);

                            // Get or create Song
                            var song = await _dbService.GetOrCreateSongAsync(
                                title: string.IsNullOrWhiteSpace(tagFile.Tag.Title) ? Path.GetFileNameWithoutExtension(file) : tagFile.Tag.Title,
                                filePath: file,
                                artistId: artist.Id,
                                albumId: album.Id,
                                duration: tagFile.Properties.Duration
                            );

                            loadedPlaylist.Add(new PlaylistItem { Song = song });
                        }
                        catch (Exception ex)
                        {
                            Serilog.Log.Error(ex, "Error loading music file: {FilePath}", file);
                        }
                    }
                }
            }
            _masterPlaylist = loadedPlaylist;
        }

        public IEnumerable<PlaylistItem> GetSortedPlaylist(SortMode sortMode)
        {
            return sortMode switch
            {
                SortMode.Alphabetical => _masterPlaylist.OrderBy(p => p.Song?.Title),
                SortMode.Album => _masterPlaylist.OrderBy(p => p.Song?.Album?.Title).ThenBy(p => p.Song?.Title),
                SortMode.Artist => _masterPlaylist.OrderBy(p => p.Song?.Artist?.Name).ThenBy(p => p.Song?.Title),
                SortMode.ArtistAlbum => _masterPlaylist.OrderBy(p => p.Song?.Artist?.Name).ThenBy(p => p.Song?.Album?.Title).ThenBy(p => p.Song?.Title),
                _ => _masterPlaylist.OrderBy(p => p.Song?.Artist?.Name).ThenBy(p => p.Song?.Album?.Title).ThenBy(p => p.Song?.Title),
            };
        }

        public void SetQueue(IEnumerable<PlaylistItem> playlist, PlaylistItem selectedItem)
        {
            _currentQueue = playlist.ToList();
            _currentQueueIndex = _currentQueue.IndexOf(selectedItem);
        }

        public PlaylistItem? GetNextSong(RepeatMode repeatMode)
        {
            if (_currentQueue.Count == 0) return null;

            if (repeatMode == RepeatMode.RepeatTrack)
            {
                return _currentQueue[_currentQueueIndex];
            }

            _currentQueueIndex++;
            if (_currentQueueIndex >= _currentQueue.Count)
            {
                if (repeatMode == RepeatMode.RepeatPlaylist)
                {
                    _currentQueueIndex = 0;
                }
                else
                {
                    return null; // Playlist finished
                }
            }
            return _currentQueue[_currentQueueIndex];
        }

        public PlaylistItem? GetPreviousSong(RepeatMode repeatMode)
        {
            if (_currentQueue.Count == 0) return null;

            _currentQueueIndex--;
            if (_currentQueueIndex < 0)
            {
                _currentQueueIndex = repeatMode == RepeatMode.RepeatPlaylist ? _currentQueue.Count - 1 : 0;
            }
            return _currentQueue[_currentQueueIndex];
        }

        public void ToggleShuffle(bool active, SortMode sortMode)
        {
            _isShuffleActive = active;
            if (_currentQueue.Count <= 1) return;

            var currentItem = _currentQueue[_currentQueueIndex];

            if (_isShuffleActive)
            {
                // Shuffle the list
                var shuffled = _currentQueue.OrderBy(x => _random.Next()).ToList();
                _currentQueue = shuffled;
                _currentQueueIndex = _currentQueue.IndexOf(currentItem);
            }
            else
            {
                // Unshuffle (sort) the list
                var sorted = GetSortedPlaylist(sortMode).ToList();
                _currentQueue = sorted;
                _currentQueueIndex = _currentQueue.IndexOf(currentItem);
            }
        }
    }
}
