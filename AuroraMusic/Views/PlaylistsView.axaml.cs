using AuroraMusic.Models;
using AuroraMusic.Services;
using AuroraMusic.Views.Modals;
using Avalonia.Controls;
using Avalonia.Input;
using Avalonia.Interactivity;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;

namespace AuroraMusic.Views
{
    public partial class PlaylistsView : UserControl
    {
        private readonly DatabaseService _dbService;
        private readonly PlaylistManager _playlistManager;
        private readonly Window _parentWindow; // Add this field
        private ListBox _playlistsListBox;
        private ListBox _playlistSongsListBox;
        private TextBlock _playlistNameTextBlock;

        public event System.Action<PlaylistItem>? PlayRequested;

        public PlaylistsView(DatabaseService dbService, PlaylistManager playlistManager, Window parentWindow)
        {
            InitializeComponent();
            _dbService = dbService;
            _playlistManager = playlistManager;
            _parentWindow = parentWindow; // Assign the parent window

            _playlistsListBox = this.FindControl<ListBox>("PlaylistsListBox")!;
            _playlistSongsListBox = this.FindControl<ListBox>("PlaylistSongsListBox")!;
            _playlistNameTextBlock = this.FindControl<TextBlock>("PlaylistNameTextBlock")!;

            _playlistsListBox.SelectionChanged += PlaylistsListBox_SelectionChanged;
            _playlistSongsListBox.DoubleTapped += PlaylistSongsListBox_DoubleTapped;
            this.FindControl<Button>("NewPlaylistButton")!.Click += NewPlaylistButton_Click;
            this.FindControl<Button>("AddSongButton")!.Click += AddSongButton_Click;
            this.FindControl<Button>("RemoveSongButton")!.Click += RemoveSongButton_Click;
        }

        public async Task LoadPlaylistsAsync()
        {
            var playlists = await _dbService.GetPlaylistsAsync();
            _playlistsListBox.ItemsSource = playlists;
            if (playlists.Any())
            {
                _playlistsListBox.SelectedIndex = 0;
            }
        }

        private async void NewPlaylistButton_Click(object? sender, RoutedEventArgs e)
        {
            // For simplicity, prompt for name directly. In a real app, use a dialog.
            var newPlaylistName = await GetUserInput("Enter new playlist name:");
            await Task.Yield();
            if (!string.IsNullOrWhiteSpace(newPlaylistName))
            {
                var newPlaylist = new Playlist { Name = newPlaylistName };
                await _dbService.AddPlaylistAsync(newPlaylist);
                await LoadPlaylistsAsync(); // Reload playlists to show the new one
            }
        }

        private async void PlaylistsListBox_SelectionChanged(object? sender, SelectionChangedEventArgs e)
        {
            if (_playlistsListBox.SelectedItem is Playlist selectedPlaylist)
            {
                _playlistNameTextBlock.Text = selectedPlaylist.Name;
                _playlistSongsListBox.ItemsSource = selectedPlaylist.PlaylistItems;
            }
            else
            {
                _playlistNameTextBlock.Text = "Select a Playlist";
                _playlistSongsListBox.ItemsSource = null;
            }
        }

        private void PlaylistSongsListBox_DoubleTapped(object? sender, TappedEventArgs e)
        {
            if (_playlistSongsListBox.SelectedItem is PlaylistItem selectedItem)
            {
                PlayRequested?.Invoke(selectedItem);
            }
        }

        private async void AddSongButton_Click(object? sender, RoutedEventArgs e)
        {
            if (_playlistsListBox.SelectedItem is Playlist selectedPlaylist)
            {
                // In a real app, this would open a song selection dialog
                var songIdInput = await GetUserInput("Enter Song ID to add:");
                if (int.TryParse(songIdInput, out int songId))
                {
                    await _dbService.AddSongToPlaylistAsync(selectedPlaylist.Id, songId);
                    await LoadPlaylistsAsync(); // Reload to update the current playlist view
                }
            }
        }

        private async void RemoveSongButton_Click(object? sender, RoutedEventArgs e)
        {
            if (_playlistsListBox.SelectedItem is Playlist selectedPlaylist && _playlistSongsListBox.SelectedItem is PlaylistItem selectedPlaylistItem)
            {
                await _dbService.RemoveSongFromPlaylistAsync(selectedPlaylist.Id, selectedPlaylistItem.SongId);
                await LoadPlaylistsAsync(); // Reload to update the current playlist view
            }
        }

        // Helper to get user input using the custom InputDialog
        private async Task<string?> GetUserInput(string prompt)
        {
            return await InputDialog.Show(_parentWindow, prompt);
        }
    }
}