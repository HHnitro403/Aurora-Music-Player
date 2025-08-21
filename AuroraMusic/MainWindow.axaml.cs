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
using System.ComponentModel;
using System.Runtime.CompilerServices;

namespace AuroraMusic
{
    public partial class MainWindow : Window, INotifyPropertyChanged
    {
        public new event PropertyChangedEventHandler? PropertyChanged;

        protected void OnPropertyChanged([CallerMemberName] string? propertyName = null)
        {
            PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(propertyName));
        }

        protected bool SetProperty<T>(ref T field, T value, [CallerMemberName] string? propertyName = null)
        {
            if (EqualityComparer<T>.Default.Equals(field, value))
            {
                return false;
            }
            field = value;
            OnPropertyChanged(propertyName);
            return true;
        }

        private const string AppVersion = "1.1.0";

        private PlaybackService _playbackService;
        private PlaylistManager _playlistManager;
        private UIService _uiService;
        private DatabaseService _dbService;

        private AppSettings? _appSettings;
        private RepeatMode _currentRepeatMode = RepeatMode.None;
        private SortMode _currentSortMode = SortMode.ArtistAlbum;
        private bool _isShuffleActive = false;
        private bool _isDraggingSlider = false;

        private bool _isMusicLoaded = false;

        public bool IsMusicLoaded
        {
            get => _isMusicLoaded;
            set => SetProperty(ref _isMusicLoaded, value);
        }

        private SettingsView _settingsView;
        private TracksView _tracksView;
        private PlaylistsView _playlistView;
        private AlbumView _albumView;
        private ArtistView _artistView;

        public bool IsPaneOpen
        {
            get => _appSettings?.IsPaneOpen ?? false;
            set
            {
                if (_appSettings != null && _appSettings.IsPaneOpen != value)
                {
                    _appSettings.IsPaneOpen = value;
                    OnPropertyChanged();
                    _dbService.SaveSettingsAsync(_appSettings);
                }
            }
        }

        public MainWindow()
        {
            InitializeComponent();
        }

        private async void MainWindow_Loaded(object? sender, RoutedEventArgs e)
        {
            _dbService = new DatabaseService();
            _playbackService = new PlaybackService();
            _playlistManager = new PlaylistManager(_dbService);

            // Instantiate views here
            _settingsView = new SettingsView(_dbService);
            _tracksView = new TracksView(_playlistManager, _dbService);
            _playlistView = new PlaylistsView(_dbService, _playlistManager, this);
            _albumView = new AlbumView(_dbService);
            _artistView = new ArtistView(_dbService);

            // Instantiate UI service here
            _uiService = new UIService(this, _tracksView);

            this.KeyDown += MainWindow_KeyDown;
            this.ExtendClientAreaToDecorationsHint = true;
            this.ExtendClientAreaTitleBarHeightHint = -1;

            // Subscribe to events here
            _tracksView.PlayRequested += PlayFile;
            _playlistView.PlayRequested += PlayFile;
            _settingsView.FolderSelected += OnFolderSelectedInSettings;
            _settingsView.GoBackRequested += _uiService.ShowMainContent;
            _settingsView.FolderRemoved += async () => await LoadAllMusicFilesAsync();
            _settingsView.FixedMenuSettingChanged += UpdatePaneState;

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

            var mainSplitView = this.FindControl<SplitView>("MainSplitView");
            if (mainSplitView != null)
            {
                mainSplitView.Bind(SplitView.IsPaneOpenProperty, new Avalonia.Data.Binding("IsPaneOpen"));
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

            await InitializeApplicationAsync();
        }

        private void UpdatePaneState(bool isFixed)
        {
            var mainSplitView = this.FindControl<SplitView>("MainSplitView");
            var hamburgerButton = this.FindControl<Button>("HamburgerButton");
            if (mainSplitView == null || hamburgerButton == null) return;

            _appSettings.IsPaneFixed = isFixed;

            if (isFixed)
            {
                mainSplitView.DisplayMode = SplitViewDisplayMode.CompactInline;
                IsPaneOpen = true; // This will use the property setter to open the pane and save the state.
            }
            else
            {
                mainSplitView.DisplayMode = SplitViewDisplayMode.Overlay;
                // The pane's open/closed state is already preserved in IsPaneOpen, so no action is needed.
            }
            hamburgerButton.IsVisible = !isFixed;
        }

        private void HamburgerButton_Click(object? sender, RoutedEventArgs e)
        {
            if (_appSettings != null && !_appSettings.IsPaneFixed)
            {
                IsPaneOpen = !IsPaneOpen;
            }
        }

        private async Task InitializeApplicationAsync()
        {
            (_appSettings, bool dbChangesApplied) = await _dbService.InitializeDatabaseAsync(AppVersion);

            // Pass AppSettings to views that need it after it's loaded
            _settingsView.SetAppSettings(_appSettings);
            _tracksView.SetAppSettings(_appSettings);

            bool showInitializingPopup = dbChangesApplied || _appSettings.IsFirstLaunch;

            if (showInitializingPopup)
            {
                _uiService.ShowPopup("Initializing...", false);
            }

            if (_appSettings.IsFirstLaunch)
            {
                _appSettings.IsFirstLaunch = false;
                await _dbService.SaveSettingsAsync(_appSettings);
                _uiService.ShowView(_settingsView);
            }

            ApplySettings();
            await LoadAllMusicFilesAsync();
            IsMusicLoaded = (await _dbService.GetAllSongsAsync()).Any(); // Set IsMusicLoaded based on songs found

            if (showInitializingPopup)
            {
                // Close the popup after initialization is complete
                _uiService.ClosePopup();
            }
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

                // Apply pane settings
                UpdatePaneState(_appSettings.IsPaneFixed);
                OnPropertyChanged(nameof(IsPaneOpen)); // Notify the UI of the initial state
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
                (_appSettings, _) = await _dbService.InitializeDatabaseAsync(AppVersion);
            }
            await _tracksView.LoadTracksAsync();

            if (nowPlayingInfoText != null) nowPlayingInfoText.Text = "Select a song to play";
            IsMusicLoaded = (await _dbService.GetAllSongsAsync()).Any(); // Update IsMusicLoaded after loading
        }

        private void PlayFile(PlaylistItem item)
        {
            _playbackService.Play(item);
            UpdateNowPlaying(item);
            _tracksView.SetSelectedItem(item);
            var playPauseIcon = this.FindControl<MaterialIcon>("PlayPauseIcon");
            if (playPauseIcon != null)
            {
                playPauseIcon.Kind = MaterialIconKind.Pause;
                Console.WriteLine("PlayPauseIcon found and updated.");
            }
            else
            {
                Console.WriteLine("PlayPauseIcon not found.");
            }
        }

        private void UpdateNowPlaying(PlaylistItem? item)
        {
            var nowPlayingInfoText = this.FindControl<TextBlock>("NowPlayingInfoText");
            var albumArtImage = this.FindControl<Image>("AlbumArtImage");
            if (item != null && nowPlayingInfoText != null && albumArtImage != null)
            {
                nowPlayingInfoText.Text = $"{item.Artist} - {item.Title}";
                albumArtImage.Source = item.AlbumArt;
                Console.WriteLine($"Now Playing: {item.Artist} - {item.Title}");
                Console.WriteLine($"Album Art Source: {item.AlbumArt}");
            }
            else
            {
                Console.WriteLine("UpdateNowPlaying: One or more UI elements or item is null.");
                Console.WriteLine($"Item is null: {item == null}");
                Console.WriteLine($"nowPlayingInfoText is null: {nowPlayingInfoText == null}");
                Console.WriteLine($"albumArtImage is null: {albumArtImage == null}");
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

            _playlistManager.ToggleShuffle(_isShuffleActive, _currentSortMode);
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
                    Console.WriteLine($"TimeChanged: {e.Time}");
                }
                else
                {
                    Console.WriteLine($"OnTimeChanged: songProgressBar or timeLabel not found. songProgressBar null: {songProgressBar == null}, timeLabel null: {timeLabel == null}");
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
                    Console.WriteLine($"LengthChanged: {e.Length}");
                }
                else
                {
                    Console.WriteLine($"OnLengthChanged: songProgressBar or durationLabel not found. songProgressBar null: {songProgressBar == null}, durationLabel null: {durationLabel == null}");
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
                    await _albumView.LoadAlbumsAsync();
                    break;

                case 3: // Artists
                    _uiService.ShowView(_artistView);
                    await _artistView.LoadArtistsAsync();
                    break;

                case 4: // Settings
                    _uiService.ShowView(_settingsView);
                    break;
            }
        }

        private void MainWindow_KeyDown(object? sender, KeyEventArgs e)
        {
            switch (e.Key)
            {
                case Key.MediaPlayPause:
                    PlayPauseButton_Click(this, new RoutedEventArgs());
                    break;

                case Key.MediaNextTrack:
                    NextButton_Click(this, new RoutedEventArgs());
                    break;

                case Key.MediaPreviousTrack:
                    PreviousButton_Click(this, new RoutedEventArgs());
                    break;
            }
        }
    }
}