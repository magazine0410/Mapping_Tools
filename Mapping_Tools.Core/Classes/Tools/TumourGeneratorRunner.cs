using System;
using System.Collections.Generic;
using System.ComponentModel;
using System.Linq;
using Mapping_Tools.Classes.BeatmapHelper;
using Mapping_Tools.Classes.MathUtil;
using Mapping_Tools.Classes.SystemTools.Platform;
using Mapping_Tools.Classes.ToolHelpers;
using Mapping_Tools.Classes.ToolHelpers.Sliders.Newgen;
using Mapping_Tools.Viewmodels;

namespace Mapping_Tools.Classes.Tools {
    public static class TumourGeneratorRunner {
        public static string Generate(TumourGeneratorVm args,
            BackgroundWorker worker = null) {
            int generated = 0;
            var generator = new TumourGenerating.TumourGenerator {
                TumourLayers = args.TumourLayers,
                JustMiddleAnchors = args.JustMiddleAnchors,
                Scalar = args.Scale,
                Reconstructor = new Reconstructor {
                    DebugConstruction = args.DebugConstruction
                }
            };
            foreach (var layer in args.TumourLayers) layer.Freeze();

            object reader = CorePlatform.EditorReader.GetFullEditorReaderOrNot();
            if (args.ImportModeSetting == TumourGeneratorVm.ImportMode.Selected &&
                !CorePlatform.EditorReader.IsAvailable) {
                throw new InvalidOperationException(
                    "Selected-object mode requires the editor reader. Choose bookmarked, time, or everything on Linux.");
            }
            var paths = args.Paths ?? Array.Empty<string>();
            int pathIndex = 0;
            foreach (string path in paths) {
                var editor = CorePlatform.EditorReader.GetNewestVersionOrNot(path, reader,
                    out var selected, out var readerError);
                if (args.ImportModeSetting == TumourGeneratorVm.ImportMode.Selected &&
                    readerError is not null) {
                    throw new InvalidOperationException("Could not fetch selected hit objects.",
                        readerError);
                }
                var beatmap = editor.Beatmap;
                var timing = beatmap.BeatmapTiming;
                var marked = args.ImportModeSetting switch {
                    TumourGeneratorVm.ImportMode.Selected => selected,
                    TumourGeneratorVm.ImportMode.Bookmarked => beatmap.GetBookmarkedObjects(),
                    TumourGeneratorVm.ImportMode.Time => beatmap.QueryTimeCode(args.TimeCode).ToList(),
                    TumourGeneratorVm.ImportMode.Everything => beatmap.HitObjects,
                    _ => throw new ArgumentOutOfRangeException()
                };
                for (int i = 0; i < marked.Count; i++) {
                    if (generator.TumourGenerate(marked[i])) generated++;
                    if (worker is { WorkerReportsProgress: true }) {
                        int withinMap = marked.Count == 0 ? 100 : i * 100 / marked.Count;
                        worker.ReportProgress((pathIndex * 100 + withinMap) /
                                              Math.Max(1, paths.Length));
                    }
                }

                if (args.FixSv) {
                    var changes = new List<TimingPointsChange>();
                    foreach (var hitObject in beatmap.HitObjects.Where(o => o.IsSlider)) {
                        if (marked.Contains(hitObject) && args.DelegateToBpm) {
                            var after = timing.GetRedlineAtTime(hitObject.Time).Copy();
                            var on = after.Copy();
                            after.Offset = hitObject.Time;
                            on.Offset = hitObject.Time - 1;
                            after.OmitFirstBarLine = on.OmitFirstBarLine = true;
                            on.MpB *= hitObject.SliderVelocity / -100;
                            hitObject.SliderVelocity = args.RemoveSliderTicks
                                ? double.NaN
                                : -100;
                            changes.Add(new TimingPointsChange(on, mpb: true,
                                unInherited: true, omitFirstBarLine: true,
                                fuzzyness: Precision.DoubleEpsilon));
                            changes.Add(new TimingPointsChange(after, mpb: true,
                                unInherited: true, omitFirstBarLine: true,
                                fuzzyness: Precision.DoubleEpsilon));
                            hitObject.Time -= 1;
                        }
                        var point = hitObject.TimingPoint.Copy();
                        point.Offset = hitObject.Time;
                        point.MpB = hitObject.SliderVelocity;
                        changes.Add(new TimingPointsChange(point, mpb: true,
                            fuzzyness: Precision.DoubleEpsilon));
                    }
                    TimingPointsChange.ApplyChanges(timing, changes);
                }
                editor.SaveFile();
                pathIndex++;
            }
            if (worker is { WorkerReportsProgress: true }) worker.ReportProgress(100);
            return $"Successfully generated tumours on {generated} " +
                   $"slider{(generated == 1 ? string.Empty : "s")}!";
        }
    }
}
