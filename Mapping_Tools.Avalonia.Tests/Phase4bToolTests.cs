using System;
using System.IO;
using System.Linq;
using Mapping_Tools.Avalonia.Views.MetadataManager;
using Mapping_Tools.Avalonia.Views.SliderCompletionator;
using Mapping_Tools.Avalonia.Views.SliderMerger;
using Mapping_Tools.Avalonia.Views.TumourGenerator;
using Mapping_Tools.Classes.BeatmapHelper;
using Mapping_Tools.Classes.Tools.TumourGenerating.Options;
using Mapping_Tools.Viewmodels;
using Microsoft.VisualStudio.TestTools.UnitTesting;

namespace Mapping_Tools.Avalonia.Tests {
    [TestClass]
    public class Phase4bToolTests {
        private static string CopyMap() {
            string source = Path.Combine(AppContext.BaseDirectory, "Resources",
                "ComplicatedTestMap.osu");
            string target = Path.Combine(Path.GetTempPath(),
                $"mt-{Guid.NewGuid():N}.osu");
            File.Copy(source, target);
            return target;
        }

        [TestMethod]
        public void SliderMergerDrawsAndMergesRealObjects() {
            var view = new SliderMergerView();
            var window = HeadlessApp.Show(view);
            HeadlessApp.Draw(window);
            string path = CopyMap();
            try {
                int before = new BeatmapEditor(path).Beatmap.HitObjects.Count;
                var vm = new SliderMergerVm {
                    Paths = new[] { path },
                    ImportModeSetting = SliderMergerVm.ImportMode.Everything,
                    Leniency = 10000,
                    MergeOnSliderEnd = false
                };

                var message = global::Mapping_Tools.Classes.Tools.SliderMerger.Merge(vm);

                int after = new BeatmapEditor(path).Beatmap.HitObjects.Count;
                StringAssert.Contains(message, "merged");
                Assert.IsTrue(after < before, $"Expected fewer than {before}, got {after}.");
            } finally {
                File.Delete(path);
            }
        }

        [TestMethod]
        public void SliderCompletionatorDrawsAndChangesRealSliders() {
            var view = new SliderCompletionatorView();
            var window = HeadlessApp.Show(view);
            HeadlessApp.Draw(window);
            string path = CopyMap();
            try {
                double before = new BeatmapEditor(path).Beatmap.HitObjects
                    .First(o => o.IsSlider).PixelLength;
                var vm = new SliderCompletionatorVm {
                    Paths = new[] { path },
                    ImportModeSetting = SliderCompletionatorVm.ImportMode.Everything,
                    FreeVariableSetting = SliderCompletionatorVm.FreeVariable.Velocity,
                    Length = 2,
                    Duration = -1,
                    SliderVelocity = -1
                };

                var message = global::Mapping_Tools.Classes.Tools.SliderCompletionator
                    .Complete(vm);

                double after = new BeatmapEditor(path).Beatmap.HitObjects
                    .First(o => o.IsSlider).PixelLength;
                StringAssert.Contains(message, "completed");
                Assert.AreNotEqual(before, after);
            } finally {
                File.Delete(path);
            }
        }

        [TestMethod]
        public void MetadataManagerDrawsAndExportsRealMetadata() {
            var view = new MetadataManagerView();
            var window = HeadlessApp.Show(view);
            HeadlessApp.Draw(window);
            string path = CopyMap();
            string directory = Path.GetDirectoryName(path)!;
            string marker = $"Test Artist {Guid.NewGuid():N}";
            try {
                var vm = new MetadataManagerVm {
                    ExportPath = path,
                    Artist = marker,
                    RomanisedArtist = marker,
                    Title = "Port Test",
                    RomanisedTitle = "Port Test",
                    BeatmapCreator = "Mapping Tools",
                    Tags = "linux port linux",
                    PreviewTime = 1234,
                    ResetIds = true
                };

                var message = global::Mapping_Tools.Classes.Tools.MetadataManager.Apply(vm);

                string result = Directory.GetFiles(directory, "*.osu")
                    .Single(file => new BeatmapEditor(file).Beatmap.Metadata["Artist"].Value == marker);
                var beatmap = new BeatmapEditor(result).Beatmap;
                StringAssert.Contains(message, "1 beatmap");
                Assert.AreEqual("linux port", beatmap.Metadata["Tags"].Value);
                Assert.AreEqual("0", beatmap.Metadata["BeatmapID"].Value);
                if (result != path) File.Delete(result);
            } finally {
                if (File.Exists(path)) File.Delete(path);
            }
        }

        [TestMethod]
        public void TumourGeneratorDrawsPreviewAndChangesRealSliders() {
            var view = new TumourGeneratorView();
            var window = HeadlessApp.Show(view);
            HeadlessApp.Draw(window);
            string path = CopyMap();
            try {
                int before = new BeatmapEditor(path).Beatmap.HitObjects
                    .Where(o => o.IsSlider).Sum(o => o.CurvePoints.Count);
                var vm = new TumourGeneratorVm {
                    Paths = new[] { path },
                    ImportModeSetting = TumourGeneratorVm.ImportMode.Everything,
                    FixSv = true
                };
                vm.TumourLayers.Add(TumourLayer.GetDefaultLayer());

                var message = global::Mapping_Tools.Classes.Tools.TumourGeneratorRunner
                    .Generate(vm);

                int after = new BeatmapEditor(path).Beatmap.HitObjects
                    .Where(o => o.IsSlider).Sum(o => o.CurvePoints.Count);
                StringAssert.Contains(message, "generated tumours");
                Assert.AreNotEqual(before, after);
            } finally {
                File.Delete(path);
            }
        }

        [TestMethod]
        public void PortableVisibilityPropertiesTrackTheirModes() {
            var merger = new SliderMergerVm();
            merger.ImportModeSetting = SliderMergerVm.ImportMode.Time;
            Assert.IsTrue(merger.IsTimeCodeBoxVisible);

            var completionator = new SliderCompletionatorVm {
                FreeVariableSetting = SliderCompletionatorVm.FreeVariable.Velocity,
                UseEndTime = true
            };
            Assert.IsTrue(completionator.IsEndTimeBoxVisible);

            var metadata = new MetadataManagerVm {
                RomanisedArtist = new string('a', 250),
                RomanisedTitle = "title",
                BeatmapCreator = "creator"
            };
            Assert.IsTrue(metadata.HasBeatmapFileNameOverflowError);
        }
    }
}
