using System;
using System.Collections.Generic;
using System.ComponentModel;
using Mapping_Tools.Classes.BeatmapHelper;
using Mapping_Tools.Classes.SystemTools.Platform;
using Mapping_Tools.Viewmodels;

namespace Mapping_Tools.Classes.Tools {
    public static class MetadataManager {
        public static string Apply(MetadataManagerVm args, BackgroundWorker worker = null) {
            var paths = (args.ExportPath ?? string.Empty)
                .Split('|', StringSplitOptions.RemoveEmptyEntries);
            int mapsDone = 0;
            object reader = CorePlatform.EditorReader.GetFullEditorReaderOrNot();
            foreach (string path in paths) {
                var editor = CorePlatform.EditorReader.GetNewestVersionOrNot(path, reader);
                var beatmap = editor.Beatmap;
                beatmap.Metadata["ArtistUnicode"] = new TValue(args.Artist ?? string.Empty);
                beatmap.Metadata["Artist"] = new TValue(args.RomanisedArtist ?? string.Empty);
                beatmap.Metadata["TitleUnicode"] = new TValue(args.Title ?? string.Empty);
                beatmap.Metadata["Title"] = new TValue(args.RomanisedTitle ?? string.Empty);
                beatmap.Metadata["Creator"] = new TValue(args.BeatmapCreator ?? string.Empty);
                beatmap.Metadata["Source"] = new TValue(args.Source ?? string.Empty);
                beatmap.Metadata["Tags"] = new TValue(args.Tags ?? string.Empty);
                beatmap.General["PreviewTime"] = new TValue(args.PreviewTime.ToRoundInvariant());
                if (args.UseComboColours) {
                    beatmap.ComboColours = new List<ComboColour>(args.ComboColours);
                    beatmap.SpecialColours.Clear();
                    foreach (var colour in args.SpecialColours) {
                        if (!string.IsNullOrWhiteSpace(colour.Name)) {
                            beatmap.SpecialColours[colour.Name] = colour;
                        }
                    }
                }
                if (args.ResetIds) {
                    beatmap.Metadata["BeatmapID"] = new TValue("0");
                    beatmap.Metadata["BeatmapSetID"] = new TValue("-1");
                }
                editor.SaveFileWithNameUpdate();
                mapsDone++;
                if (worker is { WorkerReportsProgress: true }) {
                    worker.ReportProgress(mapsDone * 100 / paths.Length);
                }
            }
            return $"Successfully exported metadata to {mapsDone} " +
                   $"{(mapsDone == 1 ? "beatmap" : "beatmaps")}!";
        }
    }
}
