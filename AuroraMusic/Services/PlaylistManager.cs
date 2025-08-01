
using AuroraMusic.Models;
using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Threading.Tasks;
using Avalonia.Media.Imaging;
using TagLib;

namespace AuroraMusic.Services
{
    public class PlaylistManager
    {
        private List<PlaylistItem> _masterPlaylist = new List<PlaylistItem>();
        private List<PlaylistItem> _currentQueue = new List<PlaylistItem>();
        private int _currentQueueIndex = -1;
        private bool _isShuffleActive = false;

        public IReadOnlyList<PlaylistItem> CurrentQueue => _currentQueue;
        public int CurrentQueueIndex => _currentQueueIndex;

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

                    await Task.Run(() =>
                    {
                        foreach (var file in files)
                        {
                            using var tagFile = TagLib.File.Create(file);
                                var item = new PlaylistItem
                                {
                                    Song = new Song
                                    {
                                        Title = string.IsNullOrWhiteSpace(tagFile.Tag.Title) ? Path.GetFileNameWithoutExtension(file) : tagFile.Tag.Title,
                                        FilePath = file,
                                        Artist = new Artist { Name = string.IsNullOrWhiteSpace(tagFile.Tag.FirstPerformer) ? "Unknown Artist" : tagFile.Tag.FirstPerformer },
                                        Album = new Album { Title = string.IsNullOrWhiteSpace(tagFile.Tag.Album) ? "Unknown Album" : tagFile.Tag.Album, Artist = new Artist { Name = string.IsNullOrWhiteSpace(tagFile.Tag.FirstAlbumArtist) ? "Unknown Artist" : tagFile.Tag.FirstAlbumArtist } }
                                    }
                                };
                                if (tagFile.Tag.Pictures.Length > 0)
                                {
                                    using var stream = new MemoryStream(tagFile.Tag.Pictures[0].Data.Data);
                                    item.Song.Album.AlbumArt = stream.ToArray();
                                }
                                loadedPlaylist.Add(item);
                        }
                    });
                }
            }
            _masterPlaylist = loadedPlaylist;
        }

        public IEnumerable<PlaylistItem> GetSortedPlaylist(SortMode sortMode)
        {
            return sortMode switch
            {
                SortMode.Alphabetical => _masterPlaylist.OrderBy(p => p.Title),
                SortMode.Album => _masterPlaylist.OrderBy(p => p.Album).ThenBy(p => p.Title),
                SortMode.Artist => _masterPlaylist.OrderBy(p => p.Artist).ThenBy(p => p.Title),
                SortMode.ArtistAlbum => _masterPlaylist.OrderBy(p => p.Artist).ThenBy(p => p.Album).ThenBy(p => p.Title),
                _ => _masterPlaylist.OrderBy(p => p.Artist).ThenBy(p => p.Album).ThenBy(p => p.Title),
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

        public IEnumerable<PlaylistItem> ToggleShuffle(bool active)
        {
            _isShuffleActive = active;
            if (_isShuffleActive)
            {
                var random = new Random();
                return _currentQueue.OrderBy(x => random.Next());
            }
            else
            {
                return GetSortedPlaylist(SortMode.ArtistAlbum); // Or whatever the default sort is
            }
        }
    }
}
