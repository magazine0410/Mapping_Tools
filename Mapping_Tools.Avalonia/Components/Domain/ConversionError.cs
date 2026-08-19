using Avalonia.Data;

namespace Mapping_Tools.Components.Domain {
    /// <summary>
    /// How a converter says that the text it was given is not usable.
    /// </summary>
    /// <remarks>
    /// The WPF converters returned a <c>ValidationResult</c> from <c>ConvertBack</c>.
    /// WPF does not act on that: it writes the result object into the source.
    /// <para>
    /// Here the answer is a <see cref="BindingNotification"/> that carries the reason.
    /// <see cref="Mapping_Tools.Components.ValidatedTextBox"/> reads it, holds the
    /// write, and shows the message. A plain Avalonia binding cannot: it tries to cast
    /// the notification to the type of the source and reports that cast instead.
    /// </para>
    /// </remarks>
    internal static class ConversionError {
        /// <summary>Holds the write, and shows <paramref name="message"/> on the control.</summary>
        public static object Fail(string message) =>
            new BindingNotification(new ValidationMessage(message), BindingErrorType.DataValidationError);
    }
}
