using CommunityToolkit.Mvvm.ComponentModel;
using VibeTorrent.Core.Utilities;

namespace VibeTorrent.App.ViewModels;

public sealed partial class ConfirmRemoveViewModel : ObservableObject
{
    public ConfirmRemoveViewModel(string id, string name, long size, int fileCount)
    {
        Id = id;
        Name = name;
        Size = size;
        FileCount = fileCount;
    }

    public string Id { get; }
    public string Name { get; }
    public long Size { get; }
    public int FileCount { get; }
    public string SizeText => DisplayFormat.Bytes(Size);
    public string FileCountText => FileCount == 1 ? "1 файл" : $"{FileCount:N0} файлов";

    [ObservableProperty]
    private bool deleteData;
}
