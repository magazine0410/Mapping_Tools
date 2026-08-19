using Mapping_Tools.Classes.Tools;
using Mapping_Tools.Classes.SystemTools.Platform;
using System.Collections.Generic;
using System.IO;
using Mapping_Tools.Classes.ToolHelpers;

namespace Mapping_Tools.Classes.BeatmapHelper {
    public class BeatmapEditor : Editor
    {
        public Beatmap Beatmap => (Beatmap)TextFile;

        public BeatmapEditor(List<string> lines)
        {
            TextFile = new Beatmap(lines);
        }

        public BeatmapEditor(string path)
        {
            Path = path;
            TextFile = new Beatmap(ReadFile(Path));
        }

        /// <summary>
        /// Saves the beatmap just like <see cref="SaveFile()"/> but also updates the filename according to the metadata of the <see cref="Beatmap"/>
        /// </summary>
        /// <remarks>This method also updates the Path property</remarks>
        public void SaveFileWithNameUpdate() {
            // Remove the beatmap with the old filename
            File.Delete(Path);

            // Save beatmap with the new filename
            Path = System.IO.Path.Combine(GetParentFolder(), Beatmap.GetFileName());
            SaveFile();
        }

        public override void SaveFile() {
            GenerateBetterSaveMd5(TextFile.GetLines());
            base.SaveFile();
        }

        public override void SaveFile(string path) {
            GenerateBetterSaveMd5(TextFile.GetLines());
            base.SaveFile(path);
        }

        public override void SaveFile(List<string> lines) {
            GenerateBetterSaveMd5(lines);
            base.SaveFile(lines);
        }

        private static void GenerateBetterSaveMd5(List<string> lines) {
            var tempPath = System.IO.Path.Combine(CorePlatform.Paths.AppDataPath, "temp.osu");

            // The host normally makes this folder when it starts, but a host that has
            // not run before, or a test, has no folder yet.
            Directory.CreateDirectory(CorePlatform.Paths.AppDataPath);

            if (!File.Exists(tempPath))
            {
                File.Create(tempPath).Dispose();
            }
            File.WriteAllLines(tempPath, lines);

            CorePlatform.EditorReader.DontCoolSaveWhenMd5EqualsThisString = CorePlatform.EditorReader.GetMd5FromPath(tempPath);
        }
    }
}
