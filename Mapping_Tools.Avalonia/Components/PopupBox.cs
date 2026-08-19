using Avalonia;
using Avalonia.Controls;
using Avalonia.Controls.Metadata;
using Avalonia.Controls.Primitives;

namespace Mapping_Tools.Components {
    /// <summary>
    /// A small button that opens a panel under itself. The views use it for the help
    /// bubbles and the little tool menus.
    /// </summary>
    /// <remarks>
    /// This takes the place of <c>materialDesign:PopupBox</c>, which the WPF views use
    /// in 33 places. Neither Fluent nor Material.Avalonia has an equal control, so the
    /// port supplies one. The property names are the same as the WPF ones, so a ported
    /// view keeps its shape:
    /// <code>
    /// &lt;c:PopupBox&gt;
    ///   &lt;c:PopupBox.ToggleContent&gt;
    ///     &lt;icons:MaterialIcon Kind="HelpCircle"/&gt;
    ///   &lt;/c:PopupBox.ToggleContent&gt;
    ///   &lt;TextBlock Text="What this tool does."/&gt;
    /// &lt;/c:PopupBox&gt;
    /// </code>
    /// </remarks>
    [TemplatePart("PART_Toggle", typeof(ToggleButton))]
    [TemplatePart("PART_Popup", typeof(Popup))]
    public class PopupBox : ContentControl {
        /// <summary>The face of the button that opens the panel.</summary>
        public static readonly StyledProperty<object> ToggleContentProperty =
            AvaloniaProperty.Register<PopupBox, object>(nameof(ToggleContent));

        /// <summary>True while the panel is open.</summary>
        public static readonly StyledProperty<bool> IsPopupOpenProperty =
            AvaloniaProperty.Register<PopupBox, bool>(nameof(IsPopupOpen), defaultBindingMode: global::Avalonia.Data.BindingMode.TwoWay);

        /// <summary>
        /// True keeps the panel open until the button is pressed again. False closes it
        /// on a click anywhere else, which is what most of the views want.
        /// </summary>
        public static readonly StyledProperty<bool> StaysOpenProperty =
            AvaloniaProperty.Register<PopupBox, bool>(nameof(StaysOpen));

        /// <summary>Where the panel appears against the button.</summary>
        public static readonly StyledProperty<PlacementMode> PlacementProperty =
            AvaloniaProperty.Register<PopupBox, PlacementMode>(nameof(Placement), PlacementMode.Bottom);

        public object ToggleContent {
            get => GetValue(ToggleContentProperty);
            set => SetValue(ToggleContentProperty, value);
        }

        public bool IsPopupOpen {
            get => GetValue(IsPopupOpenProperty);
            set => SetValue(IsPopupOpenProperty, value);
        }

        public bool StaysOpen {
            get => GetValue(StaysOpenProperty);
            set => SetValue(StaysOpenProperty, value);
        }

        public PlacementMode Placement {
            get => GetValue(PlacementProperty);
            set => SetValue(PlacementProperty, value);
        }

        protected override System.Type StyleKeyOverride => typeof(PopupBox);

        /// <summary>Closes the panel.</summary>
        public void ClosePopup() => IsPopupOpen = false;
    }
}
