using System.Globalization;
using System.Text;
using System.Text.RegularExpressions;
using Mapping_Tools.Classes.SystemTools;

namespace Mapping_Tools.Components.Domain {
    /// <summary>
    /// One test on what the user typed.
    /// </summary>
    /// <remarks>
    /// Avalonia has no <c>Binding.ValidationRules</c>. A rule is put to work by
    /// <see cref="Mapping_Tools.Components.ValidatedTextBox"/>, which holds the write
    /// and shows the reason when a rule says no.
    /// </remarks>
    public abstract class ValidationRule {
        /// <summary>Gives null when the value is good, or the reason it is not.</summary>
        public abstract string Validate(object value, CultureInfo culture);
    }

    /// <summary>The field must hold something.</summary>
    public class NotEmptyRule : ValidationRule {
        public override string Validate(object value, CultureInfo culture) {
            return string.IsNullOrWhiteSpace(value?.ToString()) ? "Field is required." : null;
        }
    }

    /// <summary>The field must be no longer than <see cref="Limit"/> characters.</summary>
    public class CharacterLimitRule : ValidationRule {
        public int Limit { get; set; } = int.MaxValue;

        public override string Validate(object value, CultureInfo culture) {
            var str = value?.ToString() ?? string.Empty;
            return str.Length <= Limit ? null : $"Field can not be over {Limit} characters long.";
        }
    }

    /// <summary>The field must hold ASCII only.</summary>
    public class IsAsciiRule : ValidationRule {
        public override string Validate(object value, CultureInfo culture) {
            var str = value?.ToString() ?? string.Empty;
            return Encoding.UTF8.GetByteCount(str) == str.Length ? null : "Field is not ASCII.";
        }
    }

    /// <summary>The field must hold a comma separated list of numbers, or nothing.</summary>
    public class ParsableDoubleListRule : ValidationRule {
        private static readonly Regex Pattern =
            new(@"^([0-9]+(\.[0-9]+)?(,[0-9]+(\.[0-9]+)?)*)?$", RegexOptions.Compiled);

        public override string Validate(object value, CultureInfo culture) {
            return Pattern.IsMatch(value?.ToString() ?? string.Empty) ? null : "Field cannot be parsed.";
        }
    }

    /// <summary>The shared body of the four rules that compare against a number.</summary>
    public abstract class NumberComparisonRule : ValidationRule {
        /// <summary>The number to compare against.</summary>
        public double Value { get; set; }

        public override string Validate(object value, CultureInfo culture) {
            if (!TypeConverters.TryParseDouble(value?.ToString(), out double parsed)) {
                return "Double format error.";
            }
            return Compare(parsed, Value) ? null : Complaint(Value);
        }

        /// <summary>True when the typed number is allowed.</summary>
        protected abstract bool Compare(double typed, double limit);

        /// <summary>What to tell the user when it is not.</summary>
        protected abstract string Complaint(double limit);
    }

    /// <summary>The number must be greater than <see cref="NumberComparisonRule.Value"/>.</summary>
    public class IsGreaterRule : NumberComparisonRule {
        protected override bool Compare(double typed, double limit) => typed > limit;
        protected override string Complaint(double limit) => $"Value must be greater than {limit}.";
    }

    /// <summary>The number must be at least <see cref="NumberComparisonRule.Value"/>.</summary>
    public class IsGreaterOrEqualRule : NumberComparisonRule {
        protected override bool Compare(double typed, double limit) => typed >= limit;
        protected override string Complaint(double limit) => $"Value can not be less than {limit}.";
    }

    /// <summary>The number must be less than <see cref="NumberComparisonRule.Value"/>.</summary>
    public class IsLessRule : NumberComparisonRule {
        protected override bool Compare(double typed, double limit) => typed < limit;
        protected override string Complaint(double limit) => $"Value must be less than {limit}.";
    }

    /// <summary>The number must be at most <see cref="NumberComparisonRule.Value"/>.</summary>
    public class IsLessOrEqualRule : NumberComparisonRule {
        protected override bool Compare(double typed, double limit) => typed <= limit;
        protected override string Complaint(double limit) => $"Value can not be greater than {limit}.";
    }
}
