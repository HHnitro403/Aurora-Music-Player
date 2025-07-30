using Avalonia;
using Avalonia.Controls;
using Avalonia.Markup.Xaml;
using System;

namespace AuroraMusic.Views.Modals // Updated Namespace
{
    public partial class UpdatePopupView : UserControl
    {
        public event Action? OkClicked;

        public UpdatePopupView()
        {
            InitializeComponent();
            OkButton.Click += (s, e) => OkClicked?.Invoke();
        }

        public void SetMessage(string message, bool showOkButton)
        {
            MessageText.Text = message;
            OkButton.IsVisible = showOkButton;
        }
    }
}