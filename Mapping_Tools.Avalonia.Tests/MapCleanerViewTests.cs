using System;
using System.IO;
using System.Linq;
using Avalonia.Controls;
using Avalonia.VisualTree;
using Mapping_Tools.Avalonia.Views;
using Mapping_Tools.Avalonia.Views.MapCleaner;
using Mapping_Tools.Classes.SystemTools.Platform;
using Microsoft.VisualStudio.TestTools.UnitTesting;

namespace Mapping_Tools.Avalonia.Tests {
    /// <summary>
    /// The first tool view. It proves the whole path: the shell finds it, it draws, and
    /// it cleans a real beatmap.
    /// </summary>
    [TestClass]
    public class MapCleanerViewTests {
        private static string CopyTestMap(string name) {
            var source = Path.Combine(AppContext.BaseDirectory, "Resources", name);
            var target = Path.Combine(Path.GetTempPath(),
                $"mt-{Guid.NewGuid():N}-{Path.GetFileName(source)}");
            File.Copy(source, target);
            return target;
        }

        [TestMethod]
        public void TheShellFindsMapCleaner() {
            var found = ViewCollection.GetAllToolTypes();

            CollectionAssert.Contains(found, typeof(CleanerView),
                "Reflection did not find the tool, so it never appears in the list.");
            Assert.AreEqual("Map Cleaner", ViewCollection.GetName(typeof(CleanerView)));
        }

        [TestMethod]
        public void TheViewDrawsWithItsHeaderAndItsSettings() {
            var view = new CleanerView();
            var window = HeadlessApp.Show(view);
            HeadlessApp.Draw(window);

            var checkBoxes = view.GetVisualDescendants().OfType<CheckBox>().ToList();
            Assert.AreEqual(10, checkBoxes.Count,
                "Map Cleaner has ten switches. One went missing in the port.");
            Assert.IsTrue(view.CanRun, "The tool should be ready to run.");
        }

        [TestMethod]
        public void TheBeatDivisorBoxShowsTheDefaultsAndRefusesRubbish() {
            var view = new CleanerView();
            HeadlessApp.Show(view);

            var box = view.FindControl<Components.ValidatedTextBox>("BeatDivisorsBox");
            Assert.IsNotNull(box);
            Assert.AreEqual("1/16, 1/12", box.Text, "The default beat divisors did not reach the box.");

            var before = view.ViewModel.MapCleanerArgs.BeatDivisors;
            // The box writes back when the focus leaves, the way the WPF view did.
            box.Text = "not a divisor";
            box.Commit();
            Assert.IsTrue(DataValidationErrors.GetHasErrors(box), "The box was not marked.");
            Assert.AreSame(before, view.ViewModel.MapCleanerArgs.BeatDivisors,
                "Rubbish reached the tool settings.");
        }

        [TestMethod]
        public void ItCleansARealBeatmap() {
            var path = CopyTestMap("ComplicatedTestMap.osu");
            try {
                CorePlatform.FileDialogs.SetCurrentBeatmaps(path);

                var view = new CleanerView();
                HeadlessApp.Show(view);

                var vm = view.ViewModel;
                vm.Paths = new[] { path };

                var before = File.ReadAllText(path);
                var message = CleanerView.RunProgram(vm, null);

                Assert.IsTrue(message.StartsWith("Successfully "), $"The tool said: {message}");
                Assert.AreNotEqual(before, File.ReadAllText(path),
                    "The beatmap on disk did not change.");
            } finally {
                File.Delete(path);
                CorePlatform.FileDialogs.SetCurrentBeatmaps();
            }
        }

        [TestMethod]
        public void ItSaysSoWhenNoBeatmapIsOpen() {
            CorePlatform.FileDialogs.SetCurrentBeatmaps();

            var view = new CleanerView();
            HeadlessApp.Show(view);

            var asked = new RecordingDialogService();
            var previous = CorePlatform.Dialogs;
            CorePlatform.Dialogs = asked;
            try {
                view.RunTool(CorePlatform.FileDialogs.GetCurrentBeatmaps());
            } finally {
                CorePlatform.Dialogs = previous;
            }

            Assert.AreEqual("No beatmap", asked.LastTitle);
            Assert.IsTrue(view.CanRun, "The tool locked itself with nothing to do.");
        }

        private class RecordingDialogService : IDialogService {
            public string LastTitle { get; private set; }

            public void ShowMessage(string message, string title = null) => LastTitle = title;
            public bool AskYesNo(string message, string title = null) => false;
            public bool? AskYesNoCancel(string message, string title = null) => null;
            public bool ShowOkCancel(string message, string title = null) => false;
        }
    }
}
