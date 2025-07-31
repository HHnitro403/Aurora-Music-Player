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
using Avalonia.Media.Imaging;

namespace AuroraMusic
{
    public partial class MainWindow : Window
    {
        private const string AppVersion = "1.1.0"; // Reverted version

        private LibVLC _libVLC;
        private MediaPlayer _mediaPlayer;
        private RepeatMode _currentRepeatMode = RepeatMode.None;

        private List<PlaylistItem> _masterPlaylist = new List<PlaylistItem>();
        private List<PlaylistItem> _currentQueue = new List<PlaylistItem>();
        private int _currentQueueIndex = -1;
        private bool _isShuffleActive = false;

        private readonly SettingsView _settingsView;
        private readonly UpdatePopupView _updatePopup;
        private readonly DatabaseService _dbService;
        private AppSettings _appSettings;

        public MainWindow()
        {
            InitializeComponent();
            this.ExtendClientAreaToDecorationsHint = true;
            this.ExtendClientAreaTitleBarHeightHint = -1;

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
                _appSettings = await _dbService.InitializeDatabaseAsync(AppVersion);
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
                _mediaPlayer.EndReached += OnEndReached;
                _mediaPlayer.TimeChanged += OnTimeChanged;
                _mediaPlayer.LengthChanged += OnLengthChanged;

                await ApplySettingsAsync();
            }
            catch (Exception ex)
            {
                ShowPopup($"A database error occurred: {ex.Message}", true);
            }
        }

        private async Task ApplySettingsAsync()
        {
            var volumeSlider = this.FindControl<Slider>("VolumeSlider");
            volumeSlider.Value = _appSettings.Volume;
            _mediaPlayer.Volume = (int)_appSettings.Volume;
            volumeSlider.ValueChanged += OnVolumeChanged;

            _currentRepeatMode = (RepeatMode)_appSettings.RepeatMode;
            UpdateRepeatIcon();

            if (!string.IsNullOrEmpty(_appSettings.MusicFolderPath) && Directory.Exists(_appSettings.MusicFolderPath))
            {
                await LoadFilesFromFolder(_appSettings.MusicFolderPath);
            }
        }

        private void ShowPopup(string message, bool showOkButton)
        {
            var popupOverlay = this.FindControl<Grid>("PopupOverlay");
            _updatePopup.SetMessage(message, showOkButton);
            popupOverlay.IsVisible = true;
        }

        private void HidePopup()
        {
            var popupOverlay = this.FindControl<Grid>("PopupOverlay");
            popupOverlay.IsVisible = false;
        }

        private void SettingsButton_Click(object? sender, RoutedEventArgs e)
        {
            var mainContentArea = this.FindControl<ContentControl>("MainContentArea");
            mainContentArea.Content = _settingsView;
        }

        private void ShowMainContent()
        {
            var mainContentArea = this.FindControl<ContentControl>("MainContentArea");
            var albumArtImage = this.FindControl<Image>("AlbumArtImage");
            mainContentArea.Content = albumArtImage.Parent; // Go back to the border containing the image
        }

        private async void OnFolderSelectedInSettings(string folderPath)
        {
            _appSettings.MusicFolderPath = folderPath;
            await _dbService.SaveSettingsAsync(_appSettings);
            await LoadFilesFromFolder(folderPath);
            ShowMainContent();
        }

        private async Task LoadFilesFromFolder(string folderPath)
        {
            var nowPlayingInfoText = this.FindControl<TextBlock>("NowPlayingInfoText");
            nowPlayingInfoText.Text = "Loading...";

            var supportedExtensions = new[] { ".mp3", ".flac", ".m4a", ".mp4" };
            var files = Directory.EnumerateFiles(folderPath, "*.*", SearchOption.AllDirectories)
                                 .Where(f => supportedExtensions.Contains(Path.GetExtension(f).ToLower()));

            var loadedPlaylist = new List<PlaylistItem>();
            await Task.Run(() =>
            {
                foreach (var file in files)
                {
                    try
                    {
                        using var tagFile = TagLib.File.Create(file);
                        var item = new PlaylistItem
                        {
                            Title = string.IsNullOrWhiteSpace(tagFile.Tag.Title) ? Path.GetFileNameWithoutExtension(file) : tagFile.Tag.Title,
                            Artist = string.IsNullOrWhiteSpace(tagFile.Tag.FirstPerformer) ? "Unknown Artist" : tagFile.Tag.FirstPerformer,
                            FilePath = file
                        };
                        if (tagFile.Tag.Pictures.Length > 0)
                        {
                            using var stream = new MemoryStream(tagFile.Tag.Pictures[0].Data.Data);
                            item.AlbumArt = new Bitmap(stream);
                        }
                        loadedPlaylist.Add(item);
                    }
                    catch { /* Ignore invalid files */ }
                }
            });

            _masterPlaylist = loadedPlaylist;
            UpdatePlaylistDisplay(_masterPlaylist);
            nowPlayingInfoText.Text = "Select a song to play";
        }

        private void UpdatePlaylistDisplay(IEnumerable<PlaylistItem> items)
        {
            var playlistListBox = this.FindControl<ListBox>("PlaylistListBox");
            playlistListBox.ItemsSource = items;
        }

        private void Playlist_DoubleTapped(object? sender, TappedEventArgs e)
        {
            var playlistListBox = this.FindControl<ListBox>("PlaylistListBox");
            if (playlistListBox.SelectedItem is PlaylistItem selectedItem)
            {
                _currentQueue = (playlistListBox.ItemsSource as IEnumerable<PlaylistItem>).ToList();
                _currentQueueIndex = _currentQueue.IndexOf(selectedItem);
                PlayFile(selectedItem);
            }
        }

        private void PlayFile(PlaylistItem item)
        {
            if (item == null) return;

            if (_mediaPlayer.IsPlaying) _mediaPlayer.Stop();

            var media = new Media(_libVLC, new Uri(item.FilePath));
            _mediaPlayer.Media = media;
            _mediaPlayer.Play();
            media.Dispose();

            var playPauseIcon = this.FindControl<MaterialIcon>("PlayPauseIcon");
            playPauseIcon.Kind = MaterialIconKind.Pause;
            UpdateNowPlaying(item);
        }

        private void UpdateNowPlaying(PlaylistItem? item)
        {
            var nowPlayingInfoText = this.FindControl<TextBlock>("NowPlayingInfoText");
            var albumArtImage = this.FindControl<Image>("AlbumArtImage");
            if (item != null)
            {
                nowPlayingInfoText.Text = $"{item.Artist} - {item.Title}";
                albumArtImage.Source = item.AlbumArt;
            }
        }

        private void OnEndReached(object? sender, EventArgs e)
        {
            Dispatcher.UIThread.InvokeAsync(() =>
            {
                if (_currentRepeatMode == RepeatMode.RepeatTrack && _currentQueueIndex > -1)
                {
                    PlayFile(_currentQueue[_currentQueueIndex]);
                }
                else
                {
                    PlayNext();
                }
            });
        }

        private void PlayNext()
        {
            if (_currentQueue.Count == 0) return;
            _currentQueueIndex++;
            if (_currentQueueIndex >= _currentQueue.Count)
            {
                if (_currentRepeatMode == RepeatMode.RepeatPlaylist)
                {
                    _currentQueueIndex = 0;
                }
                else
                {
                    StopPlayback();
                    return;
                }
            }
            PlayFile(_currentQueue[_currentQueueIndex]);
        }

        private void PlayPrevious()
        {
            if (_currentQueue.Count == 0) return;
            _currentQueueIndex--;
            if (_currentQueueIndex < 0)
            {
                _currentQueueIndex = _currentRepeatMode == RepeatMode.RepeatPlaylist ? _currentQueue.Count - 1 : 0;
            }
            PlayFile(_currentQueue[_currentQueueIndex]);
        }

        private void StopPlayback()
        {
            _mediaPlayer.Stop();
            var playPauseIcon = this.FindControl<MaterialIcon>("PlayPauseIcon");
            var nowPlayingInfoText = this.FindControl<TextBlock>("NowPlayingInfoText");
            playPauseIcon.Kind = MaterialIconKind.Play;
            nowPlayingInfoText.Text = "Playlist finished";
        }

        private void ShuffleButton_Click(object? sender, RoutedEventArgs e)
        {
            _isShuffleActive = !_isShuffleActive;
            var shuffleIcon = this.FindControl<MaterialIcon>("ShuffleIcon");
            shuffleIcon.Foreground = _isShuffleActive ? new SolidColorBrush(Color.FromRgb(0, 191, 255)) : Brushes.White;

            var playlistListBox = this.FindControl<ListBox>("PlaylistListBox");
            if (_isShuffleActive)
            {
                var random = new Random();
                var shuffledList = (playlistListBox.ItemsSource as IEnumerable<PlaylistItem>).OrderBy(x => random.Next()).ToList();
                UpdatePlaylistDisplay(shuffledList);
            }
            else
            {
                var searchBox = this.FindControl<Avalonia.Controls.TextBox>("SearchBox");
                SearchBox_OnTextChanged(searchBox, null); // Re-apply search/sort
            }
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
            var repeatIcon = this.FindControl<MaterialIcon>("RepeatIcon");
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
            if (_mediaPlayer.IsPlaying)
            {
                _mediaPlayer.Pause();
                playPauseIcon.Kind = MaterialIconKind.Play;
            }
            else
            {
                if (_mediaPlayer.Media != null)
                {
                    _mediaPlayer.Play();
                    playPauseIcon.Kind = MaterialIconKind.Pause;
                }
                else if (_currentQueue.Count > 0)
                {
                    PlayNext();
                }
            }
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
            var positionSlider = this.FindControl<Slider>("PositionSlider");
            if (_mediaPlayer.Media != null) _mediaPlayer.Time = (long)positionSlider.Value;
        }

        private void SearchBox_OnTextChanged(object? sender, TextChangedEventArgs e)
        {
            var searchBox = sender as Avalonia.Controls.TextBox;
            var searchText = searchBox.Text;
            if (string.IsNullOrWhiteSpace(searchText))
            {
                UpdatePlaylistDisplay(_masterPlaylist.OrderBy(p => p.Artist).ThenBy(p => p.Title));
            }
            else
            {
                var filteredList = _masterPlaylist.Where(p =>
                    p.Title.Contains(searchText, StringComparison.OrdinalIgnoreCase) ||
                    p.Artist.Contains(searchText, StringComparison.OrdinalIgnoreCase)
                ).OrderBy(p => p.Artist).ThenBy(p => p.Title).ToList();
                UpdatePlaylistDisplay(filteredList);
            }
        }

        private void OnTimeChanged(object? sender, MediaPlayerTimeChangedEventArgs e)
        {
            var positionSlider = this.FindControl<Slider>("PositionSlider");
            var timeLabel = this.FindControl<TextBlock>("TimeLabel");
            Dispatcher.UIThread.InvokeAsync(() =>
            {
                positionSlider.Value = e.Time;
                timeLabel.Text = TimeSpan.FromMilliseconds(e.Time).ToString(@"mm\:ss");
            });
        }

        private void OnLengthChanged(object? sender, MediaPlayerLengthChangedEventArgs e)
        {
            var positionSlider = this.FindControl<Slider>("PositionSlider");
            var durationLabel = this.FindControl<TextBlock>("DurationLabel");
            Dispatcher.UIThread.InvokeAsync(() =>
            {
                positionSlider.Maximum = e.Length;
                durationLabel.Text = TimeSpan.FromMilliseconds(e.Length).ToString(@"mm\:ss");
            });
        }
    }
}