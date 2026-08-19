using System;
using System.Collections.Generic;
using System.Linq;
using System.Reflection;
using Avalonia.Controls;
using Avalonia.Interactivity;
using Avalonia.Markup.Xaml;
using Avalonia.Threading;
using Mapping_Tools.Avalonia.Platform;
using Mapping_Tools.Avalonia.Views;
using Mapping_Tools.Classes.SystemTools.Platform;

namespace Mapping_Tools.Avalonia {
    /// <summary>
    /// The shell. It finds the tools, lists them, and shows the one that is chosen.
    /// </summary>
    public partial class MainWindow : Window {
        /// <summary>One row of the tool list.</summary>
        public record ToolEntry(string Name, string Summary, Type Type) {
            public override string ToString() => Name;
        }

        private readonly ViewCollection views = new();
        private readonly List<ToolEntry> allTools = new();
        private MappingTool activeTool;
        private DispatcherTimer snackTimer;

        public MainWindow() {
            InitializeComponent();

            var version = Assembly.GetExecutingAssembly().GetName().Version;
            this.FindControl<TextBlock>("VersionText")!.Text = version is null ? string.Empty : $"v{version.ToString(3)}";

            // Controls with no dialog host of their own show their dialogs here.
            Components.DialogHost.Root = this.FindControl<Components.DialogHost>("RootDialogHost");

            LoadTools();
            AvaloniaNotificationService.Sink = ShowSnack;
            Closing += (_, _) => views.AutoSaveSettings();
        }

        private void InitializeComponent() => AvaloniaXamlLoader.Load(this);

        /// <summary>
        /// Finds every tool by reflection, the same way the WPF shell does.
        /// </summary>
        private void LoadTools() {
            foreach (var type in ViewCollection.GetAllToolTypes()
                         .OrderBy(ViewCollection.GetName, StringComparer.OrdinalIgnoreCase)) {
                var description = ViewCollection.GetDescription(type);
                allTools.Add(new ToolEntry(ViewCollection.GetName(type), FirstLine(description), type));
            }

            ApplyFilter(string.Empty);

            if (allTools.Count == 0) {
                ShowPlaceholder();
            }
        }

        private static string FirstLine(string text) {
            if (string.IsNullOrWhiteSpace(text)) return string.Empty;
            var line = text.Split('\n', '\r').FirstOrDefault(l => !string.IsNullOrWhiteSpace(l)) ?? string.Empty;
            return line.Length > 90 ? line[..90].TrimEnd() + "…" : line.Trim();
        }

        /// <summary>
        /// Shown while no tool view has been ported yet.
        /// </summary>
        private void ShowPlaceholder() {
            this.FindControl<ContentControl>("ToolHost")!.Content = new Border {
                Padding = new global::Avalonia.Thickness(48),
                Child = new StackPanel {
                    Spacing = 12,
                    VerticalAlignment = global::Avalonia.Layout.VerticalAlignment.Center,
                    HorizontalAlignment = global::Avalonia.Layout.HorizontalAlignment.Center,
                    Children = {
                        new TextBlock {
                            Text = "The shell runs. No tool views are ported yet.",
                            FontSize = 18
                        },
                        new TextBlock {
                            Text = "A class that extends MappingTool and declares the static fields " +
                                   "ToolName and ToolDescription appears in this list by itself.",
                            Opacity = 0.7, MaxWidth = 460, TextWrapping = global::Avalonia.Media.TextWrapping.Wrap
                        }
                    }
                }
            };
        }

        private void ApplyFilter(string filter) {
            var list = this.FindControl<ListBox>("ToolList")!;
            list.ItemsSource = string.IsNullOrWhiteSpace(filter)
                ? allTools
                : allTools.Where(t => t.Name.Contains(filter, StringComparison.OrdinalIgnoreCase)).ToList();
        }

        private void ToolFilterChanged(object sender, TextChangedEventArgs e) =>
            ApplyFilter(this.FindControl<TextBox>("ToolFilter")!.Text ?? string.Empty);

        private void ToolSelected(object sender, SelectionChangedEventArgs e) {
            if (e.AddedItems.Count == 0 || e.AddedItems[0] is not ToolEntry entry) return;
            if (views.GetView(entry.Type) is not MappingTool tool) return;

            activeTool?.Deactivate();
            activeTool = tool;
            this.FindControl<ContentControl>("ToolHost")!.Content = tool;
            this.FindControl<TextBlock>("CurrentToolText")!.Text = entry.Name;
            tool.Activate();
        }

        /// <summary>Shows a short message at the bottom, then hides it again.</summary>
        private void ShowSnack(string message) {
            var border = this.FindControl<Border>("SnackBorder")!;
            this.FindControl<TextBlock>("SnackText")!.Text = message;
            border.IsVisible = true;

            snackTimer?.Stop();
            snackTimer = new DispatcherTimer { Interval = TimeSpan.FromSeconds(3) };
            snackTimer.Tick += (_, _) => {
                border.IsVisible = false;
                snackTimer.Stop();
            };
            snackTimer.Start();
        }

        private void OpenBeatmap(object sender, RoutedEventArgs e) {
            var path = CorePlatform.FileDialogs.GetCurrentBeatmap();
            if (!string.IsNullOrEmpty(path)) {
                CorePlatform.Notifications.Notify($"Selected {System.IO.Path.GetFileName(path)}");
            }
        }

        private void OpenConfigFolder(object sender, RoutedEventArgs e) =>
            CorePlatform.Shell.OpenFolder(CorePlatform.Paths.AppDataPath);

        private void OpenBackupsFolder(object sender, RoutedEventArgs e) =>
            CorePlatform.Shell.OpenFolder(CorePlatform.Settings.BackupsPath);

        private void OpenAbout(object sender, RoutedEventArgs e) {
            var version = Assembly.GetExecutingAssembly().GetName().Version;
            CorePlatform.Dialogs.ShowMessage(
                $"Mapping Tools {version}\n\nMade by:\nOliBomby\n\n" +
                "This is the Avalonia host, for Linux. The editor reader does not work " +
                "here, so tools read the beatmap file that you select.",
                "About");
        }

        private void ExitApp(object sender, RoutedEventArgs e) => Close();
    }
}
