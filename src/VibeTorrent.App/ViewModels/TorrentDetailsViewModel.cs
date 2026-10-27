using System.Collections.ObjectModel;
using CommunityToolkit.Mvvm.ComponentModel;
using VibeTorrent.Core.Models;
using VibeTorrent.Core.Utilities;

namespace VibeTorrent.App.ViewModels;

public sealed class TorrentDetailsViewModel : ObservableObject
{
    private TorrentDetailsSnapshot? _snapshot;
    private int _selectedTabIndex;

    public ObservableCollection<TorrentFileSnapshot> Files { get; } = [];
    public ObservableCollection<TrackerSnapshot> Trackers { get; } = [];
    public ObservableCollection<PeerSnapshot> Peers { get; } = [];

    public TorrentDetailsSnapshot? Snapshot => _snapshot;

    public TorrentSnapshot? Summary => _snapshot?.Summary;
    public string? Comment => _snapshot?.Comment;
    public string? CreatedBy => _snapshot?.CreatedBy;
    public DateTime? CreationDate => _snapshot?.CreationDate;

    public int FileCount => Files.Count;
    public int TrackerCount => Trackers.Count;
    public int PeerCount => Peers.Count;
    public string FileCountText => FileCount == 1 ? "1 файл" : $"{FileCount:N0} файлов";
    public string TrackerCountText => $"{TrackerCount:N0}";
    public string PeerCountText => $"{PeerCount:N0}";

    public string SelectedFilesText
    {
        get
        {
            var selected = Files.Count(f => f.IsSelected);
            return $"{selected:N0} из {FileCount:N0}";
        }
    }

    public string SelectedFilesSizeText =>
        DisplayFormat.Bytes(Files.Where(f => f.IsSelected).Sum(f => f.Length));

    public string TotalFilesSizeText => DisplayFormat.Bytes(Files.Sum(f => f.Length));

    public int SelectedTabIndex
    {
        get => _selectedTabIndex;
        set
        {
            if (SetProperty(ref _selectedTabIndex, value))
            {
                OnPropertyChanged(nameof(IsOverviewTab));
                OnPropertyChanged(nameof(IsFilesTab));
                OnPropertyChanged(nameof(IsTrackersTab));
                OnPropertyChanged(nameof(IsPeersTab));
            }
        }
    }

    public bool IsOverviewTab => SelectedTabIndex == 0;
    public bool IsFilesTab => SelectedTabIndex == 1;
    public bool IsTrackersTab => SelectedTabIndex == 2;
    public bool IsPeersTab => SelectedTabIndex == 3;

    /// <summary>
    /// Updates the summary and patches the three observable collections in
    /// place. Replacing ItemsSource wholesale every two seconds resets scroll
    /// position and selection, which made large file lists unusable.
    /// </summary>
    public void Update(TorrentDetailsSnapshot snapshot)
    {
        _snapshot = snapshot;

        SyncCollection(Files, snapshot.Files, static file => file.RelativePath,
            static (left, right) => left == right, reorder: true);
        SyncCollection(Trackers, snapshot.Trackers, static tracker => tracker.Url,
            static (left, right) => left == right, reorder: true);
        SyncPreservingOrder(Peers, snapshot.Peers, static peer => peer.Address,
            static (left, right) => left == right);

        OnPropertyChanged(nameof(Snapshot));
        OnPropertyChanged(nameof(Summary));
        OnPropertyChanged(nameof(Comment));
        OnPropertyChanged(nameof(CreatedBy));
        OnPropertyChanged(nameof(CreationDate));
        OnPropertyChanged(nameof(FileCount));
        OnPropertyChanged(nameof(TrackerCount));
        OnPropertyChanged(nameof(PeerCount));
        OnPropertyChanged(nameof(FileCountText));
        OnPropertyChanged(nameof(TrackerCountText));
        OnPropertyChanged(nameof(PeerCountText));
        OnPropertyChanged(nameof(SelectedFilesText));
        OnPropertyChanged(nameof(SelectedFilesSizeText));
        OnPropertyChanged(nameof(TotalFilesSizeText));
    }

    public void Clear()
    {
        _snapshot = null;
        Files.Clear();
        Trackers.Clear();
        Peers.Clear();
        OnPropertyChanged(string.Empty);
    }

    private static void SyncPreservingOrder<T>(
        ObservableCollection<T> target,
        IReadOnlyList<T> incoming,
        Func<T, string> keySelector,
        Func<T, T, bool> equals)
    {
        var incomingByKey = new Dictionary<string, T>(StringComparer.OrdinalIgnoreCase);
        foreach (var item in incoming)
        {
            incomingByKey.TryAdd(keySelector(item), item);
        }

        for (var i = target.Count - 1; i >= 0; i--)
        {
            if (!incomingByKey.TryGetValue(keySelector(target[i]), out var next))
            {
                target.RemoveAt(i);
            }
            else if (!equals(target[i], next))
            {
                target[i] = next;
            }
        }

        var present = new HashSet<string>(target.Select(keySelector), StringComparer.OrdinalIgnoreCase);
        foreach (var item in incoming)
        {
            if (present.Add(keySelector(item)))
            {
                target.Add(item);
            }
        }
    }

    private static void SyncCollection<T>(
        ObservableCollection<T> target,
        IReadOnlyList<T> incoming,
        Func<T, string> keySelector,
        Func<T, T, bool> equals,
        bool reorder)
    {
        var incomingKeys = new HashSet<string>(
            incoming.Select(keySelector), StringComparer.OrdinalIgnoreCase);

        for (var i = target.Count - 1; i >= 0; i--)
        {
            if (!incomingKeys.Contains(keySelector(target[i])))
            {
                target.RemoveAt(i);
            }
        }

        for (var i = 0; i < incoming.Count; i++)
        {
            var item = incoming[i];
            var key = keySelector(item);

            if (i < target.Count &&
                string.Equals(keySelector(target[i]), key, StringComparison.OrdinalIgnoreCase))
            {
                if (!equals(target[i], item))
                {
                    target[i] = item;
                }

                continue;
            }

            var existing = -1;
            for (var j = i; j < target.Count; j++)
            {
                if (string.Equals(keySelector(target[j]), key, StringComparison.OrdinalIgnoreCase))
                {
                    existing = j;
                    break;
                }
            }

            if (existing < 0)
            {
                target.Insert(i, item);
                continue;
            }

            if (reorder)
            {
                target.Move(existing, i);
            }

            if (!equals(target[i], item))
            {
                target[i] = item;
            }
        }

        while (target.Count > incoming.Count)
        {
            target.RemoveAt(target.Count - 1);
        }
    }
}
