using System;
using System.IO;
using System.Text;
using Mapping_Tools.Classes.SystemTools;
using Mapping_Tools.Classes.SystemTools.Platform;
using Mapping_Tools.Viewmodels;
using Microsoft.VisualStudio.TestTools.UnitTesting;

namespace Mapping_Tools_Tests.Classes.SystemTools {
    [TestClass]
    public class ProjectManagerTests {
        [TestMethod]
        public void LoadsProjectsWrittenBeforeTheCoreAssemblySplit() {
            using var output = new MemoryStream();
            using (var writer = new StreamWriter(output, new UTF8Encoding(false),
                       bufferSize: 1024, leaveOpen: true)) {
                ProjectManager.WriteJson(writer, new MapCleanerVm());
            }

            var currentJson = Encoding.UTF8.GetString(output.ToArray());
            var legacyJson = currentJson.Replace(
                ", Mapping_Tools.Core", ", Mapping Tools", StringComparison.Ordinal);
            Assert.AreNotEqual(currentJson, legacyJson,
                "The fixture did not contain a core assembly identity to translate.");
            using var stream = new MemoryStream(Encoding.UTF8.GetBytes(legacyJson));

            var project = ProjectManager.LoadJson<MapCleanerVm>(stream);

            Assert.IsNotNull(project);
            Assert.IsNotNull(project.MapCleanerArgs);
            Assert.IsTrue(project.MapCleanerArgs.BeatDivisors.Length > 0);
        }

        [TestMethod]
        public void ProjectDialogCancellationIsSafeForEveryHelper() {
            var folder = Path.Combine(Path.GetTempPath(), $"mt-{Guid.NewGuid():N}");
            var view = new FakeSavable(folder);
            var previous = CorePlatform.FileDialogs;
            CorePlatform.FileDialogs = new NullFileDialogService();

            try {
                ProjectManager.LoadProject(view, dialog: true);
                Assert.IsFalse(view.WasLoaded);

                Assert.AreEqual(0, ProjectManager.GetProject(view, dialog: true));

                ProjectManager.SaveToolFile<int, string>(view, "data", dialog: true);
                Assert.IsNull(ProjectManager.LoadToolFile<int, string>(view, dialog: true));
            } finally {
                CorePlatform.FileDialogs = previous;
                if (Directory.Exists(folder)) Directory.Delete(folder, true);
            }
        }

        private sealed class FakeSavable : ISavable<int> {
            public FakeSavable(string folder) {
                DefaultSaveFolder = folder;
                AutoSavePath = Path.Combine(folder, "autosave.json");
            }

            public bool WasLoaded { get; private set; }
            public int GetSaveData() => 1;
            public void SetSaveData(int saveData) => WasLoaded = true;
            public string AutoSavePath { get; }
            public string DefaultSaveFolder { get; }
        }
    }
}
