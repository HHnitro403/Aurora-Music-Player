using Avalonia;
using Avalonia.Controls;
using Avalonia.Interactivity;
using Avalonia.Markup.Xaml;
using Avalonia.Platform.Storage;
using System;

namespace AuroraMusic;

public partial class SettingsView : UserControl
{
    // Event to notify the MainWindow that a folder has been selected.
    public event Action<string>? FolderSelected;

    // Event to notify the MainWindow to navigate back.
    public event Action? GoBackRequested;

    public SettingsView()
    {
        InitializeComponent();
    }

    private async void SelectFolderButton_Click(object? sender, RoutedEventArgs e)
    {
        // This is the fix: Call GetTopLevel from the TopLevel class.
        var topLevel = TopLevel.GetTopLevel(this);
        if (topLevel == null) return;

        var folders = await topLevel.StorageProvider.OpenFolderPickerAsync(new FolderPickerOpenOptions
        {
            Title = "Select Music Folder",
            AllowMultiple = false
        });

        if (folders.Count > 0)
        {
            string folderPath = folders[0].Path.LocalPath;
            SelectedFolderText.Text = $"Selected: {folderPath}";
            // Raise the event to pass the folder path to the MainWindow
            FolderSelected?.Invoke(folderPath);
        }
    }

    private void BackButton_Click(object? sender, RoutedEventArgs e)
    {
        // Raise the event to tell MainWindow to go back
        GoBackRequested?.Invoke();
    }
}