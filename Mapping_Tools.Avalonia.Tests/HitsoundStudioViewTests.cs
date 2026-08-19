using System;
using System.IO;
using System.Linq;
using System.Text;
using Mapping_Tools.Avalonia.Views.HitsoundStudio;
using Mapping_Tools.Classes.BeatmapHelper.Enums;
using Mapping_Tools.Classes.HitsoundStuff;
using Mapping_Tools.Viewmodels;
using Microsoft.VisualStudio.TestTools.UnitTesting;

namespace Mapping_Tools.Avalonia.Tests {
    [TestClass]
    public class HitsoundStudioViewTests {
        private static string CopyMap(string directory) {
            Directory.CreateDirectory(directory);
            string source = Path.Combine(AppContext.BaseDirectory, "Resources",
                "EmptyTestMap.osu");
            string target = Path.Combine(directory, "base.osu");
            File.Copy(source, target);
            return target;
        }

        private static string MakeWave(string directory) {
            string path = Path.Combine(directory, "sample.wav");
            const int sampleRate = 8000;
            const int sampleCount = 800;
            const int dataLength = sampleCount * 2;
            using var stream = File.Create(path);
            using var writer = new BinaryWriter(stream, Encoding.ASCII);
            writer.Write(Encoding.ASCII.GetBytes("RIFF"));
            writer.Write(36 + dataLength);
            writer.Write(Encoding.ASCII.GetBytes("WAVEfmt "));
            writer.Write(16);
            writer.Write((short)1);
            writer.Write((short)1);
            writer.Write(sampleRate);
            writer.Write(sampleRate * 2);
            writer.Write((short)2);
            writer.Write((short)16);
            writer.Write(Encoding.ASCII.GetBytes("data"));
            writer.Write(dataLength);
            for (int i = 0; i < sampleCount; i++) {
                short value = (short)(Math.Sin(i * Math.PI / 20) * 4000);
                writer.Write(value);
            }
            return path;
        }

        [TestMethod]
        public void HitsoundStudioDrawsAndExportsAMapAndSamples() {
            var view = new HitsoundStudioView();
            var window = HeadlessApp.Show(view);
            HeadlessApp.Draw(window);
            string root = Path.Combine(Path.GetTempPath(), $"mt-{Guid.NewGuid():N}");
            string sourceDirectory = Path.Combine(root, "source");
            string exportDirectory = Path.Combine(root, "export");
            string beatmap = CopyMap(sourceDirectory);
            string wave = MakeWave(sourceDirectory);
            try {
                var vm = new HitsoundStudioVm {
                    BaseBeatmap = beatmap,
                    ExportFolder = exportDirectory,
                    HitsoundDiffName = "Hitsound Test",
                    ExportMap = true,
                    ExportSamples = true,
                    ShowResults = true
                };
                vm.HitsoundLayers.Add(new HitsoundLayer("Clap", SampleSet.Normal,
                    Hitsound.Clap, new SampleGeneratingArgs(wave),
                    new LayerImportArgs(ImportType.None)) {
                    Times = new() { 1000, 1500 }
                });

                string message = global::Mapping_Tools.Classes.Tools.HitsoundStudioRunner
                    .Export(vm);

                Assert.IsTrue(Directory.GetFiles(exportDirectory, "*.osu").Any());
                Assert.IsTrue(Directory.GetFiles(exportDirectory, "*.wav").Any());
                StringAssert.Contains(message, "Number of sample indices");
                Assert.IsNotNull(vm.PreviousSampleSchema);
            } finally {
                if (Directory.Exists(root)) Directory.Delete(root, true);
            }
        }

        [TestMethod]
        public void HitsoundStudioVisibilityIsPortable() {
            var vm = new HitsoundStudioVm {
                HitsoundExportModeSetting = HitsoundStudioVm.HitsoundExportMode.Midi
            };
            Assert.IsTrue(vm.IsMidiSettingsVisible);
            Assert.IsFalse(vm.AreSampleExportSettingsVisible);
            vm.HitsoundExportModeSetting = HitsoundStudioVm.HitsoundExportMode.Standard;
            Assert.IsTrue(vm.IsStandardSettingsVisible);
        }
    }
}
