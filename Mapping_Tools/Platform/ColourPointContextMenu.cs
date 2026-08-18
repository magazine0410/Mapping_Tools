using System.Windows.Controls;
using Mapping_Tools.Classes.Tools.ComboColourStudio;
using Mapping_Tools.Classes.ToolHelpers;
using Mapping_Tools.Components.Domain;
using MaterialDesignThemes.Wpf;
using System.Windows.Media;

namespace Mapping_Tools.Platform {
    /// <summary>
    /// Builds the menu that adds a colour to a colour sequence.
    /// This menu was in <see cref="ColourPoint"/>, which is now in the portable core.
    /// </summary>
    public static class ColourPointContextMenu {
        public static void Register() {
            ColourPoint.ShowAddColourMenu = (colourPoint, sender) => {
                var cm = GetContextMenu(colourPoint);
                cm.PlacementTarget = sender as Button;
                cm.IsOpen = true;
            };
        }

        private static ContextMenu GetContextMenu(ColourPoint colourPoint) {
            var colourSource = colourPoint.ParentProject;
            var cm = new ContextMenu();

            if (colourSource.ComboColours.Count == 0) {
                cm.Items.Add(new MenuItem
                    {Header = "Add at least one combo colour before adding colours to this sequence."});
            } else {
                foreach (var comboColour in colourSource.ComboColours) {
                    cm.Items.Add(new MenuItem {
                        Header = comboColour.Name,
                        Icon = new PackIcon {
                            Kind = PackIconKind.Circle,
                            Foreground = new SolidColorBrush(comboColour.Color.ToMediaColor())
                        },
                        Command = new CommandImplementation(_ => {
                            colourPoint.ColourSequence.Add(comboColour);
                        }),
                        Tag = comboColour
                    });
                }
            }

            return cm;
        }
    }
}
