using AuroraMusic.Models;
using AuroraMusic.Services;
using AuroraMusic.Views;
using Avalonia.Controls;
using Avalonia.Input;
using Avalonia.Interactivity;
using Avalonia.Media;
using Avalonia.Threading;
using LibVLCSharp.Shared;
using Material.Icons;
using Material.Icons.Avalonia;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;

namespace AuroraMusic
{
    public partial class MainWindow : Window
    {
        private const string AppVersion = "1.1.0";

        private readonly PlaybackService _playbackService;
        private readonly PlaylistManager _playlistManager;
        private readonly UIService _uiService;
        private readonly DatabaseService _dbService;

        private AppSettings? _appSettings;
        private RepeatMode _currentRepeatMode = RepeatMode.None;
        private SortMode _currentSortMode = SortMode.ArtistAlbum;
        private bool _isShuffleActive = false;
        private bool _isDraggingSlider = false;

        private readonly SettingsView _settingsView;
        private readonly TracksView _tracksView;
        private readonly PlaylistsView _playlistView;
        private readonly AlbumView _albumView;
        private readonly ArtistView _artistView;

        public MainWindow()
        {
            InitializeComponent();
            this.ExtendClientAreaToDecorationsHint = true;
            this.ExtendClientAreaTitleBarHeightHint = -1;

            _dbService = new DatabaseService();
            _playbackService = new PlaybackService();
            _playlistManager = new PlaylistManager();

            _settingsView = new SettingsView();
            _tracksView = new TracksView(_playlistManager, _dbService);
            _playlistView = new PlaylistsView(_dbService, _playlistManager, this);
            _albumView = new AlbumView();
            _artistView = new ArtistView();

            _tracksView.PlayRequested += PlayFile;
            _playlistView.PlayRequested += PlayFile;

            _uiService = new UIService(this, _tracksView);

            _settingsView.FolderSelected += OnFolderSelectedInSettings;
            _settingsView.GoBackRequested += _uiService.ShowMainContent;
            _settingsView.FolderRemoved += async () => await LoadAllMusicFilesAsync();

            _playbackService.EndReached += OnEndReached;
            _playbackService.TimeChanged += OnTimeChanged;
            _playbackService.LengthChanged += OnLengthChanged;

            var navigationListBox = this.FindControl<ListBox>("NavigationListBox");
            if (navigationListBox != null)
            {
                navigationListBox.ItemsSource = new List<NavigationItem>
                {
                    new NavigationItem { Title = "Tracks", Icon = MaterialIconKind.MusicNote },
                    new NavigationItem { Title = "Playlists", Icon = MaterialIconKind.PlaylistPlay },
                    new NavigationItem { Title = "Albums", Icon = MaterialIconKind.Album },
                    new NavigationItem { Title = "Artists", Icon = MaterialIconKind.AccountMusic },
                    new NavigationItem { Title = "Settings", Icon = MaterialIconKind.Cog }
                };
                navigationListBox.SelectedIndex = 0;
            }

            

            var songProgressBar = this.FindControl<Slider>("SongProgressBar");
            if (songProgressBar != null)
            {
                songProgressBar.AddHandler(PointerPressedEvent, (s, e) => _isDraggingSlider = true, RoutingStrategies.Tunnel, true);
                songProgressBar.AddHandler(PointerReleasedEvent, (s, e) =>
                {
                    if (s is Slider slider)
                    {
                        _playbackService.Time = (long)slider.Value;
                    }
                    _isDraggingSlider = false;
                }, RoutingStrategies.Tunnel, true);
            }

            _ = InitializeApplicationAsync();
        }

        private async Task InitializeApplicationAsync()
        {
            _uiService.ShowPopup("Initializing...", false);
            _appSettings = await _dbService.InitializeDatabaseAsync(AppVersion);
            _uiService.HidePopup();

            if (_appSettings != null && _appSettings.IsFirstLaunch)
            {
                _appSettings.IsFirstLaunch = false;
                await _dbService.SaveSettingsAsync(_appSettings);
                _uiService.ShowView(_settingsView);
            }

            ApplySettings();
            await LoadAllMusicFilesAsync();
        }

        private void ApplySettings()
        {
            var volumeSlider = this.FindControl<Slider>("VolumeSlider");
            if (volumeSlider != null && _appSettings != null)
            {
                volumeSlider.Value = _appSettings.Volume;
                _playbackService.Volume = (int)_appSettings.Volume;
                volumeSlider.ValueChanged += OnVolumeChanged;
            }

            if (_appSettings != null)
            {
                _currentRepeatMode = (RepeatMode)_appSettings.RepeatMode;
                UpdateRepeatIcon();

                _currentSortMode = (SortMode)_appSettings.SortMode;
                var sortComboBox = this.FindControl<ComboBox>("SortComboBox");
                if (sortComboBox != null)
                {
                    sortComboBox.SelectedIndex = (int)_currentSortMode;
                }
            }
        }

        private async void OnFolderSelectedInSettings(string folderPath)
        {
            await _dbService.AddFolderAsync(folderPath);
            await LoadAllMusicFilesAsync();
            _uiService.ShowMainContent();
        }

        private async Task LoadAllMusicFilesAsync()
        {
            var nowPlayingInfoText = this.FindControl<TextBlock>("NowPlayingInfoText");
            if (nowPlayingInfoText != null) nowPlayingInfoText.Text = "Loading...";

            if (_appSettings == null)
            {
                _appSettings = await _dbService.InitializeDatabaseAsync(AppVersion);
            }
            await _tracksView.LoadTracksAsync(_appSettings);

            if (nowPlayingInfoText != null) nowPlayingInfoText.Text = "Select a song to play";
        }

        

        

        private void PlayFile(PlaylistItem item)
        {
            _playbackService.Play(item);
            UpdateNowPlaying(item);
            var playPauseIcon = this.FindControl<MaterialIcon>("PlayPauseIcon");
            if (playPauseIcon != null) playPauseIcon.Kind = MaterialIconKind.Pause;
        }

        private void UpdateNowPlaying(PlaylistItem? item)
        {
            var nowPlayingInfoText = this.FindControl<TextBlock>("NowPlayingInfoText");
            var albumArtImage = this.FindControl<Image>("AlbumArtImage");
            if (item != null && nowPlayingInfoText != null && albumArtImage != null)
            {
                nowPlayingInfoText.Text = $"{item.Artist} - {item.Title}";
                albumArtImage.Source = item.AlbumArt;
            }
        }

        private void OnEndReached(object? sender, EventArgs e)
        {
            Dispatcher.UIThread.InvokeAsync(() =>
            {
                var nextSong = _playlistManager.GetNextSong(_currentRepeatMode);
                if (nextSong != null) PlayFile(nextSong); else StopPlayback();
            });
        }

        private void PlayNext()
        {
            var nextSong = _playlistManager.GetNextSong(_currentRepeatMode);
            if (nextSong != null) PlayFile(nextSong); else StopPlayback();
        }

        private void PlayPrevious()
        {
            var prevSong = _playlistManager.GetPreviousSong(_currentRepeatMode);
            if (prevSong != null) PlayFile(prevSong);
        }

        private void StopPlayback()
        {
            _playbackService.Stop();
            var playPauseIcon = this.FindControl<MaterialIcon>("PlayPauseIcon");
            if (playPauseIcon != null) playPauseIcon.Kind = MaterialIconKind.Play;
            var nowPlayingInfoText = this.FindControl<TextBlock>("NowPlayingInfoText");
            if (nowPlayingInfoText != null) nowPlayingInfoText.Text = "Playlist finished";
        }

        private void ShuffleButton_Click(object? sender, RoutedEventArgs e)
        {
            _isShuffleActive = !_isShuffleActive;
            var shuffleIcon = this.FindControl<MaterialIcon>("ShuffleIcon");
            if (shuffleIcon != null) shuffleIcon.Foreground = _isShuffleActive ? new SolidColorBrush(Color.FromRgb(0, 191, 255)) : Brushes.White;

            var shuffledPlaylist = _playlistManager.ToggleShuffle(_isShuffleActive);
            // _tracksView.UpdatePlaylistDisplay(shuffledPlaylist); // Moved to TracksView
        }

        private async void RepeatButton_Click(object? sender, RoutedEventArgs e)
        {
            _currentRepeatMode = (RepeatMode)(((int)_currentRepeatMode + 1) % 3);
            UpdateRepeatIcon();
            if (_appSettings != null)
            {
                _appSettings.RepeatMode = (int)_currentRepeatMode;
                await _dbService.SaveSettingsAsync(_appSettings);
            }
        }

        private void UpdateRepeatIcon()
        {
            var repeatIcon = this.FindControl<MaterialIcon>("RepeatIcon");
            if (repeatIcon == null) return;

            switch (_currentRepeatMode)
            {
                case RepeatMode.None: 
                    repeatIcon.Kind = MaterialIconKind.Repeat; 
                    repeatIcon.Foreground = Brushes.White; 
                    break;
                case RepeatMode.RepeatPlaylist: 
                    repeatIcon.Kind = MaterialIconKind.Repeat;
                    repeatIcon.Foreground = new SolidColorBrush(Color.FromRgb(0, 191, 255)); 
                    break;
                case RepeatMode.RepeatTrack: 
                    repeatIcon.Kind = MaterialIconKind.RepeatOnce; 
                    repeatIcon.Foreground = new SolidColorBrush(Color.FromRgb(0, 191, 255)); 
                    break;
            }
        }

        private void PlayPauseButton_Click(object sender, RoutedEventArgs e)
        {
            var playPauseIcon = this.FindControl<MaterialIcon>("PlayPauseIcon");
            if (playPauseIcon == null) return;

            if (_playbackService.IsPlaying)
            {
                _playbackService.Pause();
                playPauseIcon.Kind = MaterialIconKind.Play;
            }
            else
            {
                _playbackService.Play();
                playPauseIcon.Kind = MaterialIconKind.Pause;
            }
        }

        private void PreviousButton_Click(object sender, RoutedEventArgs e) => PlayPrevious();

        private void NextButton_Click(object sender, RoutedEventArgs e) => PlayNext();

        private async void OnVolumeChanged(object? sender, Avalonia.Controls.Primitives.RangeBaseValueChangedEventArgs e)
        {
            if (_appSettings != null)
            {
                _playbackService.Volume = (int)e.NewValue;
                _appSettings.Volume = e.NewValue;
                await _dbService.SaveSettingsAsync(_appSettings);
            }
        }

        

        

        private void OnTimeChanged(object? sender, MediaPlayerTimeChangedEventArgs e)
        {
            if (_isDraggingSlider) return;
            Dispatcher.UIThread.InvokeAsync(() =>
            {
                var songProgressBar = this.FindControl<Slider>("SongProgressBar");
                var timeLabel = this.FindControl<TextBlock>("TimeLabel");
                if (songProgressBar != null && timeLabel != null)
                {
                    songProgressBar.Value = e.Time;
                    timeLabel.Text = TimeSpan.FromMilliseconds(e.Time).ToString(@"mm\:ss");
                }
            });
        }

        private void OnLengthChanged(object? sender, MediaPlayerLengthChangedEventArgs e)
        {
            Dispatcher.UIThread.InvokeAsync(() =>
            {
                var songProgressBar = this.FindControl<Slider>("SongProgressBar");
                var durationLabel = this.FindControl<TextBlock>("DurationLabel");
                if (songProgressBar != null && durationLabel != null)
                {
                    songProgressBar.Maximum = e.Length;
                    durationLabel.Text = TimeSpan.FromMilliseconds(e.Length).ToString(@"mm\:ss");
                }
            });
        }

        private void MinimizeButton_Click(object? sender, RoutedEventArgs e) => WindowState = WindowState.Minimized;

        private void MaximizeButton_Click(object? sender, RoutedEventArgs e) => WindowState = WindowState == WindowState.Maximized ? WindowState.Normal : WindowState.Maximized;

        private void CloseButton_Click(object? sender, RoutedEventArgs e) => Close();

        private void TitleBar_PointerPressed(object? sender, PointerPressedEventArgs e) => BeginMoveDrag(e);
        
        private void SettingsButton_Click(object? sender, RoutedEventArgs e) => _uiService.ShowView(_settingsView);

        private async void Navigation_SelectionChanged(object? sender, SelectionChangedEventArgs e)
        {
            if (sender is not ListBox listBox || _uiService is null) return;

            switch (listBox.SelectedIndex)
            {
                case 0: // Tracks
                    _uiService.ShowView(_tracksView);
                    break;
                case 1: // Playlists
                    _uiService.ShowView(_playlistView);
                    await _playlistView.LoadPlaylistsAsync();
                    break;
                case 2: // Albums
                    _uiService.ShowView(_albumView);
                    break;
                case 3: // Artists
                    _uiService.ShowView(_artistView);
                    break;
                case 4: // Settings
                    _uiService.ShowView(_settingsView);
                    break;
            }
        }
    }
}
