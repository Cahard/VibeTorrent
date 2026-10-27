using CommunityToolkit.Mvvm.ComponentModel;
using VibeTorrent.Core.Models;

namespace VibeTorrent.App.ViewModels;

public sealed partial class SelectableFileViewModel : ObservableObject
{
    private readonly Action _selectionChanged;

    public SelectableFileViewModel(PreviewFile file, bool selected, Action selectionChanged)
    {
        File = file;
        _selectionChanged = selectionChanged;
        _isSelected = selected;
    }

    public PreviewFile File { get; }
    public string Path => File.Path;
    public string Name => System.IO.Path.GetFileName(File.Path);
    public long Length => File.Length;
    public string SizeText => Core.Utilities.DisplayFormat.Bytes(File.Length);

    [ObservableProperty]
    private bool _isSelected;

    partial void OnIsSelectedChanged(bool value) => _selectionChanged();
}
