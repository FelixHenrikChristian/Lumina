using Microsoft.UI;
using Microsoft.UI.Xaml.Data;
using Microsoft.UI.Xaml.Media;
using Windows.UI;

namespace Lumina.App.Converters;

public sealed class HexColorToBrushConverter : IValueConverter
{
    public object Convert(object value, Type targetType, object parameter, string language)
    {
        var fallback = parameter as string ?? "#000000";
        var colorText = value as string;

        return new SolidColorBrush(ParseColor(colorText) ?? ParseColor(fallback) ?? Colors.Transparent);
    }

    public object ConvertBack(object value, Type targetType, object parameter, string language)
    {
        throw new NotSupportedException();
    }

    private static Color? ParseColor(string? value)
    {
        if (string.IsNullOrWhiteSpace(value))
        {
            return null;
        }

        var hex = value.Trim();
        if (hex[0] == '#')
        {
            hex = hex[1..];
        }

        if (hex.Length != 6 && hex.Length != 8)
        {
            return null;
        }

        if (!hex.All(Uri.IsHexDigit))
        {
            return null;
        }

        var offset = hex.Length == 8 ? 2 : 0;
        var alpha = hex.Length == 8
            ? System.Convert.ToByte(hex[..2], 16)
            : byte.MaxValue;
        var red = System.Convert.ToByte(hex.Substring(offset, 2), 16);
        var green = System.Convert.ToByte(hex.Substring(offset + 2, 2), 16);
        var blue = System.Convert.ToByte(hex.Substring(offset + 4, 2), 16);

        return Color.FromArgb(alpha, red, green, blue);
    }
}
