using Avalonia.Controls;
using Avalonia.Interactivity;
using System.Threading.Tasks;

namespace AuroraMusic.Views.Modals
{
    public partial class NewPlaylistDialog : Window // Changed from UserControl to Window
    {
        private readonly TaskCompletionSource<string?> _tcs;
        private readonly TextBox? _playlistNameTextBox;

        public NewPlaylistDialog()
        {
            InitializeComponent();
            _tcs = new TaskCompletionSource<string?>();
            _playlistNameTextBox = this.FindControl<TextBox>("PlaylistNameTextBox")!;

            this.FindControl<Button>("CreateButton")!.Click += CreateButton_Click;
            this.FindControl<Button>("CancelButton")!.Click += CancelButton_Click;
        }

        // Removed the Show(Window parent) method

        private void CreateButton_Click(object? sender, RoutedEventArgs e)
        {
            _tcs.SetResult(_playlistNameTextBox!.Text);
            this.Close(); // Changed from (this.Parent as Window)?.Close() to this.Close()
        }

        private void CancelButton_Click(object? sender, RoutedEventArgs e)
        {
            _tcs.SetResult(string.Empty);
            this.Close(); // Changed from (this.Parent as Window)?.Close() to this.Close()
        }

        // New static method to show the dialog
        public static new Task<string?> Show(Window parent)
        {
            var dialog = new NewPlaylistDialog();
            dialog.ShowDialog(parent);
            return dialog._tcs.Task;
        }
    }
}