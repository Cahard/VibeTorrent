using System.Diagnostics;
using System.Windows.Controls;
using System.Windows.Navigation;

namespace VibeTorrent.App.Views;

public partial class SettingsView : UserControl
{
    public SettingsView()
    {
        InitializeComponent();
    }

    private void GitHubLink_OnRequestNavigate(object sender, RequestNavigateEventArgs e)
    {
        try
        {
            Process.Start(new ProcessStartInfo(e.Uri.AbsoluteUri) { UseShellExecute = true });
        }
        catch (Exception)
        {
            // The default browser may be unavailable in a restricted session.
        }

        e.Handled = true;
    }
}
