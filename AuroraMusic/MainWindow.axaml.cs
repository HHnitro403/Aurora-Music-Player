﻿using AuroraMusic.Data;
using AuroraMusic.Models;
using AuroraMusic.Services;
using AuroraMusic.Views;
using AuroraMusic.Views.Modals;
using Avalonia.Controls;
using Avalonia.Input;
using Avalonia.Interactivity;
using Avalonia.Media;
using Avalonia.Media.Imaging;
using Avalonia.Threading;
using LibVLCSharp.Shared;
using Material.Icons;
using Material.Icons.Avalonia;
using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Threading.Tasks;
using System.Timers;
using TagLib;

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

        private readonly System.Timers.Timer _timer;
        private readonly MusicDbContext _context;

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

            _timer = new System.Timers.Timer(1000); // 1-second interval
            _timer.Elapsed += _timer_Elapsed;
            _timer.AutoReset = true;
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
            try
            {
                var volumeSlider = this.FindControl<Slider>("VolumeSlider");
                if (volumeSlider != null)
                {
                    volumeSlider.Value = _appSettings.Volume;
                    _mediaPlayer.Volume = (int)_appSettings.Volume;
                    volumeSlider.ValueChanged += OnVolumeChanged;
                }

                _currentRepeatMode = (RepeatMode)_appSettings.RepeatMode;
                UpdateRepeatIcon();

                if (!string.IsNullOrEmpty(_appSettings.MusicFolderPath) && Directory.Exists(_appSettings.MusicFolderPath))
                {
                    await LoadFilesFromFolder(_appSettings.MusicFolderPath);
                }
            }
            catch (Exception ex)
            {
                ShowPopup($"An error occurred while applying settings: {ex.Message}", true);
            }
        }

        private void ShowPopup(string message, bool showOkButton)
        {
            Dispatcher.UIThread.InvokeAsync(() =>
            {
                try
                {
                    var popupOverlay = this.FindControl<Grid>("PopupOverlay");
                    if (popupOverlay != null)
                    {
                        _updatePopup.SetMessage(message, showOkButton);
                        popupOverlay.IsVisible = true;
                    }
                }
                catch (Exception ex)
                {
                    Console.WriteLine($"Error showing popup: {ex.Message}");
                }
            });
        }

        private void HidePopup()
        {
            Dispatcher.UIThread.InvokeAsync(() =>
            {
                try
                {
                    var popupOverlay = this.FindControl<Grid>("PopupOverlay");
                    if (popupOverlay != null)
                    {
                        popupOverlay.IsVisible = false;
                    }
                }
                catch (Exception ex)
                {
                    Console.WriteLine($"Error hiding popup: {ex.Message}");
                }
            });
        }

        private void SettingsButton_Click(object? sender, RoutedEventArgs e)
        {
            try
            {
                var mainContentArea = this.FindControl<ContentControl>("MainContentArea");
                if (mainContentArea != null)
                {
                    mainContentArea.Content = _settingsView;
                }
            }
            catch (Exception ex)
            {
                ShowPopup($"An error occurred: {ex.Message}", true);
            }
        }

        private void ShowMainContent()
        {
            try
            {
                var mainContentArea = this.FindControl<ContentControl>("MainContentArea");
                var albumArtImage = this.FindControl<Image>("AlbumArtImage");
                if (mainContentArea != null && albumArtImage != null)
                {
                    mainContentArea.Content = albumArtImage.Parent; // Go back to the border containing the image
                }
            }
            catch (Exception ex)
            {
                ShowPopup($"An error occurred: {ex.Message}", true);
            }
        }

        private async void OnFolderSelectedInSettings(string folderPath)
        {
            try
            {
                _appSettings.MusicFolderPath = folderPath;
                await _dbService.SaveSettingsAsync(_appSettings);
                await LoadFilesFromFolder(folderPath);
                ShowMainContent();
            }
            catch (Exception ex)
            {
                ShowPopup($"An error occurred: {ex.Message}", true);
            }
        }

        private async Task LoadFilesFromFolder(string folderPath)
        {
            try
            {
                var nowPlayingInfoText = this.FindControl<TextBlock>("NowPlayingInfoText");
                if (nowPlayingInfoText != null)
                {
                    nowPlayingInfoText.Text = "Loading...";
                }

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
                        catch (Exception ex)
                        {
                            Console.WriteLine($"Error loading file metadata: {ex.Message}");
                        }
                    }
                });

                _masterPlaylist = loadedPlaylist;
                UpdatePlaylistDisplay(_masterPlaylist);
                if (nowPlayingInfoText != null)
                {
                    nowPlayingInfoText.Text = "Select a song to play";
                }
            }
            catch (Exception ex)
            {
                ShowPopup($"An error occurred while loading files: {ex.Message}", true);
            }
        }

        private void UpdatePlaylistDisplay(IEnumerable<PlaylistItem> items)
        {
            try
            {
                var playlistListBox = this.FindControl<ListBox>("PlaylistListBox");
                if (playlistListBox != null)
                {
                    playlistListBox.ItemsSource = items;
                }
            }
            catch (Exception ex)
            {
                ShowPopup($"An error occurred: {ex.Message}", true);
            }
        }

        private void Playlist_DoubleTapped(object? sender, TappedEventArgs e)
        {
            try
            {
                var playlistListBox = this.FindControl<ListBox>("PlaylistListBox");
                if (playlistListBox != null && playlistListBox.SelectedItem is PlaylistItem selectedItem)
                {
                    _currentQueue = (playlistListBox.ItemsSource as IEnumerable<PlaylistItem>).ToList();
                    _currentQueueIndex = _currentQueue.IndexOf(selectedItem);
                    PlayFile(selectedItem);
                }
            }
            catch (Exception ex)
            {
                ShowPopup($"An error occurred: {ex.Message}", true);
            }
        }

        private void PlayFile(PlaylistItem item)
        {
            try
            {
                if (item == null) return;

                if (_mediaPlayer.IsPlaying) _mediaPlayer.Stop();

                var media = new Media(_libVLC, new Uri(item.FilePath));
                _mediaPlayer.Media = media;
                _mediaPlayer.Play();
                media.Dispose();

                var playPauseIcon = this.FindControl<MaterialIcon>("PlayPauseIcon");
                if (playPauseIcon != null)
                {
                    playPauseIcon.Kind = MaterialIconKind.Pause;
                }
                UpdateNowPlaying(item);
            }
            catch (Exception ex)
            {
                ShowPopup($"An error occurred while playing the file: {ex.Message}", true);
            }
        }

        private void UpdateNowPlaying(PlaylistItem? item)
        {
            try
            {
                var nowPlayingInfoText = this.FindControl<TextBlock>("NowPlayingInfoText");
                var albumArtImage = this.FindControl<Image>("AlbumArtImage");
                if (item != null && nowPlayingInfoText != null && albumArtImage != null)
                {
                    nowPlayingInfoText.Text = $"{item.Artist} - {item.Title}";
                    albumArtImage.Source = item.AlbumArt;
                }
            }
            catch (Exception ex)
            {
                ShowPopup($"An error occurred: {ex.Message}", true);
            }
        }

        private void OnEndReached(object? sender, EventArgs e)
        {
            Dispatcher.UIThread.InvokeAsync(() =>
            {
                try
                {
                    if (_currentRepeatMode == RepeatMode.RepeatTrack && _currentQueueIndex > -1)
                    {
                        PlayFile(_currentQueue[_currentQueueIndex]);
                    }
                    else
                    {
                        PlayNext();
                    }
                }
                catch (Exception ex)
                {
                    ShowPopup($"An error occurred: {ex.Message}", true);
                }
            });
        }

        private void PlayNext()
        {
            try
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
            catch (Exception ex)
            {
                ShowPopup($"An error occurred: {ex.Message}", true);
            }
        }

        private void PlayPrevious()
        {
            try
            {
                if (_currentQueue.Count == 0) return;
                _currentQueueIndex--;
                if (_currentQueueIndex < 0)
                {
                    _currentQueueIndex = _currentRepeatMode == RepeatMode.RepeatPlaylist ? _currentQueue.Count - 1 : 0;
                }
                PlayFile(_currentQueue[_currentQueueIndex]);
            }
            catch (Exception ex)
            {
                ShowPopup($"An error occurred: {ex.Message}", true);
            }
        }

        private void StopPlayback()
        {
            try
            {
                _mediaPlayer.Stop();
                var playPauseIcon = this.FindControl<MaterialIcon>("PlayPauseIcon");
                var nowPlayingInfoText = this.FindControl<TextBlock>("NowPlayingInfoText");
                if (playPauseIcon != null)
                {
                    playPauseIcon.Kind = MaterialIconKind.Play;
                }
                if (nowPlayingInfoText != null)
                {
                    nowPlayingInfoText.Text = "Playlist finished";
                }
            }
            catch (Exception ex)
            {
                ShowPopup($"An error occurred: {ex.Message}", true);
            }
        }

        private void ShuffleButton_Click(object? sender, RoutedEventArgs e)
        {
            try
            {
                _isShuffleActive = !_isShuffleActive;
                var shuffleIcon = this.FindControl<MaterialIcon>("ShuffleIcon");
                if (shuffleIcon != null)
                {
                    shuffleIcon.Foreground = _isShuffleActive ? new SolidColorBrush(Color.FromRgb(0, 191, 255)) : Brushes.White;
                }

                var playlistListBox = this.FindControl<ListBox>("PlaylistListBox");
                if (playlistListBox != null)
                {
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
            }
            catch (Exception ex)
            {
                ShowPopup($"An error occurred: {ex.Message}", true);
            }
        }

        private async void RepeatButton_Click(object? sender, RoutedEventArgs e)
        {
            try
            {
                _currentRepeatMode = (RepeatMode)(((int)_currentRepeatMode + 1) % 3);
                UpdateRepeatIcon();
                _appSettings.RepeatMode = (int)_currentRepeatMode;
                await _dbService.SaveSettingsAsync(_appSettings);
            }
            catch (Exception ex)
            {
                ShowPopup($"An error occurred: {ex.Message}", true);
            }
        }

        private void UpdateRepeatIcon()
        {
            try
            {
                var repeatIcon = this.FindControl<MaterialIcon>("RepeatIcon");
                if (repeatIcon != null)
                {
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
            }
            catch (Exception ex)
            {
                ShowPopup($"An error occurred: {ex.Message}", true);
            }
        }

        private void PlayPauseButton_Click(object sender, RoutedEventArgs e)
        {
            try
            {
                var playPauseIcon = this.FindControl<MaterialIcon>("PlayPauseIcon");
                if (playPauseIcon != null)
                {
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
            }
            catch (Exception ex)
            {
                ShowPopup($"An error occurred: {ex.Message}", true);
            }
        }

        private void PreviousButton_Click(object sender, RoutedEventArgs e) => PlayPrevious();

        private void NextButton_Click(object sender, RoutedEventArgs e) => PlayNext();

        private async void OnVolumeChanged(object? sender, Avalonia.Controls.Primitives.RangeBaseValueChangedEventArgs e)
        {
            try
            {
                if (_mediaPlayer != null && _appSettings != null)
                {
                    _mediaPlayer.Volume = (int)e.NewValue;
                    _appSettings.Volume = e.NewValue;
                    await _dbService.SaveSettingsAsync(_appSettings);
                }
            }
            catch (Exception ex)
            {
                ShowPopup($"An error occurred: {ex.Message}", true);
            }
        }

        private bool _isDraggingSlider = false;

        private void OnPositionSliderPressed(object? sender, Avalonia.Input.PointerPressedEventArgs e)
        {
            _isDraggingSlider = true;
        }

        private void OnPositionSliderReleased(object? sender, Avalonia.Input.PointerReleasedEventArgs e)
        {
            try
            {
                var positionSlider = this.FindControl<Slider>("PositionSlider");
                if (positionSlider != null && _mediaPlayer.Media != null)
                {
                    _mediaPlayer.Time = (long)positionSlider.Value;
                }
            }
            catch (Exception ex)
            {
                ShowPopup($"An error occurred: {ex.Message}", true);
            }
            finally
            {
                _isDraggingSlider = false;
            }
        }

        private void SearchBox_OnTextChanged(object? sender, TextChangedEventArgs e)
        {
            try
            {
                var searchBox = sender as Avalonia.Controls.TextBox;
                if (searchBox != null)
                {
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
            }
            catch (Exception ex)
            {
                ShowPopup($"An error occurred: {ex.Message}", true);
            }
        }

        private void OnTimeChanged(object? sender, MediaPlayerTimeChangedEventArgs e)
        {
            if (_isDraggingSlider) return;

            Dispatcher.UIThread.InvokeAsync(() =>
            {
                try
                {
                    var positionSlider = this.FindControl<Slider>("PositionSlider");
                    var timeLabel = this.FindControl<TextBlock>("TimeLabel");
                    if (positionSlider != null && timeLabel != null)
                    {
                        positionSlider.Value = e.Time;
                        timeLabel.Text = TimeSpan.FromMilliseconds(e.Time).ToString(@"mm\:ss");
                    }
                }
                catch (Exception ex)
                {
                    Console.WriteLine($"Error in OnTimeChanged: {ex.Message}");
                }
            });
        }

        private void OnLengthChanged(object? sender, MediaPlayerLengthChangedEventArgs e)
        {
            Dispatcher.UIThread.InvokeAsync(() =>
            {
                try
                {
                    var positionSlider = this.FindControl<Slider>("PositionSlider");
                    var durationLabel = this.FindControl<TextBlock>("DurationLabel");
                    if (positionSlider != null && durationLabel != null)
                    {
                        positionSlider.Maximum = e.Length;
                        durationLabel.Text = TimeSpan.FromMilliseconds(e.Length).ToString(@"mm\:ss");
                    }
                }
                catch (Exception ex)
                {
                    Console.WriteLine($"Error in OnLengthChanged: {ex.Message}");
                }
            });
        }

        private void _timer_Elapsed(object? sender, ElapsedEventArgs e)
        {
            if (_isDraggingSlider) return;

            Dispatcher.UIThread.Post(() =>
            {
                try
                {
                    if (_mediaPlayer != null && _mediaPlayer.IsPlaying)
                    {
                        var positionSlider = this.FindControl<Slider>("PositionSlider");
                        var positionLabel = this.FindControl<TextBlock>("TimeLabel");

                        if (positionSlider != null && positionLabel != null)
                        {
                            positionSlider.Value = _mediaPlayer.Time;
                            positionLabel.Text = TimeSpan.FromMilliseconds(_mediaPlayer.Time).ToString(@"mm\:ss");
                        }
                    }
                }
                catch (Exception ex)
                {
                    Console.WriteLine($"Error in timer elapsed: {ex.Message}");
                }
            });
        }
    }
}