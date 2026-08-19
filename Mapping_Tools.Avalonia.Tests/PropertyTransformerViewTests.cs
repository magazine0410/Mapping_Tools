using System;
using System.IO;
using System.Linq;
using Avalonia.Controls;
using Avalonia.VisualTree;
using Mapping_Tools.Avalonia.Views;
using Mapping_Tools.Avalonia.Views.PropertyTransformer;
using Mapping_Tools.Classes.BeatmapHelper;
using Mapping_Tools.Components;
using Mapping_Tools.Viewmodels;
using Microsoft.VisualStudio.TestTools.UnitTesting;

namespace Mapping_Tools.Avalonia.Tests {
    [TestClass]
    public class PropertyTransformerViewTests {
        private static string CopyTestMap() {
            var source = Path.Combine(AppContext.BaseDirectory, "Resources",
                "ComplicatedTestMap.osu");
            var target = Path.Combine(Path.GetTempPath(),
                $"mt-{Guid.NewGuid():N}-{Path.GetFileName(source)}");
            File.Copy(source, target);
            return target;
        }

        [TestMethod]
        public void TheViewDrawsAllTransformFields() {
            var view = new PropertyTransformerView();
            var window = HeadlessApp.Show(view);
            HeadlessApp.Draw(window);

            Assert.IsTrue(view.CanRun);
            Assert.AreEqual(32, view.GetVisualDescendants()
                .OfType<ValidatedTextBox>().Count());
            CollectionAssert.Contains(ViewCollection.GetAllToolTypes(), typeof(PropertyTransformerView));
        }

        [TestMethod]
        public void ItTransformsARealBeatmap() {
            var path = CopyTestMap();
            try {
                var before = new BeatmapEditor(path).Beatmap.HitObjects[0].Time;
                var vm = new PropertyTransformerVm {
                    HitObjectTimeOffset = 100,
                    ExportPaths = new[] { path }
                };

                var message = PropertyTransformerView.RunProgram(vm, null);

                var after = new BeatmapEditor(path).Beatmap.HitObjects[0].Time;
                Assert.AreEqual(before + 100, after, 0.001);
                Assert.AreEqual("Done!", message);
            } finally {
                File.Delete(path);
            }
        }
    }
}
