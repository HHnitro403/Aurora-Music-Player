using AuroraMusic.Models;
using Avalonia.Controls;
using Avalonia.Interactivity;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;

namespace AuroraMusic.Views.Modals
{
    public partial class AddSongToPlaylistDialog : Window
    {
        private readonly TaskCompletionSource<IEnumerable<Song>?> _tcs;
        private readonly ListBox _songsListBox;

        public AddSongToPlaylistDialog() // Parameterless constructor for XAML previewer
        {
            InitializeComponent();
            _tcs = new TaskCompletionSource<IEnumerable<Song>?>();
            _songsListBox = this.FindControl<ListBox>("SongsListBox")!;

            this.FindControl<Button>("AddButton")!.Click += AddButton_Click;
            this.FindControl<Button>("CancelButton")!.Click += CancelButton_Click;
        }

        public AddSongToPlaylistDialog(IEnumerable<Song> songs) : this()
        {
            _songsListBox.ItemsSource = songs;
        }

        // Removed the Show(Window parent) method

        private void AddButton_Click(object? sender, RoutedEventArgs e)
        {
            _tcs.SetResult(_songsListBox.SelectedItems?.Cast<Song>() ?? Enumerable.Empty<Song>());
            this.Close(); // Changed from (this.Parent as Window)?.Close() to this.Close()
        }

        private void CancelButton_Click(object? sender, RoutedEventArgs e)
        {
            _tcs.SetResult(new List<Song>());
            this.Close(); // Changed from (this.Parent as Window)?.Close() to this.Close()
        }

        // New static method to show the dialog
        public static Task<IEnumerable<Song>?> Show(Window parent, IEnumerable<Song> songs)
        {
            var dialog = new AddSongToPlaylistDialog(songs);
            dialog.ShowDialog(parent);
            return dialog._tcs.Task;
        }
    }
}