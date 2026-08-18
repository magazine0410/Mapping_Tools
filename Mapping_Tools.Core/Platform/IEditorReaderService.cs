using System;
using System.Collections.Generic;
using Mapping_Tools.Classes.BeatmapHelper;

namespace Mapping_Tools.Classes.SystemTools.Platform {
    /// <summary>
    /// Reads the live state of the osu! editor.
    /// </summary>
    /// <remarks>
    /// The Windows host supplies an implementation that reads the memory of the
    /// osu! process. Linux has no such implementation yet, so it uses
    /// <see cref="NullEditorReaderService"/>, which always reads the file on disk.
    /// The core does not know the type of the reader handle. It only moves it.
    /// </remarks>
    public interface IEditorReaderService {
        /// <summary>True if this host can read the live editor.</summary>
        bool IsAvailable { get; }

        /// <summary>
        /// Stops the next save if the file has this MD5 hash.
        /// </summary>
        string DontCoolSaveWhenMd5EqualsThisString { get; set; }

        string GetMd5FromPath(string path);

        /// <summary>The time of the cursor in the editor, in milliseconds.</summary>
        int GetEditorTime();

        /// <summary>
        /// Gets a reader that holds all the editor data, or null.
        /// Give the result back to <see cref="GetNewestVersionOrNot(string,object)"/>.
        /// </summary>
        object GetFullEditorReaderOrNot();

        BeatmapEditor GetNewestVersionOrNot(string path);
        BeatmapEditor GetNewestVersionOrNot(string path, object fullReader);
        BeatmapEditor GetNewestVersionOrNot(string path, out List<HitObject> selected, out Exception exception);
        BeatmapEditor GetNewestVersionOrNot(string path, object fullReader, out List<HitObject> selected, out Exception exception);

        /// <summary>Brings the osu! editor to the front and makes it reload the beatmap.</summary>
        void ForceReloadEditor();
    }

    /// <summary>
    /// Always reads the file on disk. Nothing is selected. This is the Linux behaviour.
    /// </summary>
    public class NullEditorReaderService : IEditorReaderService {
        public bool IsAvailable => false;

        public string DontCoolSaveWhenMd5EqualsThisString { get; set; } = string.Empty;

        public string GetMd5FromPath(string path) {
            using var md5 = System.Security.Cryptography.MD5.Create();
            using var stream = System.IO.File.OpenRead(path);
            return BitConverter.ToString(md5.ComputeHash(stream)).Replace("-", string.Empty).ToLowerInvariant();
        }

        public int GetEditorTime() => 0;

        public object GetFullEditorReaderOrNot() => null;

        public BeatmapEditor GetNewestVersionOrNot(string path) => new BeatmapEditor(path);

        public BeatmapEditor GetNewestVersionOrNot(string path, object fullReader) => new BeatmapEditor(path);

        public BeatmapEditor GetNewestVersionOrNot(string path, out List<HitObject> selected, out Exception exception) {
            selected = new List<HitObject>();
            exception = null;
            return new BeatmapEditor(path);
        }

        public BeatmapEditor GetNewestVersionOrNot(string path, object fullReader,
            out List<HitObject> selected, out Exception exception) {
            selected = new List<HitObject>();
            exception = null;
            return new BeatmapEditor(path);
        }

        public void ForceReloadEditor() { }
    }
}
