using System;
using System.ComponentModel;
using System.Linq;
using Mapping_Tools.Classes.SystemTools.Platform;
using Mapping_Tools.Viewmodels;

namespace Mapping_Tools.Classes.Tools.PatternGallery {
    public static class PatternGalleryRunner {
        public static string Export(PatternGalleryVm args,
            BackgroundWorker worker = null) {
            var selected = args.Patterns.Where(o => o.IsSelected).ToList();
            if (selected.Count == 0) {
                throw new InvalidOperationException("Select at least one pattern to export.");
            }
            if (args.Paths is null || args.Paths.Length == 0) {
                throw new InvalidOperationException("Open a destination beatmap first.");
            }
            if (args.ExportTimeMode == ExportTimeMode.Current &&
                !CorePlatform.EditorReader.IsAvailable) {
                throw new InvalidOperationException(
                    "Current editor time requires the editor reader. Choose pattern offset or a custom time on Linux.");
            }
            object reader = CorePlatform.EditorReader.GetFullEditorReaderOrNot();
            double exportTime = args.ExportTimeMode switch {
                ExportTimeMode.Current => CorePlatform.EditorReader.GetEditorTime(),
                ExportTimeMode.Custom => args.CustomExportTime,
                _ => 0
            };
            bool usePatternOffset = args.ExportTimeMode == ExportTimeMode.Pattern;
            int mapsDone = 0;
            foreach (string path in args.Paths) {
                var editor = CorePlatform.EditorReader.GetNewestVersionOrNot(path, reader);
                foreach (var pattern in selected) {
                    var patternBeatmap = args.FileHandler.GetPatternBeatmap(pattern.FileName);
                    if (usePatternOffset) {
                        args.OsuPatternPlacer.PlaceOsuPattern(patternBeatmap,
                            editor.Beatmap, protectBeatmapPattern: false);
                    } else {
                        args.OsuPatternPlacer.PlaceOsuPatternAtTime(patternBeatmap,
                            editor.Beatmap, exportTime, false);
                    }
                    pattern.UseCount++;
                    pattern.LastUsedTime = DateTime.Now;
                }
                editor.SaveFile();
                mapsDone++;
                if (worker is { WorkerReportsProgress: true }) {
                    worker.ReportProgress(mapsDone * 100 / args.Paths.Length);
                }
            }
            return $"Successfully exported {selected.Count} " +
                   $"pattern{(selected.Count == 1 ? string.Empty : "s")} to " +
                   $"{mapsDone} beatmap{(mapsDone == 1 ? string.Empty : "s")}.";
        }
    }
}
