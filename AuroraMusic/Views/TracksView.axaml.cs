using AuroraMusic.Models;
using AuroraMusic.Services;
using Avalonia.Controls;
using Avalonia.Input;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;

namespace AuroraMusic.Views
{
    public partial class TracksView : UserControl
    {
        private readonly PlaylistManager? _playlistManager;
        private readonly DatabaseService? _dbService;
        private AppSettings? _appSettings;
        private SortMode _currentSortMode = SortMode.ArtistAlbum;

        public event Action<PlaylistItem>? PlayRequested;

        public TracksView()
        {
            InitializeComponent();
        }

        public TracksView(PlaylistManager playlistManager, DatabaseService dbService)
        {
            InitializeComponent();
            _playlistManager = playlistManager;
            _dbService = dbService;

            var searchBox = this.FindControl<TextBox>("SearchBox");
            if (searchBox != null)
            {
                searchBox.TextChanged += SearchBox_OnTextChanged;
            }

            var sortComboBox = this.FindControl<ComboBox>("SortComboBox");
            if (sortComboBox != null)
            {
                sortComboBox.ItemsSource = Enum.GetValues(typeof(SortMode));
                // Do not set SelectedIndex here, it will be set by SetAppSettings
                sortComboBox.SelectionChanged += SortComboBox_SelectionChanged;
            }

            var playlistListBox = this.FindControl<ListBox>("PlaylistListBox");
            if (playlistListBox != null)
            {
                playlistListBox.DoubleTapped += Playlist_DoubleTapped;
            }
        }

        public void SetAppSettings(AppSettings appSettings)
        {
            _appSettings = appSettings;
            var sortComboBox = this.FindControl<ComboBox>("SortComboBox");
            if (sortComboBox != null && _appSettings != null)
            {
                _currentSortMode = (SortMode)_appSettings.SortMode;
                sortComboBox.SelectedIndex = (int)_currentSortMode;
            }
        }

        public async Task LoadTracksAsync()
        {
            if (_appSettings == null || _dbService == null || _playlistManager == null) return;
            var folders = await _dbService.GetAllFoldersAsync();
            await _playlistManager.LoadMusicFilesAsync(folders);
            SortAndDisplayPlaylist();
        }

        private void SearchBox_OnTextChanged(object? sender, TextChangedEventArgs e)
        {
            if (sender is not TextBox searchBox) return;
            if (_playlistManager == null) return;

            var searchText = searchBox.Text;
            if (string.IsNullOrWhiteSpace(searchText))
            {
                SortAndDisplayPlaylist();
            }
            else
            {
                var filteredList = _playlistManager.CurrentQueue.Where(p =>
                    p.Title.Contains(searchText, StringComparison.OrdinalIgnoreCase) ||
                    p.Artist.Contains(searchText, StringComparison.OrdinalIgnoreCase)
                ).ToList();
                UpdatePlaylistDisplay(filteredList);
            }
        }

        private async void SortComboBox_SelectionChanged(object? sender, SelectionChangedEventArgs e)
        {
            if (_appSettings is null || _dbService == null || _playlistManager == null) return;

            if (sender is ComboBox { SelectedItem: SortMode sortMode })
            {
                _currentSortMode = sortMode;
                _appSettings.SortMode = (int)sortMode;
                await _dbService.SaveSettingsAsync(_appSettings);
                SortAndDisplayPlaylist();
            }
        }

        private void Playlist_DoubleTapped(object? sender, TappedEventArgs e)
        {
            var playlistListBox = this.FindControl<ListBox>("PlaylistListBox");
            if (playlistListBox?.SelectedItem is PlaylistItem selectedItem && playlistListBox.ItemsSource is IEnumerable<PlaylistItem> items)
            {
                _playlistManager!.SetQueue(items, selectedItem);
                PlayRequested?.Invoke(selectedItem);
            }
        }

        private void SortAndDisplayPlaylist()
        {
            if (_playlistManager == null) return;
            var sortedPlaylist = _playlistManager.GetSortedPlaylist(_currentSortMode);
            UpdatePlaylistDisplay(sortedPlaylist);
        }

        private void UpdatePlaylistDisplay(IEnumerable<PlaylistItem> items)
        {
            var playlistListBox = this.FindControl<ListBox>("PlaylistListBox");
            if (playlistListBox != null)
            {
                playlistListBox.ItemsSource = items.ToList();
            }
        }

        public void SetSelectedItem(PlaylistItem item)
        {
            var playlistListBox = this.FindControl<ListBox>("PlaylistListBox");
            if (playlistListBox != null)
            {
                playlistListBox.SelectedItem = item;
                playlistListBox.ScrollIntoView(item);
            }
        }
    }
}
