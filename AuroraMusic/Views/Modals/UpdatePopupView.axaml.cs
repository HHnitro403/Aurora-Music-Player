using Avalonia.Controls;
using Avalonia.Interactivity;
using System;

namespace AuroraMusic.Views.Modals
{
    public partial class UpdatePopupView : Window // Changed from UserControl to Window
    {
        public UpdatePopupView()
        {
            InitializeComponent();
            // OkButton.Click += (s, e) => OkClicked?.Invoke(); // Removed
            OkButton.Click += (s, e) => Close(); // Added to close the window
        }

        public void SetMessage(string message, bool showOkButton)
        {
            MessageText.Text = message;
            OkButton.IsVisible = showOkButton;
        }

        // New static method to show the dialog
        public static UpdatePopupView Show(Window parent, string message, bool showOkButton)
        {
            var dialog = new UpdatePopupView();
            dialog.SetMessage(message, showOkButton);
            dialog.Show(parent); // Use Show() for non-blocking, ShowDialog() for blocking
            return dialog;
        }
    }
}