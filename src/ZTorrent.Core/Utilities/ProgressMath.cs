namespace ZTorrent.Core.Utilities;

/// <summary>
/// MonoTorrent exposes Progress and BitField.PercentComplete as percentages
/// (0..100). The UI and models use fractions (0..1); every conversion goes
/// through this helper so a missing division cannot regress the progress bar.
/// </summary>
public static class ProgressMath
{
    public static double FromPercent(double percent) =>
        double.IsNaN(percent) ? 0 : Math.Clamp(percent / 100d, 0, 1);
}
