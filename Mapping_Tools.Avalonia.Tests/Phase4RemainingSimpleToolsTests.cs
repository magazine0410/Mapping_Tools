using System;
using System.Drawing;
using System.IO;
using System.Linq;
using Avalonia.VisualTree;
using Mapping_Tools.Avalonia.Platform;
using Mapping_Tools.Avalonia.Views;
using Mapping_Tools.Avalonia.Views.AutoFailDetector;
using Mapping_Tools.Avalonia.Views.ComboColourStudio;
using Mapping_Tools.Avalonia.Views.MapsetMerger;
using Mapping_Tools.Avalonia.Views.Preferences;
using Mapping_Tools.Avalonia.Views.Standard;
using Mapping_Tools.Classes.BeatmapHelper;
using Mapping_Tools.Classes.Tools.ComboColourStudio;
using Mapping_Tools.Viewmodels;
using Microsoft.VisualStudio.TestTools.UnitTesting;

namespace Mapping_Tools.Avalonia.Tests {
    [TestClass]
    public class Phase4RemainingSimpleToolsTests {
        private static string CopyResource(string name, string directory = null) {
            directory ??= Path.GetTempPath();
            Directory.CreateDirectory(directory);
            var source = Path.Combine(AppContext.BaseDirectory, "Resources", name);
            var target = Path.Combine(directory,
                $"mt-{Guid.NewGuid():N}-{Path.GetFileName(source)}");
            File.Copy(source, target);
            return target;
        }

        [TestMethod]
        public void AutoFailDetectorDrawsAndAnalyzesARealMap() {
            var view = new AutoFailDetectorView();
            var window = HeadlessApp.Show(view);
            HeadlessApp.Draw(window);
            var path = CopyResource("ComplicatedTestMap.osu");
            try {
                var analysis = AutoFailDetectorView.Analyze(new AutoFailDetectorVm {
                    Paths = new[] { path },
                    ShowUnloadingObjects = true,
                    ShowPotentialUnloadingObjects = true,
                    ShowPotentialDisruptors = true
                }, null);

                Assert.IsFalse(string.IsNullOrWhiteSpace(analysis.Message));
                Assert.IsNotNull(analysis.Unloading);
            } finally {
                File.Delete(path);
            }
        }

        [TestMethod]
        public void MapsetMergerDrawsAndMergesARealMapset() {
            var view = new MapsetMergerView();
            var window = HeadlessApp.Show(view);
            HeadlessApp.Draw(window);
            string root = Path.Combine(Path.GetTempPath(), $"mt-{Guid.NewGuid():N}");
            string source = Path.Combine(root, "source");
            string output = Path.Combine(root, "output");
            CopyResource("EmptyTestMap.osu", source);
            try {
                var vm = new MapsetMergerVm { ExportPath = output };
                vm.Mapsets.Add(new MapsetMergerVm.MapsetItem {
                    Name = "Set A", Path = source
                });

                var message = global::Mapping_Tools.Classes.Tools.MapsetMerger.Merge(vm);

                StringAssert.Contains(message, "1 mapset");
                Assert.AreEqual(1, Directory.GetFiles(output, "*.osu").Length);
            } finally {
                if (Directory.Exists(root)) Directory.Delete(root, true);
            }
        }

        [TestMethod]
        public void ComboColourStudioDrawsAndExportsColours() {
            var view = new ComboColourStudioView();
            var window = HeadlessApp.Show(view);
            HeadlessApp.Draw(window);
            var path = CopyResource("ComplicatedTestMap.osu");
            try {
                var vm = new ComboColourStudioVm { ExportPath = path };
                var first = new SpecialColour(Color.CornflowerBlue, "Combo1");
                var second = new SpecialColour(Color.OrangeRed, "Combo2");
                vm.Project.ComboColours.Add(first);
                vm.Project.ComboColours.Add(second);
                vm.Project.ColourPoints.Add(new ColourPoint(0,
                    new[] { first, second }, ColourPointMode.Normal, vm.Project));

                var message = ComboColourStudioView.RunProgram(vm, null);

                StringAssert.Contains(message, "1 beatmap");
                var beatmap = new BeatmapEditor(path).Beatmap;
                Assert.AreEqual(2, beatmap.ComboColours.Count);
                Assert.AreEqual(Color.CornflowerBlue.ToArgb(),
                    beatmap.ComboColours[0].Color.ToArgb());
            } finally {
                File.Delete(path);
            }
        }

        [TestMethod]
        public void GetStartedDrawsAndParsesForkReleases() {
            var view = new StandardView(false);
            var window = HeadlessApp.Show(view);
            HeadlessApp.Draw(window);

            var releases = StandardView.ParseReleases("""
                [{"tag_name":"v1.2.3","name":"Linux preview","body":"Notes",
                  "published_at":"2026-08-19T12:00:00Z","html_url":"https://example.test/release"}]
                """);

            Assert.AreEqual(1, releases.Count);
            Assert.AreEqual("Linux preview", releases[0].Title);
            CollectionAssert.Contains(ViewCollection.GetAllToolTypes(), typeof(StandardView));
        }

        [TestMethod]
        public void PreferencesDrawsAndPersistsHostSettings() {
            var settings = new JsonCoreSettings { MaxBackupFiles = 10 };
            int saves = 0;
            var model = new PreferencesModel(settings, () => saves++);
            model.MaxBackupFiles = 25;
            model.SongsPath = "/tmp/Songs";

            var view = new PreferencesView(settings);
            var window = HeadlessApp.Show(view);
            HeadlessApp.Draw(window);

            Assert.AreEqual(25, settings.MaxBackupFiles);
            Assert.AreEqual("/tmp/Songs", settings.SongsPath);
            Assert.AreEqual(2, saves);
            Assert.IsTrue(view.GetVisualDescendants().Any());
        }
    }
}
