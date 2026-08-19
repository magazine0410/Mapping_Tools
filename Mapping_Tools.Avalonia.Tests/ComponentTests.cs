using System;
using Avalonia.Controls;
using Mapping_Tools.Components;
using Mapping_Tools.Components.Dialogs;
using Microsoft.VisualStudio.TestTools.UnitTesting;

namespace Mapping_Tools.Avalonia.Tests {
    /// <summary>
    /// The parts that the WPF views need, and that neither Fluent nor Avalonia supplies.
    /// </summary>
    [TestClass]
    public class ComponentTests {
        [TestMethod]
        public void ThePopupBoxOpensItsPanel() {
            var box = new PopupBox {
                ToggleContent = new TextBlock { Text = "?" },
                Content = new TextBlock { Text = "The panel body." }
            };
            var window = HeadlessApp.Show(box);

            box.IsPopupOpen = true;
            HeadlessApp.Draw(window);
            Assert.IsTrue(box.IsPopupOpen);

            box.ClosePopup();
            Assert.IsFalse(box.IsPopupOpen);
        }

        [TestMethod]
        public void TheHeaderShowsTheNameAndTheDescriptionOfTheTool() {
            var header = new ViewHeaderComponent();
            var window = HeadlessApp.Show(header);

            header.ParentControl = new FakeTool();
            HeadlessApp.Draw(window);

            Assert.AreEqual("Map Cleaner", header.FindControl<TextBlock>("HeaderTextBlock")!.Text);
            Assert.AreEqual(FakeTool.ToolDescription,
                header.FindControl<TextBlock>("DescriptionTextBlock")!.Text);
        }

        [TestMethod]
        public void TheHeaderHidesItselfForAToolThatWantsNoTitle() {
            var header = new ViewHeaderComponent();
            HeadlessApp.Show(header);

            header.ParentControl = new QuietTool();

            Assert.IsFalse(header.FindControl<StackPanel>("MainPanel")!.IsVisible);
        }

        [TestMethod]
        public void TheDialogHostGivesBackTheAnswerOfTheButton() {
            var host = new DialogHost { Content = new TextBlock { Text = "Behind the dialog." } };
            var window = HeadlessApp.Show(host);

            var answer = host.ShowDialog(new MessageDialog("Something went wrong."));
            HeadlessApp.Draw(window);
            Assert.IsTrue(host.IsOpen);

            DialogHost.CloseDialogCommand.Execute(true);

            Assert.IsTrue(answer.IsCompleted, "The close command left the caller waiting.");
            Assert.AreEqual(true, answer.Result);
            Assert.IsFalse(host.IsOpen);
        }

        [TestMethod]
        public void TheTypeValueDialogStartsWithTheValueItWasGiven() {
            var host = new DialogHost();
            var window = HeadlessApp.Show(host);

            var dialog = new TypeValueDialog(12.5);
            var answer = host.ShowDialog(dialog);
            HeadlessApp.Draw(window);

            Assert.AreEqual("12.5", dialog.FindControl<TextBox>("ValueBox")!.Text);

            DialogHost.CloseDialogCommand.Execute(false);
            Assert.AreEqual(false, answer.Result);
        }

        [TestMethod]
        public void TheBeatmapImportDialogDrawsAndKeepsItsPath() {
            var host = new DialogHost();
            var window = HeadlessApp.Show(host);

            var dialog = new BeatmapImportDialog { Path = "/home/me/maps/song.osu" };
            var answer = host.ShowDialog(dialog);
            HeadlessApp.Draw(window);

            DialogHost.CloseDialogCommand.Execute(true);
            Assert.AreEqual(true, answer.Result);
            Assert.AreEqual("/home/me/maps/song.osu", dialog.Path);
        }

        private class FakeTool : UserControl {
            public static readonly string ToolName = "Map Cleaner";
            public static readonly string ToolDescription =
                "Reads a beatmap, and writes it back the way the editor would.";
        }

        [Mapping_Tools.Avalonia.Views.DontShowTitle]
        private class QuietTool : UserControl {
            public static readonly string ToolName = "Get started";
            public static readonly string ToolDescription = "";
        }
    }
}
