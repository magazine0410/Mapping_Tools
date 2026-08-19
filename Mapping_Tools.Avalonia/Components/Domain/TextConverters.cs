using System;
using System.Globalization;
using System.IO;
using System.Linq;
using System.Text;
using System.Text.RegularExpressions;
using Avalonia.Data.Converters;
using Mapping_Tools.Classes.BeatmapHelper;
using Mapping_Tools.Classes.BeatmapHelper.BeatDivisors;
using Mapping_Tools.Classes.SystemTools;

namespace Mapping_Tools.Components.Domain {
    /// <summary>A number, as text. 727 reads back as "727 WYSI".</summary>
    public class DoubleToStringConverter : IValueConverter {
        public object Convert(object value, Type targetType, object parameter, CultureInfo culture) {
            if (value is double d) {
                return d == 727 ? "727 WYSI" : d.ToString(CultureInfo.InvariantCulture);
            }
            return parameter != null ? parameter.ToString() : string.Empty;
        }

        public object ConvertBack(object value, Type targetType, object parameter, CultureInfo culture) {
            if (value == null) {
                return parameter != null
                    ? double.Parse(parameter.ToString()!, CultureInfo.InvariantCulture)
                    : ConversionError.Fail("Cannot convert back null.");
            }

            if (value.ToString() == "727 WYSI") return 727d;

            if (parameter == null) {
                return TypeConverters.TryParseDouble(value.ToString(), out double parsed)
                    ? parsed
                    : ConversionError.Fail("Double format error.");
            }

            TypeConverters.TryParseDouble(value.ToString(), out double fallback,
                double.Parse(parameter.ToString()!, CultureInfo.InvariantCulture));
            return fallback;
        }
    }

    /// <summary>A whole number, as text. 727 reads back as "727 WYSI".</summary>
    public class IntToStringConverter : IValueConverter {
        public object Convert(object value, Type targetType, object parameter, CultureInfo culture) {
            if (value is int i) {
                return i == 727 ? "727 WYSI" : i.ToString(CultureInfo.InvariantCulture);
            }
            return parameter != null ? parameter.ToString() : string.Empty;
        }

        public object ConvertBack(object value, Type targetType, object parameter, CultureInfo culture) {
            if (value == null) {
                return parameter is int fallbackValue
                    ? fallbackValue
                    : ConversionError.Fail("Cannot convert back null.");
            }

            if (value.ToString() == "727 WYSI") return 727;

            if (parameter == null) {
                return TypeConverters.TryParseInt(value.ToString(), out int parsed)
                    ? parsed
                    : ConversionError.Fail("Int format error.");
            }

            TypeConverters.TryParseInt(value.ToString(), out int fallback,
                int.Parse(parameter.ToString()!, CultureInfo.InvariantCulture));
            return fallback;
        }
    }

    /// <summary>Milliseconds, as an osu! timestamp.</summary>
    public class TimeToStringConverter : IValueConverter {
        public object Convert(object value, Type targetType, object parameter, CultureInfo culture) {
            if (value is not double timeValue) return string.Empty;

            var timeSpan = TimeSpan.FromMilliseconds(timeValue);

            try {
                return parameter != null
                    ? value.ToInvariant()
                    : $"{(timeSpan.Days > 0 ? $"{timeSpan.Days:####}:" : string.Empty)}" +
                      $"{(timeSpan.Hours > 0 ? $"{timeSpan.Hours:00}:" : string.Empty)}" +
                      $"{timeSpan.Minutes:00}:{timeSpan.Seconds:00}:{timeSpan.Milliseconds:000}";
            } catch (OverflowException) {
                return value.ToInvariant();
            }
        }

        public object ConvertBack(object value, Type targetType, object parameter, CultureInfo culture) {
            if (value is not string str) return null;

            try {
                return TypeConverters.ParseOsuTimestamp(str).TotalMilliseconds;
            } catch (Exception e) {
                Console.WriteLine(e);
            }

            if (parameter is string s) {
                return TypeConverters.TryParseDouble(str, out double parsed)
                    ? parsed
                    : double.Parse(s, CultureInfo.InvariantCulture);
            }

            return TypeConverters.TryParseDouble(str, out double result)
                ? result
                : ConversionError.Fail("Time format error.");
        }
    }

    /// <summary>A volume of 0 to 1, as a percentage.</summary>
    public class VolumeToPercentageConverter : IValueConverter {
        public object Convert(object value, Type targetType, object parameter, CultureInfo culture) {
            return value is double d ? (d * 100).ToString(CultureInfo.InvariantCulture) : string.Empty;
        }

        public object ConvertBack(object value, Type targetType, object parameter, CultureInfo culture) {
            TypeConverters.TryParseDouble(value?.ToString(), out double result,
                double.Parse(parameter?.ToString() ?? "0", CultureInfo.InvariantCulture));
            return result / 100;
        }
    }

    /// <summary>Numbers, as a comma separated list.</summary>
    public class DoubleArrayToStringConverter : IValueConverter {
        public object Convert(object value, Type targetType, object parameter, CultureInfo culture) {
            if (value is not double[] values) return string.Empty;

            var builder = new StringBuilder();
            bool first = true;
            foreach (var d in values) {
                if (!first) builder.Append(", ");
                builder.Append(d.ToInvariant());
                first = false;
            }

            return builder.ToString();
        }

        public object ConvertBack(object value, Type targetType, object parameter, CultureInfo culture) {
            if (value is not string str) return Array.Empty<double>();
            if (string.IsNullOrWhiteSpace(str)) return Array.Empty<double>();

            var parts = str.Split(',');
            var result = new double[parts.Length];

            for (int i = 0; i < parts.Length; i++) {
                if (!TypeConverters.TryParseDouble(parts[i], out double parsed)) {
                    return ConversionError.Fail("Double format error.");
                }
                result[i] = parsed;
            }

            return result;
        }
    }

    /// <summary>Beat divisors, as a comma separated list of fractions.</summary>
    public class BeatDivisorArrayToStringConverter : IValueConverter {
        public object Convert(object value, Type targetType, object parameter, CultureInfo culture) {
            if (value is not IBeatDivisor[] beatDivisors) return string.Empty;

            var builder = new StringBuilder();
            bool first = true;
            foreach (var beatDivisor in beatDivisors) {
                if (!first) builder.Append(", ");

                switch (beatDivisor) {
                    case RationalBeatDivisor rbd:
                        builder.Append($"{rbd.Numerator.ToInvariant()}/{rbd.Denominator.ToInvariant()}");
                        break;
                    case IrrationalBeatDivisor ibd:
                        builder.Append(ibd.GetValue().ToInvariant());
                        break;
                }

                first = false;
            }

            return builder.ToString();
        }

        public object ConvertBack(object value, Type targetType, object parameter, CultureInfo culture) {
            if (value is not string str) return Array.Empty<IBeatDivisor>();

            var parts = str.Split(',');
            var beatDivisors = new IBeatDivisor[parts.Length];

            for (int i = 0; i < parts.Length; i++) {
                var part = parts[i];

                // A positive fraction, with no zero above or below the line.
                if (Regex.IsMatch(part, "^[\\s]*[1-9][0-9]*[\\s]*/[\\s]*[1-9][0-9]*[\\s]*$")) {
                    var ndSplit = part.Split('/');
                    beatDivisors[i] = new RationalBeatDivisor(
                        int.Parse(ndSplit[0], CultureInfo.InvariantCulture),
                        int.Parse(ndSplit[1], CultureInfo.InvariantCulture));
                    continue;
                }

                if (!TypeConverters.TryParseDouble(part, out double doubleValue)) {
                    return ConversionError.Fail("Double format error.");
                }
                if (doubleValue <= 0) {
                    return ConversionError.Fail("Beat divisor must be greater than zero.");
                }

                beatDivisors[i] = new IrrationalBeatDivisor(doubleValue);
            }

            return beatDivisors;
        }
    }

    /// <summary>Text, as a list split on the bar.</summary>
    public class StringArrayToStringConverter : IValueConverter {
        public object Convert(object value, Type targetType, object parameter, CultureInfo culture) {
            return value is string[] parts ? string.Join("|", parts) : string.Empty;
        }

        public object ConvertBack(object value, Type targetType, object parameter, CultureInfo culture) {
            return value?.ToString()?.Split('|');
        }
    }

    /// <summary>The bar separated map paths, one to a line.</summary>
    public class MapPathStringAddNewLinesConverter : IValueConverter {
        public object Convert(object value, Type targetType, object parameter, CultureInfo culture) {
            return value is string str ? str.Replace('|', '\n') : string.Empty;
        }

        public object ConvertBack(object value, Type targetType, object parameter, CultureInfo culture) {
            return value is string str ? str.Replace('\n', '|') : string.Empty;
        }
    }

    /// <summary>The bar separated map paths, without their folders.</summary>
    public class MapPathStringJustFilenameConverter : IValueConverter {
        public object Convert(object value, Type targetType, object parameter, CultureInfo culture) {
            return value is string str
                ? string.Join(" | ", str.Split('|').Select(Path.GetFileName))
                : string.Empty;
        }

        public object ConvertBack(object value, Type targetType, object parameter, CultureInfo culture) {
            throw new InvalidOperationException(
                "MapPathStringJustFilenameConverter can not convert back values.");
        }
    }

    /// <summary>How many maps the bar separated map paths hold.</summary>
    public class MapPathStringToCountStringConverter : IValueConverter {
        public object Convert(object value, Type targetType, object parameter, CultureInfo culture) {
            int count = 0;
            if (value is string str && !string.IsNullOrEmpty(str)) {
                count = str.Split('|').Length;
            }
            return count == 1 ? $"({count}) map total" : $"({count}) maps total";
        }

        public object ConvertBack(object value, Type targetType, object parameter, CultureInfo culture) {
            return string.Empty;
        }
    }
}
