using System.Globalization;
using System.Windows.Data;
using System.Windows.Markup;

namespace ZTorrent.App.Localization;

/// <summary>
/// XAML sugar for live localization: {loc:T "All downloads"}.
/// The binding listens to LocalizationManager.Revision and re-applies the
/// translation when the user changes the language.
/// </summary>
[MarkupExtensionReturnType(typeof(string))]
public sealed class T : MarkupExtension
{
    public T()
    {
    }

    public T(string key)
    {
        Key = key;
    }

    public string Key { get; set; } = string.Empty;

    public override object ProvideValue(IServiceProvider serviceProvider)
    {
        var binding = new Binding(nameof(Core.Localization.LocalizationManager.Revision))
        {
            Source = Core.Localization.LocalizationManager.Instance,
            Converter = new LocKeyConverter(),
            ConverterParameter = Key,
            Mode = BindingMode.OneWay
        };
        return binding.ProvideValue(serviceProvider);
    }
}

public sealed class LocKeyConverter : IValueConverter
{
    public object Convert(object? value, Type targetType, object? parameter, CultureInfo culture) =>
        Core.Localization.L.T(parameter?.ToString() ?? string.Empty);

    public object ConvertBack(object? value, Type targetType, object? parameter, CultureInfo culture) =>
        Binding.DoNothing;
}
