using System;
using System.Collections.Generic;
using System.ComponentModel;
using System.Linq;
using Mapping_Tools.Classes.HitsoundStuff;
using Mapping_Tools.Classes.SystemTools.Platform;

namespace Mapping_Tools.Classes.Tools {
    public static class HitsoundPreviewHelper {
        public static string Place(string[] paths, IReadOnlyCollection<HitsoundZone> zones,
            BackgroundWorker worker = null) {
            if (zones is null || zones.Count == 0) {
                throw new InvalidOperationException("Add at least one hitsound zone first.");
            }
            int mapsDone = 0;
            object reader = CorePlatform.EditorReader.GetFullEditorReaderOrNot();
            foreach (string path in paths ?? Array.Empty<string>()) {
                var editor = CorePlatform.EditorReader.GetNewestVersionOrNot(path, reader);
                var timeline = editor.Beatmap.GetTimeline();
                for (int i = 0; i < timeline.TimelineObjects.Count; i++) {
                    var timelineObject = timeline.TimelineObjects[i];
                    var zone = zones.MinBy(o => o.Distance(timelineObject.Origin.Pos));
                    if (zone is null) continue;
                    timelineObject.Filename = zone.Filename ?? string.Empty;
                    timelineObject.SampleSet = zone.SampleSet;
                    timelineObject.AdditionSet = zone.AdditionsSet;
                    timelineObject.CustomIndex = zone.CustomIndex;
                    timelineObject.SampleVolume = 0;
                    timelineObject.SetHitsound(zone.Hitsound);
                    timelineObject.HitsoundsToOrigin();
                    if (worker is { WorkerReportsProgress: true }) {
                        worker.ReportProgress(timeline.TimelineObjects.Count == 0 ? 100 :
                            i * 100 / timeline.TimelineObjects.Count);
                    }
                }
                editor.SaveFile();
                mapsDone++;
            }
            if (worker is { WorkerReportsProgress: true }) worker.ReportProgress(100);
            return $"Placed preview hitsounds in {mapsDone} " +
                   $"beatmap{(mapsDone == 1 ? string.Empty : "s")}.";
        }
    }
}
