using Avalonia.Controls;
using Avalonia.Interactivity;
using System.Threading.Tasks;

namespace AuroraMusic.Views.Modals
{
    public partial class InputDialog : Window
    {
        private TaskCompletionSource<string?> _tcs = new TaskCompletionSource<string?>();

        public InputDialog()
        {
            InitializeComponent();
            OkButton.Click += (sender, e) =>
            {
                _tcs.SetResult(InputTextBox.Text);
                Close();
            };
            CancelButton.Click += (sender, e) =>
            {
                _tcs.SetResult(null);
                Close();
            };
        }

        public static Task<string?> Show(Window parent, string prompt, string defaultText = "")
        {
            var dialog = new InputDialog();
            dialog.PromptTextBlock.Text = prompt;
            dialog.InputTextBox.Text = defaultText;
            dialog.ShowDialog(parent);
            return dialog._tcs.Task;
        }
    }
}