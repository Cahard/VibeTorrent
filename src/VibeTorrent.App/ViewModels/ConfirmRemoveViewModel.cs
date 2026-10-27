using CommunityToolkit.Mvvm.ComponentModel;
using VibeTorrent.Core.Localization;
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
    public string FileCountText => L.F("{0} files", FileCount);

    [ObservableProperty]
    private bool deleteData;
}
