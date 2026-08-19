using System;
using System.Collections.Generic;
using System.Globalization;
using Avalonia;
using Avalonia.Controls;
using Avalonia.Data;
using Avalonia.Data.Converters;
using Avalonia.Input;
using Avalonia.Metadata;
using Mapping_Tools.Components.Domain;

namespace Mapping_Tools.Components {
    /// <summary>
    /// A text box that tests what the user typed before it writes it back.
    /// </summary>
    /// <remarks>
    /// This takes the place of the WPF pair "Binding.Converter plus
    /// Binding.ValidationRules". Bind <see cref="Value"/>, not <c>Text</c>:
    /// <code>
    /// &lt;c:ValidatedTextBox Value="{Binding BeatsPerMinute}"
    ///                     Converter="{StaticResource DoubleToStringConverter}"
    ///                     PlaceholderText="BPM"&gt;
    ///   &lt;domain:IsGreaterRule Value="0"/&gt;
    /// &lt;/c:ValidatedTextBox&gt;
    /// </code>
    /// <para>
    /// A plain converter cannot do this job in Avalonia. A converter that throws out of
    /// <c>ConvertBack</c> leaves the source at zero and shows nothing, and a converter
    /// that gives back a <see cref="BindingNotification"/> shows a cast error instead of
    /// the message. Both were measured against Avalonia 12.1.1. So the control owns the
    /// write, and marks itself through <see cref="DataValidationErrors"/>.
    /// </para>
    /// </remarks>
    public class ValidatedTextBox : TextBox {
        /// <summary>What the source holds. This is the property to bind.</summary>
        public static readonly StyledProperty<object> ValueProperty =
            AvaloniaProperty.Register<ValidatedTextBox, object>(
                nameof(Value), defaultBindingMode: BindingMode.TwoWay);

        /// <summary>Turns the value into text, and the text back into a value.</summary>
        public static readonly StyledProperty<IValueConverter> ConverterProperty =
            AvaloniaProperty.Register<ValidatedTextBox, IValueConverter>(nameof(Converter));

        /// <summary>Handed to the converter, the way a binding hands over its parameter.</summary>
        public static readonly StyledProperty<object> ConverterParameterProperty =
            AvaloniaProperty.Register<ValidatedTextBox, object>(nameof(ConverterParameter));

        /// <summary>
        /// True writes back when the box loses the focus, which is what the WPF views
        /// ask for. False writes back at every keypress.
        /// </summary>
        public static readonly StyledProperty<bool> UpdateOnLostFocusProperty =
            AvaloniaProperty.Register<ValidatedTextBox, bool>(nameof(UpdateOnLostFocus), true);

        /// <summary>The tests, in the order they run. This is the XAML content.</summary>
        [Content]
        public List<ValidationRule> Rules { get; } = new();

        public object Value {
            get => GetValue(ValueProperty);
            set => SetValue(ValueProperty, value);
        }

        public IValueConverter Converter {
            get => GetValue(ConverterProperty);
            set => SetValue(ConverterProperty, value);
        }

        public object ConverterParameter {
            get => GetValue(ConverterParameterProperty);
            set => SetValue(ConverterParameterProperty, value);
        }

        public bool UpdateOnLostFocus {
            get => GetValue(UpdateOnLostFocusProperty);
            set => SetValue(UpdateOnLostFocusProperty, value);
        }

        protected override Type StyleKeyOverride => typeof(TextBox);

        /// <summary>True while this control is the one writing, so it does not loop.</summary>
        private bool writing;

        protected override void OnPropertyChanged(AvaloniaPropertyChangedEventArgs change) {
            base.OnPropertyChanged(change);

            if (change.Property == ValueProperty) {
                if (writing) return;
                ShowValue();
                return;
            }

            if (change.Property == TextProperty && !UpdateOnLostFocus) {
                Commit();
            }
        }

        protected override void OnLostFocus(FocusChangedEventArgs e) {
            base.OnLostFocus(e);
            if (UpdateOnLostFocus) Commit();
        }

        /// <summary>Puts the value of the source into the box.</summary>
        private void ShowValue() {
            var culture = CultureInfo.CurrentCulture;
            var text = Converter is null
                ? Value?.ToString()
                : Converter.Convert(Value, typeof(string), ConverterParameter, culture)?.ToString();

            writing = true;
            Text = text ?? string.Empty;
            writing = false;

            DataValidationErrors.ClearErrors(this);
        }

        /// <summary>
        /// Runs the rules over the text. It writes to the source only when they all
        /// pass, so a bad value never reaches the tool.
        /// </summary>
        public void Commit() {
            if (writing) return;

            var culture = CultureInfo.CurrentCulture;

            foreach (var rule in Rules) {
                var complaint = rule.Validate(Text, culture);
                if (complaint is null) continue;

                DataValidationErrors.SetError(this, new ValidationMessage(complaint));
                return;
            }

            object converted;
            try {
                converted = Converter is null
                    ? Text
                    : Converter.ConvertBack(Text, typeof(object), ConverterParameter, culture);
            } catch (Exception e) {
                DataValidationErrors.SetError(this, e);
                return;
            }

            // The converters report a bad value this way, rather than by throwing.
            if (converted is BindingNotification notification) {
                if (notification.Error is not null) {
                    DataValidationErrors.SetError(this, notification.Error);
                    return;
                }
                converted = notification.Value;
            }

            if (ReferenceEquals(converted, BindingOperations.DoNothing)) return;

            DataValidationErrors.ClearErrors(this);

            writing = true;
            Value = converted;
            writing = false;
        }
    }
}
