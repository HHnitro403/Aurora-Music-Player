using AuroraMusic.Data;
using AuroraMusic.Models;
using Avalonia.Controls;
using Avalonia.Interactivity;
using Avalonia.Platform.Storage;
using Microsoft.EntityFrameworkCore;
using System;
using System.Linq;

namespace AuroraMusic.Views;

public partial class SettingsView : UserControl
{
    public event Action<string>? FolderSelected;
    public event Action? GoBackRequested;

    private readonly MusicDbContext _context = new MusicDbContext();

    public SettingsView()
    {
        InitializeComponent();
        LoadFolders();
    }

    private void LoadFolders()
    {
        FoldersListBox.ItemsSource = _context.Folders.ToList();
    }

    private async void SelectFolderButton_Click(object? sender, RoutedEventArgs e)
    {
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
            var folder = new Folder { Path = folderPath };
            _context.Folders.Add(folder);
            await _context.SaveChangesAsync();
            LoadFolders();
            FolderSelected?.Invoke(folderPath);
        }
    }

    private async void DeleteFolderButton_Click(object? sender, RoutedEventArgs e)
    {
        if (sender is Button { CommandParameter: Folder folder })
        {
            _context.Folders.Remove(folder);
            await _context.SaveChangesAsync();
            LoadFolders();
        }
    }

    private void BackButton_Click(object? sender, RoutedEventArgs e)
    {
        GoBackRequested?.Invoke();
    }
}