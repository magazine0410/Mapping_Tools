using System;
using System.Collections.Generic;
using System.Globalization;
using Avalonia.Data;
using Avalonia.Data.Converters;

namespace Mapping_Tools.Components.Domain {
    /// <summary>
    /// Gives one of two values, by a boolean.
    /// </summary>
    public class BooleanConverter<T> : IValueConverter, IMultiValueConverter {
        public BooleanConverter(T trueValue, T falseValue) {
            True = trueValue;
            False = falseValue;
        }

        public T True { get; set; }
        public T False { get; set; }

        public virtual object Convert(object value, Type targetType, object parameter, CultureInfo culture) {
            return value is true ? True : False;
        }

        public virtual object ConvertBack(object value, Type targetType, object parameter, CultureInfo culture) {
            return value is T typed && EqualityComparer<T>.Default.Equals(typed, True);
        }

        public virtual object Convert(IList<object> values, Type targetType, object parameter, CultureInfo culture) {
            return values.Count > 0 && values[0] is true ? True : False;
        }
    }

    /// <summary>Turns true into false, and false into true.</summary>
    public sealed class BooleanInvertConverter : BooleanConverter<bool> {
        public BooleanInvertConverter() : base(false, true) { }
    }

    /// <summary>
    /// Kept for the views that name it. Avalonia has no Visibility type: bind
    /// <c>IsVisible</c>, which is a boolean, so this converter passes the value on.
    /// </summary>
    public sealed class BooleanToVisibilityConverter : BooleanConverter<bool> {
        public BooleanToVisibilityConverter() : base(true, false) { }
    }

    /// <summary>True only when every value is true.</summary>
    public class BooleanAndConverter : IMultiValueConverter {
        public object Convert(IList<object> values, Type targetType, object parameter, CultureInfo culture) {
            foreach (var value in values) {
                if (value is false) return false;
            }
            return true;
        }
    }

    /// <summary>True when one value or more is true.</summary>
    public class BooleanOrConverter : IMultiValueConverter {
        public object Convert(IList<object> values, Type targetType, object parameter, CultureInfo culture) {
            foreach (var value in values) {
                if (value is true) return true;
            }
            return false;
        }
    }

    /// <summary>
    /// The same test as <see cref="BooleanAndConverter"/>. The WPF name says "Or", but
    /// the WPF body returns Collapsed as soon as one value is false. The behaviour is
    /// kept, because the views depend on it.
    /// </summary>
    public class BooleanOrToVisibilityConverter : IMultiValueConverter {
        public object Convert(IList<object> values, Type targetType, object parameter, CultureInfo culture) {
            foreach (var value in values) {
                if (value is false) return false;
            }
            return true;
        }
    }

    /// <summary>Gives the value back, unchanged.</summary>
    public class IdentityConverter : IValueConverter, IMultiValueConverter {
        public virtual object Convert(object value, Type targetType, object parameter, CultureInfo culture) => value;

        public virtual object ConvertBack(object value, Type targetType, object parameter, CultureInfo culture) => value;

        public object Convert(IList<object> values, Type targetType, object parameter, CultureInfo culture) =>
            values.Count > 0 ? values[0] : null;
    }

    /// <summary>True when the value equals the parameter.</summary>
    public class EnumToBoolConverter : IValueConverter {
        public object Convert(object value, Type targetType, object parameter, CultureInfo culture) {
            return value != null && value.Equals(parameter);
        }

        public object ConvertBack(object value, Type targetType, object parameter, CultureInfo culture) {
            return value is true ? parameter : BindingOperations.DoNothing;
        }
    }

    /// <summary>
    /// Kept for the views that name it. It gives a boolean now, for <c>IsVisible</c>.
    /// </summary>
    public class EnumToVisibilityConverter : IValueConverter {
        public object Convert(object value, Type targetType, object parameter, CultureInfo culture) {
            return value != null && value.Equals(parameter);
        }

        public object ConvertBack(object value, Type targetType, object parameter, CultureInfo culture) {
            return value is true ? parameter : BindingOperations.DoNothing;
        }
    }

    /// <summary>True when the flag in the parameter is set on the value.</summary>
    public class FlagToBoolConverter : IValueConverter {
        public object Convert(object value, Type targetType, object parameter, CultureInfo culture) {
            return value is Enum flags && parameter is Enum flag && flags.HasFlag(flag);
        }

        public object ConvertBack(object value, Type targetType, object parameter, CultureInfo culture) {
            return value is true && parameter != null ? parameter : BindingOperations.DoNothing;
        }
    }

    /// <summary>
    /// Runs a chain of converters over each value, then joins the results with the last
    /// converter of the chain.
    /// </summary>
    public class MultiValueConverterGroup : List<IMultiValueConverter>, IMultiValueConverter {
        public object Convert(IList<object> values, Type targetType, object parameter, CultureInfo culture) {
            if (values.Count == 0) return null;
            if ((Count - 1) % values.Count != 0) {
                throw new ArgumentException(
                    "Could not interpret how to apply converters to values (make sure each value " +
                    "goes through the same number of converters, and there's one left at the end " +
                    "to combine them!)");
            }

            int conversionsPerValue = (Count - 1) / values.Count;
            var results = new object[values.Count];
            for (int i = 0; i < values.Count; i++) {
                results[i] = values[i];
                for (int j = 0; j < conversionsPerValue; j++) {
                    results[i] = this[j + i * conversionsPerValue]
                        .Convert(new[] { results[i] }, targetType, parameter, culture);
                }
            }

            return this[Count - 1].Convert(results, targetType, parameter, culture);
        }
    }
}
