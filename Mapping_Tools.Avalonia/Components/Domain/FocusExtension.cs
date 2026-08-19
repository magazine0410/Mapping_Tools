using Avalonia;
using Avalonia.Input;

namespace Mapping_Tools.Components.Domain {
    /// <summary>
    /// Lets a view model ask for the keyboard focus, through a bound property.
    /// </summary>
    public static class FocusExtension {
        public static readonly AttachedProperty<bool> IsFocusedProperty =
            AvaloniaProperty.RegisterAttached<InputElement, bool>(
                "IsFocused", typeof(FocusExtension));

        static FocusExtension() {
            IsFocusedProperty.Changed.AddClassHandler<InputElement>((element, args) => {
                // Only true means anything. False is not "give the focus away".
                if (args.NewValue is true) element.Focus();
            });
        }

        public static bool GetIsFocused(InputElement element) => element.GetValue(IsFocusedProperty);

        public static void SetIsFocused(InputElement element, bool value) =>
            element.SetValue(IsFocusedProperty, value);
    }
}
