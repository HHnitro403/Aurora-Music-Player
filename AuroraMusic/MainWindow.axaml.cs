using Avalonia.Controls;
using Avalonia.Interactivity;
using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Threading.Tasks;
using Avalonia.Threading;
using LibVLCSharp.Shared;
using Material.Icons;
using Material.Icons.Avalonia;
using TagLib;
using Avalonia.Input; // Required for TappedEventArgs
using Avalonia.Media; // Required for Brushes

namespace AuroraMusic
{
    public enum RepeatMode
    {
        None,
        RepeatPlaylist,
        RepeatTrack
    }

    public class PlaylistItem
    {
        public string Title { get; set; } = "Unknown Title";
        public string Artist { get; set; } = "Unknown Artist";
        public string FilePath { get; set; } = "";

        public override string ToString() => $"{Artist} - {Title}";
    }

    public partial class MainWindow : Window
    {
        private LibVLC _libVLC;
        private MediaPlayer _mediaPlayer;
        private List<PlaylistItem> _playlist = new List<PlaylistItem>();
        private bool _isUserDraggingSlider = false;
        private RepeatMode _currentRepeatMode = RepeatMode.None;

        private readonly Control _mainContentView;
        private readonly SettingsView _settingsView;

        public MainWindow()
        {
            InitializeComponent();
            this.ExtendClientAreaToDecorationsHint = true;
            this.ExtendClientAreaTitleBarHeightHint = -1;

            _mainContentView = (Control)MainContentArea.Content;
            _settingsView = new SettingsView();
            _settingsView.FolderSelected += OnFolderSelectedInSettings;
            _settingsView.GoBackRequested += ShowMainContent;

            try
            {
                Core.Initialize();
            }
            catch (Exception ex)
            {
                NowPlayingInfoText.Text = $"Error: VLC runtime not found. Please install VLC.";
                SettingsButton.IsEnabled = false;
                return;
            }

            _libVLC = new LibVLC();
            _mediaPlayer = new MediaPlayer(_libVLC);

            _mediaPlayer.TimeChanged += OnTimeChanged;
            _mediaPlayer.LengthChanged += OnLengthChanged;
            _mediaPlayer.EndReached += OnEndReached;

            VolumeSlider.ValueChanged += OnVolumeChanged;
            PositionSlider.PointerPressed += (s, e) => _isUserDraggingSlider = true;
            PositionSlider.PointerReleased += OnPositionSliderReleased;
        }

        private void SettingsButton_Click(object? sender, RoutedEventArgs e)
        {
            MainContentArea.Content = _settingsView;
        }

        private void ShowMainContent()
        {
            MainContentArea.Content = _mainContentView;
        }

        private void OnFolderSelectedInSettings(string folderPath)
        {
            _ = LoadFilesFromFolder(folderPath);
            ShowMainContent();
        }

        private void Playlist_DoubleTapped(object? sender, TappedEventArgs e)
        {
            if (Playlist.SelectedItem is PlaylistItem selectedItem)
            {
                PlayFile(selectedItem.FilePath);
                UpdateNowPlaying(selectedItem);
            }
        }

        private void OnTimeChanged(object? sender, MediaPlayerTimeChangedEventArgs e)
        {
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
            Dispatcher.UIThread.InvokeAsync(() =>
            {
                switch (_currentRepeatMode)
                {
                    case RepeatMode.RepeatTrack:
                        _mediaPlayer.Play(); // Replay the current track
                        break;

                    case RepeatMode.RepeatPlaylist:
                        PlayNext(); // Play the next track, wrapping around
                        break;

                    case RepeatMode.None:
                        // If it's the last song, stop. Otherwise, play the next one.
                        if (Playlist.SelectedIndex < _playlist.Count - 1)
                        {
                            PlayNext();
                        }
                        else
                        {
                            _mediaPlayer.Stop();
                            PlayPauseIcon.Kind = MaterialIconKind.Play;
                            NowPlayingInfoText.Text = "Playlist finished";
                        }
                        break;
                }
            });
        }

        private void RepeatButton_Click(object? sender, RoutedEventArgs e)
        {
            _currentRepeatMode = (RepeatMode)(((int)_currentRepeatMode + 1) % 3);

            switch (_currentRepeatMode)
            {
                case RepeatMode.None:
                    RepeatIcon.Kind = MaterialIconKind.Repeat;
                    RepeatIcon.Foreground = Brushes.White;
                    break;

                case RepeatMode.RepeatPlaylist:
                    RepeatIcon.Kind = MaterialIconKind.Repeat;
                    RepeatIcon.Foreground = new SolidColorBrush(Color.FromRgb(0, 191, 255)); // DeepSkyBlue
                    break;

                case RepeatMode.RepeatTrack:
                    RepeatIcon.Kind = MaterialIconKind.RepeatOnce;
                    RepeatIcon.Foreground = new SolidColorBrush(Color.FromRgb(0, 191, 255)); // DeepSkyBlue
                    break;
            }
        }

        private void PlayPauseButton_Click(object sender, RoutedEventArgs e)
        {
            if (_mediaPlayer.IsPlaying)
            {
                _mediaPlayer.Pause();
                PlayPauseIcon.Kind = MaterialIconKind.Play;
            }
            else
            {
                if (_mediaPlayer.Media == null && Playlist.Items.Count > 0)
                {
                    if (Playlist.SelectedItem != null)
                    {
                        if (Playlist.SelectedItem is PlaylistItem selectedItem)
                        {
                            PlayFile(selectedItem.FilePath);
                            UpdateNowPlaying(selectedItem);
                        }
                    }
                    else
                    {
                        Playlist.SelectedIndex = 0;
                        if (Playlist.SelectedItem is PlaylistItem selectedItem)
                        {
                            PlayFile(selectedItem.FilePath);
                            UpdateNowPlaying(selectedItem);
                        }
                    }
                }
                else
                {
                    _mediaPlayer.Play();
                }
                PlayPauseIcon.Kind = MaterialIconKind.Pause;
            }
        }

        private void StopButton_Click(object sender, RoutedEventArgs e)
        {
            _mediaPlayer.Stop();
            PlayPauseIcon.Kind = MaterialIconKind.Play;
            NowPlayingInfoText.Text = "Stopped";
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

        private async Task LoadFilesFromFolder(string folderPath)
        {
            _playlist.Clear();
            Playlist.Items.Clear();
            NowPlayingInfoText.Text = "Loading...";

            var supportedExtensions = new[] { ".mp3", ".flac", ".m4a", ".mp4" };
            var files = Directory.EnumerateFiles(folderPath, "*.*", SearchOption.AllDirectories)
                                 .Where(f => supportedExtensions.Contains(Path.GetExtension(f).ToLower()));

            await Task.Run(() =>
            {
                foreach (var file in files)
                {
                    try
                    {
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
                        _playlist.Add(new PlaylistItem { Title = Path.GetFileName(file), FilePath = file });
                    }
                    catch (Exception)
                    {
                    }
                }
            });

            Playlist.Items.Clear();
            foreach (var item in _playlist)
            {
                Playlist.Items.Add(item);
            }

            NowPlayingInfoText.Text = "Select a song to play";
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

            media.Dispose();

            PlayPauseIcon.Kind = MaterialIconKind.Pause;
        }

        private void UpdateNowPlaying(PlaylistItem item)
        {
            NowPlayingInfoText.Text = $"{item.Artist} - {item.Title}";
        }

        private void PlayNext()
        {
            if (_playlist.Count == 0) return;
            int currentIndex = Playlist.SelectedIndex;
            int nextIndex = (currentIndex + 1) % _playlist.Count;
            Playlist.SelectedIndex = nextIndex;
            if (Playlist.SelectedItem is PlaylistItem selectedItem)
            {
                PlayFile(selectedItem.FilePath);
                UpdateNowPlaying(selectedItem);
            }
        }

        private void PlayPrevious()
        {
            if (_playlist.Count == 0) return;
            int currentIndex = Playlist.SelectedIndex;
            int prevIndex = (currentIndex - 1 + _playlist.Count) % _playlist.Count;
            Playlist.SelectedIndex = prevIndex;
            if (Playlist.SelectedItem is PlaylistItem selectedItem)
            {
                PlayFile(selectedItem.FilePath);
                UpdateNowPlaying(selectedItem);
            }
        }
    }
}