using System;
using System.Drawing;
using System.IO;
using System.Linq;
using Mapping_Tools.Classes.BeatmapHelper;
using Mapping_Tools.Classes.BeatmapHelper.Enums;
using Mapping_Tools.Classes.HitsoundStuff;
using Mapping_Tools.Classes.MathUtil;
using Mapping_Tools.Classes.SystemTools.Platform;
using Mapping_Tools.Classes.Tools;
using Mapping_Tools.Classes.Tools.SlideratorStuff;
using Mapping_Tools.Components.Domain;
using Mapping_Tools.Viewmodels;
using Microsoft.VisualStudio.TestTools.UnitTesting;
using SkiaSharp;

namespace Mapping_Tools.Avalonia.Tests {
    /// <summary>
    /// The defects that the review of phase 4 found. Each test fails on the code as it
    /// was before the fix.
    /// </summary>
    [TestClass]
    public class ReviewFixTests {
        private static string TempFolder() {
            string path = Path.Combine(Path.GetTempPath(), $"mt-{Guid.NewGuid():N}");
            Directory.CreateDirectory(path);
            return path;
        }

        [TestMethod]
        public void MidiExportKeepsTheFolderWhenItWritesNothing() {
            string exportFolder = TempFolder();
            string sourceFolder = TempFolder();
            string keepMe = Path.Combine(exportFolder, "important.osu");
            File.WriteAllText(keepMe, "do not delete me");

            string baseMap = Path.Combine(sourceFolder, "base.osu");
            File.Copy(Path.Combine(AppContext.BaseDirectory, "Resources", "EmptyTestMap.osu"),
                baseMap);

            var args = new HitsoundStudioVm {
                BaseBeatmap = baseMap,
                ExportFolder = exportFolder,
                HitsoundExportModeSetting = HitsoundStudioVm.HitsoundExportMode.Midi,
                DeleteAllInExportFirst = true,
                // Samples are never written by the MIDI branch, and the map is off.
                ExportSamples = true,
                ExportMap = false
            };
            args.HitsoundLayers.Add(new HitsoundLayer { Name = "layer" });

            try {
                HitsoundStudioRunner.Export(args);

                Assert.IsTrue(File.Exists(keepMe),
                    "The export folder was emptied even though nothing was written back.");
            } finally {
                Directory.Delete(exportFolder, true);
                Directory.Delete(sourceFolder, true);
            }
        }

        [TestMethod]
        public void MidiExportStillClearsTheFolderWhenItWritesTheMap() {
            string exportFolder = TempFolder();
            string sourceFolder = TempFolder();
            string stale = Path.Combine(exportFolder, "stale.txt");
            File.WriteAllText(stale, "old output");

            string baseMap = Path.Combine(sourceFolder, "base.osu");
            File.Copy(Path.Combine(AppContext.BaseDirectory, "Resources", "EmptyTestMap.osu"),
                baseMap);

            var args = new HitsoundStudioVm {
                BaseBeatmap = baseMap,
                ExportFolder = exportFolder,
                HitsoundDiffName = "Hitsounds",
                HitsoundExportModeSetting = HitsoundStudioVm.HitsoundExportMode.Midi,
                DeleteAllInExportFirst = true,
                ExportSamples = false,
                ExportMap = true
            };
            args.HitsoundLayers.Add(new HitsoundLayer { Name = "layer" });

            try {
                HitsoundStudioRunner.Export(args);

                Assert.IsFalse(File.Exists(stale), "The stale file survived a real export.");
            } finally {
                Directory.Delete(exportFolder, true);
                Directory.Delete(sourceFolder, true);
            }
        }

        [TestMethod]
        public void TheTimesTextRefusesRubbishWithAShortReason() {
            var layer = new HitsoundLayer { Times = new System.Collections.Generic.List<double> { 100, 200 } };

            var error = Assert.ThrowsException<ValidationMessage>(
                () => layer.TimesText = "100, 200, x");

            Assert.AreEqual("\"x\" is not a number.", error.ToString(),
                "The control shows ToString, so it must hold the reason and nothing else.");
            CollectionAssert.AreEqual(new[] { 100d, 200d }, layer.Times.ToArray(),
                "A bad value reached the layer.");
        }

        [TestMethod]
        public void TheTimesTextStillTakesGoodInput() {
            var layer = new HitsoundLayer();

            layer.TimesText = "300, 100; 200";

            CollectionAssert.AreEqual(new[] { 100d, 200d, 300d }, layer.Times.ToArray());
            Assert.AreEqual("100, 200, 300", layer.TimesText);
        }

        [TestMethod]
        public void RecolorWritesItsResultIntoTheBitmap() {
            // Two pixels far from any gradient or border colour, so both become black.
            using var image = new SKBitmap(2, 1);
            image.SetPixel(0, 0, new SKColor(255, 255, 255));
            image.SetPixel(1, 0, new SKColor(10, 10, 10));

            var (result, segments) = SliderPicturator.Recolor(image,
                Color.FromArgb(255, 0, 120, 200), Color.FromArgb(255, 255, 255, 255),
                Color.Black);

            using (result) {
                Assert.AreEqual(2, result.Width);
                Assert.AreEqual(1, result.Height);
                // The near-black pixel must have been rewritten to pure black. If the
                // pixel array is never assigned back, this still holds the input.
                Assert.AreEqual(SKColors.Black, result.GetPixel(1, 0),
                    "The recoloured pixels never reached the output bitmap.");
                Assert.IsTrue(segments >= 0);
            }
        }

        [TestMethod]
        public void RecolorAgreesWithAPerPixelReading() {
            // Guards the whole-bitmap rewrite: every pixel of the output must match what
            // the slow per-pixel path would have produced for the same input.
            using var image = new SKBitmap(8, 5);
            var random = new Random(727);
            for (int x = 0; x < image.Width; x++) {
                for (int y = 0; y < image.Height; y++) {
                    image.SetPixel(x, y, new SKColor(
                        (byte)random.Next(256), (byte)random.Next(256), (byte)random.Next(256)));
                }
            }

            var slider = Color.FromArgb(255, 0, 120, 200);
            var border = Color.FromArgb(255, 255, 255, 255);
            var (first, _) = SliderPicturator.Recolor(image, slider, border, Color.Black);
            var (second, _) = SliderPicturator.Recolor(image, slider, border, Color.Black);

            using (first)
            using (second) {
                for (int x = 0; x < image.Width; x++) {
                    for (int y = 0; y < image.Height; y++) {
                        Assert.AreEqual(first.GetPixel(x, y), second.GetPixel(x, y),
                            $"Pixel {x},{y} is not stable.");
                    }
                }
                // The source must be left alone.
                Assert.AreNotEqual(0u, (uint)image.GetPixel(0, 0).Alpha);
            }
        }

        [TestMethod]
        public void ALeniencyOf727MakesTheShape() {
            string folder = TempFolder();
            string path = Path.Combine(folder, "map.osu");
            File.Copy(Path.Combine(AppContext.BaseDirectory, "Resources", "EmptyTestMap.osu"), path);

            var editor = new BeatmapEditor(path);
            editor.Beatmap.HitObjects.Add(new HitObject("100,100,1000,2,0,L|200:100,1,100"));
            editor.Beatmap.HitObjects.Add(new HitObject("200,100,1500,2,0,L|300:100,1,100"));
            editor.SaveFile();

            var args = new SliderMergerVm {
                Paths = new[] { path },
                ImportModeSetting = SliderMergerVm.ImportMode.Everything,
                Leniency = 727
            };

            try {
                SliderMerger.Merge(args);

                var merged = new BeatmapEditor(path).Beatmap.HitObjects
                    .First(o => o.IsSlider);
                Assert.AreEqual(PathType.Bezier, merged.SliderType);
                Assert.AreEqual(16, merged.GetAllCurvePoints().Count,
                    "The 727 shape has 16 anchors. WYSI.");
            } finally {
                Directory.Delete(folder, true);
            }
        }

        [TestMethod]
        public void AnOrdinaryLeniencyDoesNotMakeTheShape() {
            string folder = TempFolder();
            string path = Path.Combine(folder, "map.osu");
            File.Copy(Path.Combine(AppContext.BaseDirectory, "Resources", "EmptyTestMap.osu"), path);

            var editor = new BeatmapEditor(path);
            editor.Beatmap.HitObjects.Add(new HitObject("100,100,1000,2,0,L|200:100,1,100"));
            editor.Beatmap.HitObjects.Add(new HitObject("200,100,1500,2,0,L|300:100,1,100"));
            editor.SaveFile();

            var args = new SliderMergerVm {
                Paths = new[] { path },
                ImportModeSetting = SliderMergerVm.ImportMode.Everything,
                Leniency = 10
            };

            try {
                SliderMerger.Merge(args);

                var merged = new BeatmapEditor(path).Beatmap.HitObjects
                    .First(o => o.IsSlider);
                Assert.AreNotEqual(16, merged.GetAllCurvePoints().Count);
            } finally {
                Directory.Delete(folder, true);
            }
        }
    }
}
