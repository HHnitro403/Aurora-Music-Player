using Avalonia.Controls;
using Avalonia.Interactivity;
using Avalonia.Platform.Storage;
using Avalonia.Threading;
using LibVLCSharp.Shared;
using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Threading.Tasks;
using TagLib;

namespace AuroraMusic
{
    public class PlaylistItem
    {
        public string Title { get; set; } = "Unknown Title";
        public string Artist { get; set; } = "Unknown Artist";
        public string FilePath { get; set; } = "";

        public override string ToString() => $"{Artist} - {Title}";
    }

    public partial class MainWindow : Window
    {
        // LibVLCSharp objects
        private LibVLC _libVLC;

        private MediaPlayer _mediaPlayer;

        // A list to hold our playlist items
        private List<PlaylistItem> _playlist = new List<PlaylistItem>();

        private bool _isUserDraggingSlider = false;

        public MainWindow()
        {
            InitializeComponent();

            // Best to load native libraries in the constructor.
            // This will throw an exception if VLC is not installed.
            try
            {
                Core.Initialize();
            }
            catch (Exception ex)
            {
                // Inform the user that VLC is required.
                NowPlayingText.Text = $"Error: VLC runtime not found. Please install VLC media player. Details: {ex.Message}";
                OpenFolderButton.IsEnabled = false;
                return;
            }

            _libVLC = new LibVLC();
            _mediaPlayer = new MediaPlayer(_libVLC);

            // Set up event handlers for the media player
            _mediaPlayer.TimeChanged += OnTimeChanged;
            _mediaPlayer.LengthChanged += OnLengthChanged;
            _mediaPlayer.EndReached += OnEndReached;

            // Set up event handlers for UI controls
            VolumeSlider.ValueChanged += OnVolumeChanged;
            PositionSlider.PointerPressed += (s, e) => _isUserDraggingSlider = true;
            PositionSlider.PointerReleased += OnPositionSliderReleased;
        }

        // --- Event Handlers for Media Player ---

        private void OnTimeChanged(object? sender, MediaPlayerTimeChangedEventArgs e)
        {
            // We need to dispatch UI updates to the UI thread
            Dispatcher.UIThread.InvokeAsync(() =>
            {
                if (!_isUserDraggingSlider)
                {
                    PositionSlider.Value = e.Time;
                }
                TimeLabel.Text = TimeSpan.FromMilliseconds(e.Time).ToString(@"mm\:ss");
            });
        }

        private void OnLengthChanged(object? sender, MediaPlayerLengthChangedEventArgs e)
        {
            Dispatcher.UIThread.InvokeAsync(() =>
            {
                PositionSlider.Maximum = e.Length;
                DurationLabel.Text = TimeSpan.FromMilliseconds(e.Length).ToString(@"mm\:ss");
            });
        }

        private void OnEndReached(object? sender, EventArgs e)
        {
            // When a song finishes, play the next one
            Dispatcher.UIThread.InvokeAsync(PlayNext);
        }

        // --- Event Handlers for UI Controls ---

        private async void OpenFolderButton_Click(object sender, RoutedEventArgs e)
        {
            var topLevel = GetTopLevel(this);
            if (topLevel == null) return;

            // Use Avalonia's storage provider to open a folder picker
            var folders = await topLevel.StorageProvider.OpenFolderPickerAsync(new FolderPickerOpenOptions
            {
                Title = "Select Music Folder",
                AllowMultiple = false
            });

            if (folders.Count > 0)
            {
                await LoadFilesFromFolder(folders[0].Path.LocalPath);
            }
        }

        private void Playlist_SelectionChanged(object? sender, SelectionChangedEventArgs e)
        {
            if (Playlist.SelectedItem is PlaylistItem selectedItem)
            {
                PlayFile(selectedItem.FilePath);
                UpdateNowPlaying(selectedItem);
            }
        }

        private void PlayPauseButton_Click(object sender, RoutedEventArgs e)
        {
            if (_mediaPlayer.IsPlaying)
            {
                _mediaPlayer.Pause();
                PlayPauseButton.Content = "▶️";
            }
            else
            {
                // If we are at the beginning of the playlist and nothing is loaded
                if (_mediaPlayer.Media == null && Playlist.Items.Count > 0)
                {
                    Playlist.SelectedIndex = 0; // This will trigger playback
                }
                else
                {
                    _mediaPlayer.Play();
                }
                PlayPauseButton.Content = "⏸";
            }
        }

        private void StopButton_Click(object sender, RoutedEventArgs e)
        {
            _mediaPlayer.Stop();
            PlayPauseButton.Content = "▶️";
            NowPlayingText.Text = "Stopped";
            PositionSlider.Value = 0;
            TimeLabel.Text = "00:00";
        }

        private void PreviousButton_Click(object sender, RoutedEventArgs e)
        {
            PlayPrevious();
        }

        private void NextButton_Click(object sender, RoutedEventArgs e)
        {
            PlayNext();
        }

        private void OnVolumeChanged(object? sender, Avalonia.Controls.Primitives.RangeBaseValueChangedEventArgs e)
        {
            if (_mediaPlayer != null)
            {
                // Volume is a percentage from 0 to 100
                _mediaPlayer.Volume = (int)e.NewValue;
            }
        }

        private void OnPositionSliderReleased(object? sender, Avalonia.Input.PointerReleasedEventArgs e)
        {
            if (_mediaPlayer.Media != null)
            {
                _mediaPlayer.Time = (long)PositionSlider.Value;
            }
            _isUserDraggingSlider = false;
        }

        // --- Helper Methods ---

        private async Task LoadFilesFromFolder(string folderPath)
        {
            _playlist.Clear();
            Playlist.Items.Clear();
            NowPlayingText.Text = "Loading...";

            var supportedExtensions = new[] { ".mp3", ".flac", ".m4a", ".mp4" };
            var files = Directory.EnumerateFiles(folderPath, "*.*", SearchOption.AllDirectories)
                                 .Where(f => supportedExtensions.Contains(Path.GetExtension(f).ToLower()));

            await Task.Run(() =>
            {
                foreach (var file in files)
                {
                    try
                    {
                        // Use TagLib# to read metadata from the file
                        using (var tagFile = TagLib.File.Create(file))
                        {
                            _playlist.Add(new PlaylistItem
                            {
                                Title = string.IsNullOrWhiteSpace(tagFile.Tag.Title) ? Path.GetFileNameWithoutExtension(file) : tagFile.Tag.Title,
                                Artist = string.IsNullOrWhiteSpace(tagFile.Tag.FirstPerformer) ? "Unknown Artist" : tagFile.Tag.FirstPerformer,
                                FilePath = file
                            });
                        }
                    }
                    catch (CorruptFileException)
                    {
                        // Handle cases where metadata can't be read
                        _playlist.Add(new PlaylistItem { Title = Path.GetFileName(file), FilePath = file });
                    }
                    catch (Exception)
                    {
                        // Ignore other potential errors with reading files
                    }
                }
            });

            // Update the UI on the main thread
            Playlist.Items.Clear();
            foreach (var item in _playlist)
            {
                Playlist.Items.Add(item);
            }

            NowPlayingText.Text = $"Loaded {_playlist.Count} tracks. Select a song to play.";
        }

        private void PlayFile(string filePath)
        {
            if (_mediaPlayer.IsPlaying)
            {
                _mediaPlayer.Stop();
            }

            var media = new Media(_libVLC, new Uri(filePath));
            _mediaPlayer.Media = media;
            _mediaPlayer.Play();

            media.Dispose(); // Dispose of the media object after it's been assigned

            PlayPauseButton.Content = "⏸";
        }

        private void UpdateNowPlaying(PlaylistItem item)
        {
            NowPlayingText.Text = $"Now Playing: {item.Artist} - {item.Title}";
        }

        private void PlayNext()
        {
            if (_playlist.Count == 0) return;

            int currentIndex = Playlist.SelectedIndex;
            int nextIndex = (currentIndex + 1) % _playlist.Count;
            Playlist.SelectedIndex = nextIndex;
        }

        private void PlayPrevious()
        {
            if (_playlist.Count == 0) return;

            int currentIndex = Playlist.SelectedIndex;
            int prevIndex = (currentIndex - 1 + _playlist.Count) % _playlist.Count;
            Playlist.SelectedIndex = prevIndex;
        }
    }
}