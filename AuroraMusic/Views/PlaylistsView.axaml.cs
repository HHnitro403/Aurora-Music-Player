using AuroraMusic.Models;
using AuroraMusic.Services;
using AuroraMusic.Views.Modals;
using Avalonia.Controls;
using Avalonia.Input;
using Avalonia.Interactivity;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using MsBox.Avalonia;
using MsBox.Avalonia.Enums;

namespace AuroraMusic.Views
{
    public partial class PlaylistsView : UserControl
    {
        private readonly DatabaseService? _dbService;
        private readonly PlaylistManager? _playlistManager;
        private readonly Window? _parentWindow; // Add this field
        private ListBox? _playlistsListBox;
        private ListBox? _playlistSongsListBox;
        private TextBlock? _playlistNameTextBlock;

        public event System.Action<PlaylistItem>? PlayRequested;

        public PlaylistsView()
        {
            InitializeComponent();
        }

        public PlaylistsView(DatabaseService dbService, PlaylistManager playlistManager, Window parentWindow)
        {
            InitializeComponent();
            _dbService = dbService;
            _playlistManager = playlistManager;
            _parentWindow = parentWindow; // Assign the parent window

            _playlistsListBox = this.FindControl<ListBox>("PlaylistsListBox");
            _playlistSongsListBox = this.FindControl<ListBox>("PlaylistSongsListBox");
            _playlistNameTextBlock = this.FindControl<TextBlock>("PlaylistNameTextBlock");

            _playlistsListBox!.SelectionChanged += PlaylistsListBox_SelectionChanged;
            _playlistSongsListBox!.DoubleTapped += PlaylistSongsListBox_DoubleTapped;
            this.FindControl<Button>("NewPlaylistButton")!.Click += NewPlaylistButton_Click;
            this.FindControl<Button>("RenamePlaylistButton")!.Click += RenamePlaylistButton_Click;
            this.FindControl<Button>("DeletePlaylistButton")!.Click += DeletePlaylistButton_Click;
            this.FindControl<Button>("AddSongButton")!.Click += AddSongButton_Click;
            this.FindControl<Button>("RemoveSongButton")!.Click += RemoveSongButton_Click;
        }

        public async Task LoadPlaylistsAsync()
        {
            if (_dbService == null) return;
            var playlists = await _dbService.GetPlaylistsAsync();
            _playlistsListBox!.ItemsSource = playlists;
            if (playlists.Any())
            {
                _playlistsListBox.SelectedIndex = 0;
            }
        }

        private async void NewPlaylistButton_Click(object? sender, RoutedEventArgs e)
        {
            if (_dbService == null || _parentWindow == null) return;

            var newPlaylistName = await NewPlaylistDialog.Show(_parentWindow);

            if (!string.IsNullOrWhiteSpace(newPlaylistName))
            {
                var newPlaylist = new Playlist { Name = newPlaylistName };
                await _dbService.AddPlaylistAsync(newPlaylist);
                await LoadPlaylistsAsync(); // Reload playlists to show the new one

                var box = MessageBoxManager.GetMessageBoxStandard("Success", $"Playlist '{newPlaylistName}' created successfully.", ButtonEnum.Ok);
                await box.ShowAsync();
            }
        }

        private async void PlaylistsListBox_SelectionChanged(object? sender, SelectionChangedEventArgs e)
        {
            if (_playlistsListBox!.SelectedItem is Playlist selectedPlaylist)
            {
                _playlistNameTextBlock!.Text = selectedPlaylist.Name;
                if (_dbService != null)
                {
                    var playlist = await _dbService.GetPlaylistByIdAsync(selectedPlaylist.Id);
                    if (playlist != null)
                    {
                        _playlistSongsListBox!.ItemsSource = playlist.PlaylistItems;
                    }
                }
            }
            else
            {
                _playlistNameTextBlock!.Text = "Select a Playlist";
                _playlistSongsListBox!.ItemsSource = null;
            }
        }

        private void PlaylistSongsListBox_DoubleTapped(object? sender, TappedEventArgs e)
        {
            if (_playlistSongsListBox!.SelectedItem is PlaylistItem selectedItem)
            {
                PlayRequested?.Invoke(selectedItem);
            }
        }

        private async void AddSongButton_Click(object? sender, RoutedEventArgs e)
        {
            if (_dbService == null || _playlistsListBox == null || _parentWindow == null) return;
            if (_playlistsListBox.SelectedItem is Playlist selectedPlaylist)
            {
                var allSongs = await _dbService.GetAllSongsAsync();
                var selectedSongs = await AddSongToPlaylistDialog.Show(_parentWindow, allSongs);

                if (selectedSongs != null)
                {
                    foreach (var song in selectedSongs)
                    {
                        await _dbService.AddSongToPlaylistAsync(selectedPlaylist.Id, song.Id);
                    }
                    await LoadPlaylistsAsync(); // Reload to update the current playlist view
                }
            }
        }

        private async void RemoveSongButton_Click(object? sender, RoutedEventArgs e)
        {
            if (_dbService == null || _playlistsListBox == null || _playlistSongsListBox == null) return;
            if (_playlistsListBox.SelectedItem is Playlist selectedPlaylist && _playlistSongsListBox.SelectedItem is PlaylistItem selectedPlaylistItem)
            {
                await _dbService.RemoveSongFromPlaylistAsync(selectedPlaylist.Id, selectedPlaylistItem.SongId);
                await LoadPlaylistsAsync(); // Reload to update the current playlist view
            }
        }

        private async void RenamePlaylistButton_Click(object? sender, RoutedEventArgs e)
        {
            if (_dbService == null || _playlistsListBox == null || _parentWindow == null) return;
            if (_playlistsListBox.SelectedItem is Playlist selectedPlaylist)
            {
                var newPlaylistName = await NewPlaylistDialog.Show(_parentWindow);

                if (!string.IsNullOrWhiteSpace(newPlaylistName))
                {
                    selectedPlaylist.Name = newPlaylistName;
                    await _dbService.UpdatePlaylistAsync(selectedPlaylist);
                    await LoadPlaylistsAsync();

                    var box = MessageBoxManager.GetMessageBoxStandard("Success", $"Playlist renamed to '{newPlaylistName}' successfully.", ButtonEnum.Ok);
                    await box.ShowAsync();
                }
            }
        }

        private async void DeletePlaylistButton_Click(object? sender, RoutedEventArgs e)
        {
            if (_dbService == null || _playlistsListBox == null || _parentWindow == null) return;
            if (_playlistsListBox.SelectedItem is Playlist selectedPlaylist)
            {
                var result = await ConfirmationDialog.Show(_parentWindow, $"Are you sure you want to delete the playlist '{selectedPlaylist.Name}'?");

                if (result)
                {
                    await _dbService.DeletePlaylistAsync(selectedPlaylist.Id);
                    await LoadPlaylistsAsync();

                    var box = MessageBoxManager.GetMessageBoxStandard("Success", $"Playlist '{selectedPlaylist.Name}' deleted successfully.", ButtonEnum.Ok);
                    await box.ShowAsync();
                }
            }
        }
    }
}