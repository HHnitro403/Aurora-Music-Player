using Avalonia.Controls;
using Avalonia.Interactivity;
using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Threading.Tasks;
using Avalonia.Threading;
using AuroraMusic.Models;
using AuroraMusic.Services;
using AuroraMusic.Views;
using AuroraMusic.Views.Modals;
using LibVLCSharp.Shared;
using Material.Icons;
using Material.Icons.Avalonia;
using TagLib;
using Avalonia.Input;
using Avalonia.Media;

namespace AuroraMusic
{
    public partial class MainWindow : Window
    {
        private const string AppVersion = "1.1.0";

        private LibVLC _libVLC;
        private MediaPlayer _mediaPlayer;
        private List<PlaylistItem> _playlist = new List<PlaylistItem>();
        private bool _isUserDraggingSlider = false;
        private RepeatMode _currentRepeatMode = RepeatMode.None;

        private readonly Control _mainContentView;
        private readonly SettingsView _settingsView;
        private readonly UpdatePopupView _updatePopup;
        private readonly DatabaseService _dbService;
        private AppSettings _appSettings;

        public MainWindow()
        {
            InitializeComponent();
            this.ExtendClientAreaToDecorationsHint = true;
            this.ExtendClientAreaTitleBarHeightHint = -1;

            _mainContentView = (Control)MainContentArea.Content;
            _settingsView = new SettingsView();
            _settingsView.FolderSelected += OnFolderSelectedInSettings;
            _settingsView.GoBackRequested += ShowMainContent;

            _updatePopup = this.FindControl<UpdatePopupView>("UpdatePopup");
            _updatePopup.OkClicked += HidePopup;

            _dbService = new DatabaseService();
            _ = InitializeApplicationAsync();
        }

        private async Task InitializeApplicationAsync()
        {
            try
            {
                ShowPopup("Initializing...", false);

                // This single call now handles database creation, migration, and versioning.
                _appSettings = await _dbService.InitializeDatabaseAsync(AppVersion);

                // Hide the popup once initialization is complete.
                HidePopup();

                if (_appSettings.IsFirstLaunch)
                {
                    _appSettings.IsFirstLaunch = false;
                    await _dbService.SaveSettingsAsync(_appSettings);
                    SettingsButton_Click(null, null);
                }

                Core.Initialize();
                _libVLC = new LibVLC();
                _mediaPlayer = new MediaPlayer(_libVLC);

                _mediaPlayer.TimeChanged += OnTimeChanged;
                _mediaPlayer.LengthChanged += OnLengthChanged;
                _mediaPlayer.EndReached += OnEndReached;

                VolumeSlider.ValueChanged += OnVolumeChanged;
                PositionSlider.PointerPressed += (s, e) => _isUserDraggingSlider = true;
                PositionSlider.PointerReleased += OnPositionSliderReleased;

                await ApplySettingsAsync();
            }
            catch (Exception ex)
            {
                ShowPopup($"A database error occurred: {ex.Message}", true);
            }
        }

        private async Task ApplySettingsAsync()
        {
            VolumeSlider.Value = _appSettings.Volume;
            if (_mediaPlayer != null) _mediaPlayer.Volume = (int)_appSettings.Volume;

            _currentRepeatMode = (RepeatMode)_appSettings.RepeatMode;
            UpdateRepeatIcon();

            if (!string.IsNullOrEmpty(_appSettings.MusicFolderPath) && Directory.Exists(_appSettings.MusicFolderPath))
            {
                await LoadFilesFromFolder(_appSettings.MusicFolderPath);
            }
        }

        private void ShowPopup(string message, bool showOkButton)
        {
            _updatePopup.SetMessage(message, showOkButton);
            PopupOverlay.IsVisible = true;
        }

        private void HidePopup()
        {
            PopupOverlay.IsVisible = false;
        }

        private void SettingsButton_Click(object? sender, RoutedEventArgs e) => MainContentArea.Content = _settingsView;

        private void ShowMainContent() => MainContentArea.Content = _mainContentView;

        private async void OnFolderSelectedInSettings(string folderPath)
        {
            _appSettings.MusicFolderPath = folderPath;
            await _dbService.SaveSettingsAsync(_appSettings);
            await LoadFilesFromFolder(folderPath);
            ShowMainContent();
        }

        private void Playlist_DoubleTapped(object? sender, TappedEventArgs e)
        {
            if (Playlist.SelectedItem is PlaylistItem selectedItem)
            {
                PlayFile(selectedItem.FilePath);
            }
        }

        private void OnEndReached(object? sender, EventArgs e)
        {
            Dispatcher.UIThread.InvokeAsync(() =>
            {
                switch (_currentRepeatMode)
                {
                    case RepeatMode.RepeatTrack: _mediaPlayer.Play(); break;
                    case RepeatMode.RepeatPlaylist: PlayNext(); break;
                    case RepeatMode.None:
                        if (Playlist.SelectedIndex < _playlist.Count - 1) PlayNext();
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

        private async void RepeatButton_Click(object? sender, RoutedEventArgs e)
        {
            _currentRepeatMode = (RepeatMode)(((int)_currentRepeatMode + 1) % 3);
            UpdateRepeatIcon();
            _appSettings.RepeatMode = (int)_currentRepeatMode;
            await _dbService.SaveSettingsAsync(_appSettings);
        }

        private void UpdateRepeatIcon()
        {
            switch (_currentRepeatMode)
            {
                case RepeatMode.None:
                    RepeatIcon.Kind = MaterialIconKind.Repeat;
                    RepeatIcon.Foreground = Brushes.White;
                    break;

                case RepeatMode.RepeatPlaylist:
                    RepeatIcon.Kind = MaterialIconKind.Repeat;
                    RepeatIcon.Foreground = new SolidColorBrush(Color.FromRgb(0, 191, 255));
                    break;

                case RepeatMode.RepeatTrack:
                    RepeatIcon.Kind = MaterialIconKind.RepeatOnce;
                    RepeatIcon.Foreground = new SolidColorBrush(Color.FromRgb(0, 191, 255));
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
                    if (Playlist.SelectedItem is PlaylistItem selectedItem) PlayFile(selectedItem.FilePath);
                    else
                    {
                        Playlist.SelectedIndex = 0;
                        if (Playlist.SelectedItem is PlaylistItem firstItem) PlayFile(firstItem.FilePath);
                    }
                }
                else
                {
                    _mediaPlayer.Play();
                }

                if (_mediaPlayer.Media != null) PlayPauseIcon.Kind = MaterialIconKind.Pause;
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

        private void PreviousButton_Click(object sender, RoutedEventArgs e) => PlayPrevious();

        private void NextButton_Click(object sender, RoutedEventArgs e) => PlayNext();

        private async void OnVolumeChanged(object? sender, Avalonia.Controls.Primitives.RangeBaseValueChangedEventArgs e)
        {
            if (_mediaPlayer != null && _appSettings != null)
            {
                _mediaPlayer.Volume = (int)e.NewValue;
                _appSettings.Volume = e.NewValue;
                await _dbService.SaveSettingsAsync(_appSettings);
            }
        }

        private void OnPositionSliderReleased(object? sender, Avalonia.Input.PointerReleasedEventArgs e)
        {
            if (_mediaPlayer.Media != null) _mediaPlayer.Time = (long)PositionSlider.Value;
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
                _playlist = files.Select(file =>
                {
                    try
                    {
                        using var tagFile = TagLib.File.Create(file);
                        return new PlaylistItem
                        {
                            Title = string.IsNullOrWhiteSpace(tagFile.Tag.Title) ? Path.GetFileNameWithoutExtension(file) : tagFile.Tag.Title,
                            Artist = string.IsNullOrWhiteSpace(tagFile.Tag.FirstPerformer) ? "Unknown Artist" : tagFile.Tag.FirstPerformer,
                            FilePath = file
                        };
                    }
                    catch { return new PlaylistItem { Title = Path.GetFileName(file), FilePath = file }; }
                }).ToList();
            });

            Playlist.Items.Clear();
            foreach (var item in _playlist) Playlist.Items.Add(item);
            NowPlayingInfoText.Text = "Select a song to play";
        }

        private void PlayFile(string filePath)
        {
            if (_mediaPlayer.IsPlaying) _mediaPlayer.Stop();

            var media = new Media(_libVLC, new Uri(filePath));
            _mediaPlayer.Media = media;
            _mediaPlayer.Play();
            media.Dispose();

            PlayPauseIcon.Kind = MaterialIconKind.Pause;
            UpdateNowPlaying(_playlist.FirstOrDefault(p => p.FilePath == filePath));
        }

        private void UpdateNowPlaying(PlaylistItem? item)
        {
            if (item != null) NowPlayingInfoText.Text = $"{item.Artist} - {item.Title}";
        }

        private void PlayNext()
        {
            if (_playlist.Count == 0) return;
            int currentIndex = Playlist.SelectedIndex;
            int nextIndex = (currentIndex + 1) % _playlist.Count;
            Playlist.SelectedIndex = nextIndex;
            if (Playlist.SelectedItem is PlaylistItem selectedItem) PlayFile(selectedItem.FilePath);
        }

        private void PlayPrevious()
        {
            if (_playlist.Count == 0) return;
            int currentIndex = Playlist.SelectedIndex;
            int prevIndex = (currentIndex - 1 + _playlist.Count) % _playlist.Count;
            Playlist.SelectedIndex = prevIndex;
            if (Playlist.SelectedItem is PlaylistItem selectedItem) PlayFile(selectedItem.FilePath);
        }

        private void OnTimeChanged(object? sender, MediaPlayerTimeChangedEventArgs e)
        {
            Dispatcher.UIThread.InvokeAsync(() =>
            {
                if (!_isUserDraggingSlider) PositionSlider.Value = e.Time;
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
    }
}