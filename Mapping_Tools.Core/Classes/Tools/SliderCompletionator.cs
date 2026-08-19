using System;
using System.Collections.Generic;
using System.ComponentModel;
using System.Linq;
using Mapping_Tools.Classes.BeatmapHelper;
using Mapping_Tools.Classes.MathUtil;
using Mapping_Tools.Classes.SystemTools.Platform;
using Mapping_Tools.Classes.ToolHelpers;
using Mapping_Tools.Classes.ToolHelpers.Sliders;
using Mapping_Tools.Viewmodels;

namespace Mapping_Tools.Classes.Tools {
    public static class SliderCompletionator {
        public static string Complete(SliderCompletionatorVm args,
            BackgroundWorker worker = null) {
            int completed = 0;
            double endTime = args.EndTime;
            object reader = CorePlatform.EditorReader.GetFullEditorReaderOrNot();
            if (args.ImportModeSetting == SliderCompletionatorVm.ImportMode.Selected &&
                !CorePlatform.EditorReader.IsAvailable) {
                throw new InvalidOperationException(
                    "Selected-object mode requires the editor reader. Choose bookmarked, time, or everything on Linux.");
            }
            if (args.UseCurrentEditorTime && args.UseEndTime) {
                if (!CorePlatform.EditorReader.IsAvailable) {
                    throw new InvalidOperationException(
                        "Current editor time requires the editor reader.");
                }
                endTime = CorePlatform.EditorReader.GetEditorTime();
            }

            foreach (string path in args.Paths ?? Array.Empty<string>()) {
                var editor = CorePlatform.EditorReader.GetNewestVersionOrNot(path, reader,
                    out var selected, out var readerError);
                if (args.ImportModeSetting == SliderCompletionatorVm.ImportMode.Selected &&
                    readerError is not null) {
                    throw new InvalidOperationException("Could not fetch selected hit objects.",
                        readerError);
                }
                var beatmap = editor.Beatmap;
                var timing = beatmap.BeatmapTiming;
                var marked = args.ImportModeSetting switch {
                    SliderCompletionatorVm.ImportMode.Selected => selected,
                    SliderCompletionatorVm.ImportMode.Bookmarked => beatmap.GetBookmarkedObjects(),
                    SliderCompletionatorVm.ImportMode.Time => beatmap.QueryTimeCode(args.TimeCode).ToList(),
                    SliderCompletionatorVm.ImportMode.Everything => beatmap.HitObjects,
                    _ => throw new ArgumentOutOfRangeException()
                };

                for (int i = 0; i < marked.Count; i++) {
                    var hitObject = marked[i];
                    if (hitObject.IsSlider) {
                        double mpb = timing.GetMpBAtTime(hitObject.Time);
                        double oldDuration = timing.CalculateSliderTemporalLength(
                            hitObject.Time, hitObject.PixelLength);
                        double newDuration = args.UseEndTime
                            ? endTime == -1 && !args.UseCurrentEditorTime
                                ? oldDuration
                                : endTime - hitObject.Time
                            : args.Duration == -1
                                ? oldDuration
                                : timing.WalkBeatsInMillisecondTime(args.Duration,
                                    hitObject.Time) - hitObject.Time;
                        double newLength = args.Length == -1
                            ? hitObject.PixelLength
                            : hitObject.GetSliderPath(true).Distance * args.Length;
                        double newSv = args.SliderVelocity == -1
                            ? timing.GetSvAtTime(hitObject.Time)
                            : -100 / args.SliderVelocity;
                        switch (args.FreeVariableSetting) {
                            case SliderCompletionatorVm.FreeVariable.Velocity:
                                newSv = -10000 * timing.SliderMultiplier * newDuration /
                                        (newLength * mpb);
                                break;
                            case SliderCompletionatorVm.FreeVariable.Duration:
                                newDuration = newLength * newSv * mpb /
                                              (-10000 * timing.SliderMultiplier);
                                break;
                            case SliderCompletionatorVm.FreeVariable.Length:
                                newLength = -10000 * timing.SliderMultiplier * newDuration /
                                            (newSv * mpb);
                                break;
                        }
                        if (double.IsNaN(newSv) || double.IsInfinity(newSv)) {
                            throw new InvalidOperationException(
                                "Encountered an invalid slider velocity. Make sure no input is zero.");
                        }
                        if (newDuration < 0) {
                            throw new InvalidOperationException(
                                "Encountered a negative slider duration. The end time must be after every selected slider.");
                        }
                        hitObject.SliderVelocity = newSv;
                        hitObject.PixelLength = newLength;
                        if (args.MoveAnchors) {
                            hitObject.SetAllCurvePoints(SliderPathUtil.MoveAnchorsToLength(
                                hitObject.GetAllCurvePoints(), hitObject.SliderType,
                                hitObject.PixelLength, out var pathType));
                            hitObject.SliderType = pathType;
                        }
                        completed++;
                    }
                    if (worker is { WorkerReportsProgress: true }) {
                        worker.ReportProgress(marked.Count == 0 ? 100 :
                            i * 100 / marked.Count);
                    }
                }

                var changes = new List<TimingPointsChange>();
                foreach (var hitObject in beatmap.HitObjects.Where(o => o.IsSlider)) {
                    if (marked.Contains(hitObject) && args.DelegateToBpm) {
                        var after = timing.GetRedlineAtTime(hitObject.Time).Copy();
                        var on = after.Copy();
                        after.Offset = hitObject.Time;
                        on.Offset = hitObject.Time - 1;
                        after.OmitFirstBarLine = on.OmitFirstBarLine = true;
                        on.MpB *= hitObject.SliderVelocity / -100;
                        hitObject.SliderVelocity = args.RemoveSliderTicks ? double.NaN : -100;
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
                editor.SaveFile();
            }
            if (worker is { WorkerReportsProgress: true }) worker.ReportProgress(100);
            return $"Successfully completed {completed} " +
                   $"{(completed == 1 ? "slider" : "sliders")}!";
        }
    }
}
