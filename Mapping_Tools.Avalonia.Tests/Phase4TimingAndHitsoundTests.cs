using System;
using System.IO;
using Avalonia.Controls;
using Mapping_Tools.Avalonia.Views.HitsoundCopier;
using Mapping_Tools.Avalonia.Views.RhythmGuide;
using Mapping_Tools.Avalonia.Views.TimingCopier;
using Mapping_Tools.Avalonia.Views.TimingHelper;
using Mapping_Tools.Classes.BeatmapHelper;
using Mapping_Tools.Classes.SystemTools.Platform;
using Mapping_Tools.Classes.Tools;
using Mapping_Tools.Viewmodels;
using Microsoft.VisualStudio.TestTools.UnitTesting;

namespace Mapping_Tools.Avalonia.Tests {
    [TestClass]
    public class Phase4TimingAndHitsoundTests {
        private static string CopyResource(string name) {
            var source = Path.Combine(AppContext.BaseDirectory, "Resources", name);
            var target = Path.Combine(Path.GetTempPath(),
                $"mt-{Guid.NewGuid():N}-{Path.GetFileName(source)}");
            File.Copy(source, target);
            return target;
        }

        [TestMethod]
        public void TimingHelperDrawsAndProcessesARealMap() {
            var view = new TimingHelperView();
            var window = HeadlessApp.Show(view);
            HeadlessApp.Draw(window);

            var path = CopyResource("ComplicatedTestMap.osu");
            try {
                var before = File.ReadAllText(path);
                var vm = new TimingHelperVm { Paths = new[] { path } };
                var message = TimingHelperView.RunProgram(vm, null);

                Assert.IsTrue(message.StartsWith("Successfully added "));
                Assert.AreNotEqual(before, File.ReadAllText(path));
                Assert.IsTrue(new BeatmapEditor(path).Beatmap.BeatmapTiming.Redlines.Count > 0);
            } finally {
                File.Delete(path);
            }
        }

        [TestMethod]
        public void TimingCopierDrawsAndCopiesTiming() {
            var view = new TimingCopierView();
            var window = HeadlessApp.Show(view);
            HeadlessApp.Draw(window);

            var source = CopyResource("ComplicatedTestMap.osu");
            var target = CopyResource("EmptyTestMap.osu");
            try {
                var vm = new TimingCopierVm {
                    ImportPath = source,
                    ExportPath = target,
                    ResnapMode = "Don't move objects"
                };
                var message = TimingCopierView.RunProgram(vm, null);

                var expected = new BeatmapEditor(source).Beatmap.BeatmapTiming.Redlines.Count;
                var actual = new BeatmapEditor(target).Beatmap.BeatmapTiming.Redlines.Count;
                Assert.AreEqual(expected, actual);
                StringAssert.Contains(message, "1 beatmap");
            } finally {
                File.Delete(source);
                File.Delete(target);
            }
        }

        [TestMethod]
        public void RhythmGuideDrawsAndGeneratesANewMap() {
            var view = new RhythmGuideView();
            var window = HeadlessApp.Show(view);
            HeadlessApp.Draw(window);

            var source = CopyResource("ComplicatedTestMap.osu");
            var target = Path.Combine(Path.GetTempPath(), $"mt-{Guid.NewGuid():N}.osu");
            var previousShell = CorePlatform.Shell;
            CorePlatform.Shell = new NullShellService();
            try {
                var args = new global::Mapping_Tools.Classes.Tools.RhythmGuide.RhythmGuideGeneratorArgs {
                    Paths = new[] { source },
                    ExportPath = target,
                    ExportMode = global::Mapping_Tools.Classes.Tools.RhythmGuide.ExportMode.NewMap,
                    SelectionMode = global::Mapping_Tools.Classes.Tools.RhythmGuide.SelectionMode.AllEvents
                };
                RhythmGuideView.RunProgram(args, null);

                Assert.IsTrue(File.Exists(target));
                Assert.IsTrue(new BeatmapEditor(target).Beatmap.HitObjects.Count > 0);
            } finally {
                CorePlatform.Shell = previousShell;
                File.Delete(source);
                if (File.Exists(target)) File.Delete(target);
            }
        }

        [TestMethod]
        public void HitsoundCopierDrawsAndCopiesHitSounds() {
            var view = new HitsoundCopierView();
            var window = HeadlessApp.Show(view);
            HeadlessApp.Draw(window);

            var source = CopyResource("ComplicatedTestMap.osu");
            var target = CopyResource("ComplicatedTestMap.osu");
            try {
                var targetEditor = new BeatmapEditor(target);
                targetEditor.Beatmap.HitObjects[0].Hitsounds = 0;
                targetEditor.SaveFile();
                var expected = new BeatmapEditor(source).Beatmap.HitObjects[0].Hitsounds;

                var message = global::Mapping_Tools.Classes.Tools.HitsoundCopier.Copy(
                    new HitsoundCopierVm { PathFrom = source, PathTo = target }, null);

                var actual = new BeatmapEditor(target).Beatmap.HitObjects[0].Hitsounds;
                Assert.AreEqual(expected, actual);
                Assert.AreEqual("Done!", message);
            } finally {
                File.Delete(source);
                File.Delete(target);
            }
        }
    }
}
