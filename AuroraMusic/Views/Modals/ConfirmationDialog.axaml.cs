using Avalonia.Controls;
using Avalonia.Interactivity;
using System.Threading.Tasks;

namespace AuroraMusic.Views.Modals
{
    public partial class ConfirmationDialog : Window // Changed from UserControl to Window
    {
        private readonly TaskCompletionSource<bool> _tcs;
        private readonly TextBlock _messageTextBlock;

        public ConfirmationDialog() // Parameterless constructor for XAML previewer
        {
            InitializeComponent();
            _tcs = new TaskCompletionSource<bool>();
            _messageTextBlock = this.FindControl<TextBlock>("MessageTextBlock")!;

            this.FindControl<Button>("YesButton")!.Click += YesButton_Click;
            this.FindControl<Button>("NoButton")!.Click += NoButton_Click;
        }

        public ConfirmationDialog(string message) : this()
        {
            _messageTextBlock.Text = message;
        }

        // Removed the Show(Window parent) method

        private void YesButton_Click(object? sender, RoutedEventArgs e)
        {
            _tcs.SetResult(true);
            this.Close(); // Changed from (this.Parent as Window)?.Close() to this.Close()
        }

        private void NoButton_Click(object? sender, RoutedEventArgs e)
        {
            _tcs.SetResult(false);
            this.Close(); // Changed from (this.Parent as Window)?.Close() to this.Close()
        }

        // New static method to show the dialog
        public static Task<bool> Show(Window parent, string message)
        {
            var dialog = new ConfirmationDialog(message);
            dialog.ShowDialog(parent);
            return dialog._tcs.Task;
        }
    }
}