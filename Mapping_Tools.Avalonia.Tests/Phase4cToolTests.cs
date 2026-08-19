using System;
using System.Drawing;
using System.IO;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using Mapping_Tools.Avalonia.Views.HitsoundPreviewHelper;
using Mapping_Tools.Avalonia.Views.PatternGallery;
using Mapping_Tools.Avalonia.Views.SliderPicturator;
using Mapping_Tools.Classes.BeatmapHelper;
using Mapping_Tools.Classes.BeatmapHelper.Enums;
using Mapping_Tools.Classes.HitsoundStuff;
using Mapping_Tools.Classes.SystemTools.Platform;
using Mapping_Tools.Classes.Tools;
using Mapping_Tools.Classes.Tools.PatternGallery;
using Mapping_Tools.Classes.Tools.SlideratorStuff;
using Mapping_Tools.Viewmodels;
using Microsoft.VisualStudio.TestTools.UnitTesting;
using SkiaSharp;

namespace Mapping_Tools.Avalonia.Tests {
    [TestClass]
    public class Phase4cToolTests {
        private static string CopyMap(string name, string directory) {
            Directory.CreateDirectory(directory);
            string source = Path.Combine(AppContext.BaseDirectory, "Resources", name);
            string target = Path.Combine(directory, name);
            File.Copy(source, target);
            return target;
        }

        private static string MakeWave(string directory) {
            string path = Path.Combine(directory, "sample.wav");
            const int sampleRate = 8000;
            const int sampleCount = 400;
            using var stream = File.Create(path);
            using var writer = new BinaryWriter(stream, Encoding.ASCII);
            writer.Write(Encoding.ASCII.GetBytes("RIFF"));
            writer.Write(36 + sampleCount * 2);
            writer.Write(Encoding.ASCII.GetBytes("WAVEfmt "));
            writer.Write(16);
            writer.Write((short)1);
            writer.Write((short)1);
            writer.Write(sampleRate);
            writer.Write(sampleRate * 2);
            writer.Write((short)2);
            writer.Write((short)16);
            writer.Write(Encoding.ASCII.GetBytes("data"));
            writer.Write(sampleCount * 2);
            for (int i = 0; i < sampleCount; i++) {
                writer.Write((short)(Math.Sin(i * Math.PI / 20) * 4000));
            }
            return path;
        }

        [TestMethod]
        public void PatternGalleryDrawsAndPlacesAStoredPattern() {
            string root = Path.Combine(Path.GetTempPath(), $"mt-{Guid.NewGuid():N}");
            var oldPaths = CorePlatform.Paths;
            CorePlatform.Paths = new TestPaths(root);
            global::Avalonia.Controls.Window window = null;
            try {
                var view = new PatternGalleryView();
                window = HeadlessApp.Show(view);
                HeadlessApp.Draw(window);
                string target = CopyMap("EmptyTestMap.osu", root);
                var vm = new PatternGalleryVm {
                    FileHandler = new OsuPatternFileHandler(root),
                    Paths = new[] { target },
                    ExportTimeMode = ExportTimeMode.Custom,
                    CustomExportTime = 1000
                };
                var pattern = vm.AddFromCode("Test circle",
                    "256,192,500,1,0,0:0:0:0:",
                    "0,500,4,2,1,60,1,0");
                pattern.IsSelected = true;

                string message = PatternGalleryRunner.Export(vm);

                var beatmap = new BeatmapEditor(target).Beatmap;
                Assert.AreEqual(1, beatmap.HitObjects.Count);
                Assert.AreEqual(1000, beatmap.HitObjects[0].Time, 1.01);
                Assert.AreEqual(1, pattern.UseCount);
                StringAssert.Contains(message, "1 pattern");
            } finally {
                window?.Close();
                CorePlatform.Paths = oldPaths;
                if (Directory.Exists(root)) Directory.Delete(root, true);
            }
        }

        [TestMethod]
        public void HitsoundPreviewHelperDrawsAndPlacesZones() {
            var view = new HitsoundPreviewHelperView();
            var window = HeadlessApp.Show(view);
            HeadlessApp.Draw(window);
            string root = Path.Combine(Path.GetTempPath(), $"mt-{Guid.NewGuid():N}");
            string target = CopyMap("ComplicatedTestMap.osu", root);
            try {
                var zone = new HitsoundZone {
                    Name = "Everywhere",
                    XPos = -1,
                    YPos = -1,
                    Hitsound = Hitsound.Clap,
                    SampleSet = SampleSet.Drum,
                    AdditionsSet = SampleSet.Soft,
                    CustomIndex = 7,
                    Filename = "preview.wav"
                };

                string message = HitsoundPreviewHelper.Place(new[] { target },
                    new[] { zone });

                var circle = new BeatmapEditor(target).Beatmap.HitObjects
                    .First(o => o.IsCircle);
                Assert.IsTrue(circle.Clap);
                Assert.AreEqual(SampleSet.Drum, circle.SampleSet);
                Assert.AreEqual(7, circle.CustomIndex);
                Assert.AreEqual("preview.wav", circle.Filename);
                StringAssert.Contains(message, "1 beatmap");
            } finally {
                window.Close();
                if (Directory.Exists(root)) Directory.Delete(root, true);
            }
        }

        [TestMethod]
        public void SliderPicturatorDrawsPreviewsAndCreatesARealSlider() {
            string root = Path.Combine(Path.GetTempPath(), $"mt-{Guid.NewGuid():N}");
            Directory.CreateDirectory(root);
            string target = CopyMap("EmptyTestMap.osu", root);
            string imagePath = Path.Combine(root, "picture.png");
            using (var bitmap = new SKBitmap(2, 2)) {
                bitmap.SetPixel(0, 0, SKColors.Black);
                bitmap.SetPixel(1, 0, SKColors.White);
                bitmap.SetPixel(0, 1, SKColors.Red);
                bitmap.SetPixel(1, 1, SKColors.Blue);
                File.WriteAllBytes(imagePath, SliderPicturator.EncodePng(bitmap));
            }
            try {
                var view = new SliderPicturatorView();
                var window = HeadlessApp.Show(view);
                try {
                    HeadlessApp.Draw(window);
                    var vm = new SliderPicturatorVm {
                        Path = target,
                        PictureFile = imagePath,
                        TimeCode = 1000,
                        Duration = 250,
                        CurrentTrackColor = Color.FromArgb(90, 160, 240),
                        BorderColor = Color.White,
                        Quality = 51
                    };
                    vm.PreviewTask.GetAwaiter().GetResult();

                    string message = SliderPicturatorRunner.Picturate(vm);

                    var beatmap = new BeatmapEditor(target).Beatmap;
                    Assert.IsNotNull(vm.PreviewPng);
                    Assert.IsTrue(vm.PreviewPng.Length > 20);
                    Assert.IsTrue(vm.SegmentCount > 0);
                    Assert.AreEqual(1, beatmap.HitObjects.Count(o => o.IsSlider));
                    Assert.IsTrue(beatmap.SpecialColours.ContainsKey("SliderBorder"));
                    StringAssert.Contains(message, "slider picture");
                } finally {
                    window.Close();
                }
            } finally {
                if (Directory.Exists(root)) Directory.Delete(root, true);
            }
        }

        [TestMethod]
        public void HitsoundStudioRendersAudioForThePortablePlaybackService() {
            string root = Path.Combine(Path.GetTempPath(), $"mt-{Guid.NewGuid():N}");
            Directory.CreateDirectory(root);
            string wave = MakeWave(root);
            var oldPaths = CorePlatform.Paths;
            var oldAudio = CorePlatform.Audio;
            var audio = new CapturingAudioService();
            CorePlatform.Paths = new TestPaths(root);
            CorePlatform.Audio = audio;
            try {
                var layer = new HitsoundLayer {
                    Name = "Preview",
                    SampleArgs = new SampleGeneratingArgs(wave)
                };

                string message = HitsoundStudioRunner.Preview(layer);

                Assert.IsTrue(audio.Played);
                Assert.IsTrue(audio.FileExistedWhenPlayed);
                Assert.IsTrue(audio.DeleteRequested);
                StringAssert.Contains(message, "Playing");
            } finally {
                CorePlatform.Paths = oldPaths;
                CorePlatform.Audio = oldAudio;
                if (Directory.Exists(root)) Directory.Delete(root, true);
            }
        }

        private sealed class TestPaths : IAppPaths {
            public TestPaths(string root) {
                AppDataPath = Path.Combine(root, "appdata");
                ExportPath = Path.Combine(root, "export");
                AppCommon = root;
            }
            public string AppDataPath { get; }
            public string ExportPath { get; }
            public string AppCommon { get; }
        }

        private sealed class CapturingAudioService : IAudioPlaybackService {
            public bool IsAvailable => true;
            public bool Played { get; private set; }
            public bool FileExistedWhenPlayed { get; private set; }
            public bool DeleteRequested { get; private set; }
            public void PlayFile(string path, bool deleteWhenFinished = false) {
                Played = true;
                FileExistedWhenPlayed = File.Exists(path);
                DeleteRequested = deleteWhenFinished;
                if (deleteWhenFinished && File.Exists(path)) File.Delete(path);
            }
            public void Stop() { }
        }
    }
}
