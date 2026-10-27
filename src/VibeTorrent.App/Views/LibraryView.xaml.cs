using System.Windows;
using System.Windows.Controls;
using System.Windows.Controls.Primitives;

namespace VibeTorrent.App.Views;

public partial class LibraryView : UserControl
{
    public LibraryView()
    {
        InitializeComponent();
    }

    public void FocusSearch()
    {
        SearchBox.Focus();
        SearchBox.SelectAll();
    }

    private void SortButton_OnClick(object sender, RoutedEventArgs e)
    {
        var menu = SortButton.ContextMenu;
        if (menu is null)
        {
            return;
        }

        menu.DataContext = DataContext;
        menu.PlacementTarget = SortButton;
        menu.Placement = PlacementMode.Bottom;
        menu.IsOpen = true;
    }
}
