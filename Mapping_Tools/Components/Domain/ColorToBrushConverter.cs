using System;
using System.Globalization;
using System.Windows.Data;
using System.Windows.Media;

namespace Mapping_Tools.Components.Domain
{
    /// <summary>
    /// Changes the <see cref="Color"/> to <see cref="SolidColorBrush"/> and back.
    /// </summary>
    public class ColorToBrushConverter : IValueConverter
    {
        /// <summary>
        /// 
        /// </summary>
        /// <param name="value"></param>
        /// <param name="targetType"></param>
        /// <param name="parameter"></param>
        /// <param name="culture"></param>
        /// <returns></returns>
        public object Convert(object value, Type targetType, object parameter, CultureInfo culture)
        {
            switch (value)
            {
                case Color color:
                    return new SolidColorBrush(color);
                // The core holds colours as System.Drawing.Color, because
                // System.Windows.Media.Color is part of WPF.
                case System.Drawing.Color drawingColor:
                    return new SolidColorBrush(Color.FromArgb(
                        drawingColor.A, drawingColor.R, drawingColor.G, drawingColor.B));
            }
            return Binding.DoNothing;
        }

        /// <summary>
        /// 
        /// </summary>
        /// <param name="value"></param>
        /// <param name="targetType"></param>
        /// <param name="parameter"></param>
        /// <param name="culture"></param>
        /// <returns></returns>
        public object ConvertBack(object value, Type targetType, object parameter, CultureInfo culture)
        {
            if (value is SolidColorBrush brush)
            {
                // Give back the type that the binding source holds.
                if (targetType == typeof(System.Drawing.Color))
                {
                    return System.Drawing.Color.FromArgb(
                        brush.Color.A, brush.Color.R, brush.Color.G, brush.Color.B);
                }
                return brush.Color;
            }
            return targetType == typeof(System.Drawing.Color)
                ? default(System.Drawing.Color)
                : default(Color);
        }
    }
}
