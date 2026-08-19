using System;
using System.Globalization;
using Avalonia.Data.Converters;
using Mapping_Tools.Classes.BeatmapHelper.Enums;
using Mapping_Tools.Classes.HitsoundStuff;

namespace Mapping_Tools.Components.Domain {
    /// <summary>
    /// The shared body of the converters that show an enum by its name.
    /// </summary>
    public abstract class EnumToStringConverter<T> : IValueConverter where T : struct, Enum {
        public object Convert(object value, Type targetType, object parameter, CultureInfo culture) {
            return value is T typed ? typed.ToString() : string.Empty;
        }

        public object ConvertBack(object value, Type targetType, object parameter, CultureInfo culture) {
            var str = value?.ToString();
            return Enum.TryParse<T>(str, out var parsed)
                ? parsed
                : ConversionError.Fail($"\"{str}\" is not one of the {typeof(T).Name} values.");
        }
    }

    /// <summary>An <see cref="ImportType"/>, by its name.</summary>
    public class ImportTypeToStringConverter : EnumToStringConverter<ImportType> { }

    /// <summary>A <see cref="Hitsound"/>, by its name.</summary>
    public class HitsoundToStringConverter : EnumToStringConverter<Hitsound> { }

    /// <summary>A <see cref="SampleSet"/>, by its name.</summary>
    public class SampleSetToStringConverter : EnumToStringConverter<SampleSet> { }
}
