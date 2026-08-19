using System;
using System.Globalization;
using Avalonia.Data;
using Avalonia.Data.Converters;
using Avalonia.Media;
using DrawingColor = System.Drawing.Color;

namespace Mapping_Tools.Components.Domain {
    /// <summary>
    /// Changes the <see cref="DrawingColor"/> of the core to the <see cref="Color"/>
    /// that Avalonia controls need, and back.
    /// </summary>
    /// <remarks>
    /// The core holds colours as <see cref="DrawingColor"/>, which is portable. Avalonia
    /// has its own colour type and no standard conversion between the two, so a colour
    /// picker cannot bind to a core colour without this converter.
    /// <para>
    /// The WPF project calls this same job <c>DrawingColorToMediaColorConverter</c>.
    /// "Media" is the WPF namespace, so the name changed here.
    /// </para>
    /// </remarks>
    public class DrawingColorToUiColorConverter : IValueConverter {
        public object Convert(object value, Type targetType, object parameter, CultureInfo culture) {
            return value switch {
                DrawingColor drawingColor => Color.FromArgb(
                    drawingColor.A, drawingColor.R, drawingColor.G, drawingColor.B),
                Color uiColor => uiColor,
                _ => BindingOperations.DoNothing
            };
        }

        public object ConvertBack(object value, Type targetType, object parameter, CultureInfo culture) {
            return value switch {
                Color uiColor => DrawingColor.FromArgb(uiColor.A, uiColor.R, uiColor.G, uiColor.B),
                DrawingColor drawingColor => drawingColor,
                _ => BindingOperations.DoNothing
            };
        }
    }

    /// <summary>
    /// Changes a colour to a <see cref="SolidColorBrush"/>, and back. It takes both the
    /// Avalonia colour and the <see cref="DrawingColor"/> that the core holds.
    /// </summary>
    public class ColorToBrushConverter : IValueConverter {
        public object Convert(object value, Type targetType, object parameter, CultureInfo culture) {
            return value switch {
                Color color => new SolidColorBrush(color),
                DrawingColor drawingColor => new SolidColorBrush(
                    Color.FromArgb(drawingColor.A, drawingColor.R, drawingColor.G, drawingColor.B)),
                _ => BindingOperations.DoNothing
            };
        }

        public object ConvertBack(object value, Type targetType, object parameter, CultureInfo culture) {
            if (value is ISolidColorBrush brush) {
                // Give back the type that the binding source holds.
                if (targetType == typeof(DrawingColor)) {
                    return DrawingColor.FromArgb(
                        brush.Color.A, brush.Color.R, brush.Color.G, brush.Color.B);
                }
                return brush.Color;
            }

            return targetType == typeof(DrawingColor) ? default(DrawingColor) : default(Color);
        }
    }

    /// <summary>A colour, as "#RRGGBB".</summary>
    public class ColorToStringConverter : IValueConverter {
        public object Convert(object value, Type targetType, object parameter, CultureInfo culture) {
            var color = value switch {
                Color uiColor => uiColor,
                DrawingColor drawingColor => Color.FromArgb(
                    drawingColor.A, drawingColor.R, drawingColor.G, drawingColor.B),
                _ => (Color?)null
            };
            if (color is null) return string.Empty;

            // Drop the alpha pair, which the user does not edit here.
            var text = color.Value.ToString();
            return text.Length == 9 ? "#" + text.Substring(3) : text;
        }

        public object ConvertBack(object value, Type targetType, object parameter, CultureInfo culture) {
            if (value is not string str) return ConversionError.Fail("Cannot convert back null.");

            if (str.Length > 0 && str[0] != '#') str = "#" + str;

            if (!Color.TryParse(str, out var color)) {
                return ConversionError.Fail("Color format error.");
            }

            return targetType == typeof(DrawingColor)
                ? DrawingColor.FromArgb(color.A, color.R, color.G, color.B)
                : color;
        }
    }
}
