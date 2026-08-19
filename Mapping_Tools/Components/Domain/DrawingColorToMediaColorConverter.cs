using System;
using System.Globalization;
using System.Windows.Data;
using DrawingColor = System.Drawing.Color;
using MediaColor = System.Windows.Media.Color;

namespace Mapping_Tools.Components.Domain {
    /// <summary>
    /// Changes the <see cref="DrawingColor"/> of the core to the <see cref="MediaColor"/>
    /// that WPF controls need, and back.
    /// </summary>
    /// <remarks>
    /// The core holds colours as <see cref="DrawingColor"/>, because
    /// <see cref="MediaColor"/> is part of WPF and does not exist on Linux. WPF has no
    /// standard conversion between the two, so a colour picker cannot bind to a core
    /// colour without this converter.
    /// </remarks>
    public class DrawingColorToMediaColorConverter : IValueConverter {
        public object Convert(object value, Type targetType, object parameter, CultureInfo culture) {
            return value switch {
                DrawingColor drawingColor => MediaColor.FromArgb(
                    drawingColor.A, drawingColor.R, drawingColor.G, drawingColor.B),
                MediaColor mediaColor => mediaColor,
                _ => Binding.DoNothing
            };
        }

        public object ConvertBack(object value, Type targetType, object parameter, CultureInfo culture) {
            return value switch {
                MediaColor mediaColor => DrawingColor.FromArgb(
                    mediaColor.A, mediaColor.R, mediaColor.G, mediaColor.B),
                DrawingColor drawingColor => drawingColor,
                _ => Binding.DoNothing
            };
        }
    }
}
