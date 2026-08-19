using System;
using System.Collections.Generic;
using System.ComponentModel;
using System.IO;
using System.Linq;
using Mapping_Tools.Classes.BeatmapHelper;
using Mapping_Tools.Classes.BeatmapHelper.Enums;
using Mapping_Tools.Classes.BeatmapHelper.Events;
using Mapping_Tools.Classes.HitsoundStuff;
using Mapping_Tools.Classes.MathUtil;
using Mapping_Tools.Classes.SystemTools.Platform;
using Mapping_Tools.Classes.ToolHelpers;
using Mapping_Tools.Viewmodels;

namespace Mapping_Tools.Classes.Tools {
    /// <summary>Portable implementation used by every Hitsound Copier host.</summary>
    public static class HitsoundCopier {
        public static string Copy(HitsoundCopierVm args, BackgroundWorker worker = null) {
            bool useMutedIndex = args.MutedIndex >= 0;
            var paths = (args.PathTo ?? string.Empty)
                .Split('|', StringSplitOptions.RemoveEmptyEntries);
            int mapsDone = 0;
            var sampleSchema = new SampleSchema();
            var reader = CorePlatform.EditorReader.GetFullEditorReaderOrNot();

            foreach (var pathTo in paths) {
                var editorTo = CorePlatform.EditorReader.GetNewestVersionOrNot(pathTo, reader);
                var beatmapTo = editorTo.Beatmap;
                Beatmap beatmapFrom;
                if (!string.IsNullOrEmpty(args.PathFrom)) {
                    beatmapFrom = CorePlatform.EditorReader
                        .GetNewestVersionOrNot(args.PathFrom, reader).Beatmap;
                } else {
                    beatmapFrom = beatmapTo.DeepCopy();
                    beatmapFrom.HitObjects.Clear();
                    beatmapFrom.BeatmapTiming.Clear();
                }

                Timeline processedTimeline;
                double fromFirst = beatmapFrom.BeatmapTiming.TimingPoints.Count > 0
                    ? beatmapFrom.BeatmapTiming.TimingPoints[0].Offset
                    : double.PositiveInfinity;
                double toFirst = beatmapTo.BeatmapTiming.TimingPoints.Count > 0
                    ? beatmapTo.BeatmapTiming.TimingPoints[0].Offset
                    : double.PositiveInfinity;
                double firstTime = Math.Min(fromFirst, toFirst);

                List<double> GetMuteTimes(Timeline timeline) =>
                    args.CopyVolumes && args.AlwaysPreserve5Volume
                        ? timeline.TimelineObjects.Where(o =>
                                Math.Abs(o.SampleVolume) < Precision.DoubleEpsilon &&
                                Math.Abs(o.FenoSampleVolume - 5) < Precision.DoubleEpsilon)
                            .Select(o => o.Time).ToList()
                        : null;

                void ApplyMuteTimes(List<double> muteTimes, Timeline timeline,
                    Beatmap beatmap) {
                    var changes = new List<TimingPointsChange>();
                    processedTimeline.GiveTimingPoints(beatmap.BeatmapTiming);
                    foreach (var timelineObject in timeline.TimelineObjects.Where(o =>
                                 Math.Abs(o.SampleVolume) < Precision.DoubleEpsilon &&
                                 Precision.AlmostBigger(o.Time, firstTime))) {
                        var point = timelineObject.HitsoundTimingPoint.Copy();
                        point.Offset = timelineObject.Time;
                        point.Volume = muteTimes.Contains(timelineObject.Time)
                            ? 5
                            : timelineObject.FenoSampleVolume;
                        changes.Add(new TimingPointsChange(point, volume: true));
                    }
                    TimingPointsChange.ApplyChanges(beatmap.BeatmapTiming, changes);
                }

                if (args.CopyMode == 0) {
                    var toTimeline = beatmapTo.GetTimeline();
                    var fromTimeline = beatmapFrom.GetTimeline();
                    var volumeMuteTimes = GetMuteTimes(toTimeline);

                    if (args.CopyHitsounds) {
                        ResetHitObjectHitsounds(beatmapTo);
                        CopyDefinedHitsounds(args, fromTimeline, toTimeline);
                    }

                    var changes = beatmapFrom.BeatmapTiming.TimingPoints.Select(point =>
                        new TimingPointsChange(point, sampleset: args.CopySampleSets,
                            index: args.CopySampleSets, volume: args.CopyVolumes)).ToList();
                    var firstPoint = beatmapFrom.BeatmapTiming
                        .GetTimingPointAtTime(firstTime).Copy();
                    firstPoint.Offset = firstTime;
                    changes.Add(new TimingPointsChange(firstPoint,
                        sampleset: args.CopySampleSets, index: args.CopySampleSets,
                        volume: args.CopyVolumes));
                    TimingPointsChange.ApplyChanges(beatmapTo.BeatmapTiming, changes, true);
                    processedTimeline = toTimeline;
                    if (volumeMuteTimes is not null) {
                        ApplyMuteTimes(volumeMuteTimes, processedTimeline, beatmapTo);
                    }
                } else {
                    var toTimeline = beatmapTo.GetTimeline();
                    var fromTimeline = beatmapFrom.GetTimeline();
                    var volumeMuteTimes = GetMuteTimes(toTimeline);
                    var changes = new List<TimingPointsChange>();
                    var mode = (GameMode)beatmapTo.General["Mode"].IntValue;
                    var mapDirectory = editorTo.GetParentFolder();
                    var firstSamples = HitsoundImporter.AnalyzeSamples(mapDirectory);

                    if (args.CopyHitsounds) {
                        CopySmartHitsounds(args, firstTime, beatmapTo, fromTimeline,
                            toTimeline, changes, mode, mapDirectory, firstSamples,
                            ref sampleSchema);
                    }

                    if (args.CopyBodyHitsounds) {
                        foreach (var point in from hitObject in beatmapTo.HitObjects
                                 from bodyPoint in hitObject.BodyHitsounds
                                 where beatmapFrom.HitObjects.Any(o =>
                                     o.Time < bodyPoint.Offset && o.EndTime > bodyPoint.Offset)
                                 where !bodyPoint.Uninherited
                                 select bodyPoint) {
                            beatmapTo.BeatmapTiming.Remove(point);
                        }
                        changes.AddRange(from hitObject in beatmapFrom.HitObjects
                            from bodyPoint in hitObject.BodyHitsounds
                            where beatmapTo.HitObjects.Any(o =>
                                o.Time < bodyPoint.Offset && o.EndTime > bodyPoint.Offset)
                            select new TimingPointsChange(bodyPoint.Copy(),
                                sampleset: args.CopySampleSets,
                                index: args.CopySampleSets, volume: args.CopyVolumes));
                    }

                    TimingPointsChange.ApplyChanges(beatmapTo.BeatmapTiming, changes);
                    processedTimeline = toTimeline;
                    if (volumeMuteTimes is not null) {
                        ApplyMuteTimes(volumeMuteTimes, processedTimeline, beatmapTo);
                    }
                }

                if (args.CopyStoryboardedSamples) {
                    if (args.CopyMode == 0) beatmapTo.StoryboardSoundSamples.Clear();
                    beatmapTo.GiveObjectsGreenlines();
                    processedTimeline.GiveTimingPoints(beatmapTo.BeatmapTiming);
                    var mapDirectory = editorTo.GetParentFolder();
                    var firstSamples = HitsoundImporter.AnalyzeSamples(mapDirectory, true);
                    var samplesTo = new HashSet<StoryboardSoundSample>(
                        beatmapTo.StoryboardSoundSamples);
                    var mode = (GameMode)beatmapTo.General["Mode"].IntValue;

                    foreach (var sampleFrom in beatmapFrom.StoryboardSoundSamples) {
                        if (args.IgnoreHitsoundSatisfiedSamples) {
                            var nearby = processedTimeline.TimelineObjects.FindAll(o =>
                                Math.Abs(o.Time - sampleFrom.StartTime) <=
                                args.TemporalLeniency);
                            var playing = new HashSet<string>();
                            foreach (var timelineObject in nearby) {
                                foreach (var filename in timelineObject.GetPlayingFilenames(mode)) {
                                    playing.Add(ResolveSamplePath(mapDirectory, filename,
                                        firstSamples));
                                }
                            }
                            if (playing.Contains(ResolveSamplePath(mapDirectory,
                                    sampleFrom.FilePath, firstSamples))) continue;
                        }
                        if (args.IgnoreWheneverHitsound &&
                            processedTimeline.TimelineObjects.Any(o =>
                                Math.Abs(o.Time - sampleFrom.StartTime) <=
                                args.TemporalLeniency)) continue;
                        if (!samplesTo.Contains(sampleFrom)) {
                            beatmapTo.StoryboardSoundSamples.Add(sampleFrom);
                        }
                    }
                    beatmapTo.StoryboardSoundSamples.Sort();
                }

                if (args.MuteSliderends) {
                    var changes = new List<TimingPointsChange>();
                    beatmapTo.GiveObjectsGreenlines();
                    processedTimeline.GiveTimingPoints(beatmapTo.BeatmapTiming);
                    foreach (var timelineObject in processedTimeline.TimelineObjects
                                 .Where(o => Precision.AlmostBigger(o.Time, firstTime))) {
                        var point = timelineObject.HitsoundTimingPoint.Copy();
                        point.Offset = timelineObject.Time;
                        if (ShouldMute(timelineObject, beatmapTo, args)) {
                            timelineObject.SampleSet = args.MutedSampleSet;
                            timelineObject.AdditionSet = 0;
                            timelineObject.Normal = false;
                            timelineObject.Whistle = false;
                            timelineObject.Finish = false;
                            timelineObject.Clap = false;
                            timelineObject.HitsoundsToOrigin();
                            point.SampleSet = args.MutedSampleSet;
                            point.SampleIndex = args.MutedIndex;
                            point.Volume = 5;
                        }
                        changes.Add(new TimingPointsChange(point, sampleset: true,
                            index: useMutedIndex, volume: true));
                    }
                    TimingPointsChange.ApplyChanges(beatmapTo.BeatmapTiming, changes);
                }

                editorTo.SaveFile();
                if (worker is { WorkerReportsProgress: true }) {
                    worker.ReportProgress(++mapsDone * 100 / paths.Length);
                } else {
                    mapsDone++;
                }
            }

            if (sampleSchema.Count > 0) {
                var exportFolder = CorePlatform.Paths.ExportPath;
                Directory.CreateDirectory(exportFolder);
                foreach (var file in new DirectoryInfo(exportFolder).GetFiles()) file.Delete();
                HitsoundExporter.ExportSampleSchema(sampleSchema, exportFolder);
                CorePlatform.Shell.OpenFolder(exportFolder);
            }
            return "Done!";
        }

        private static string ResolveSamplePath(string directory, string filename,
            IReadOnlyDictionary<string, string> samples) {
            var path = Path.Combine(directory, filename);
            var withoutExtension = Path.Combine(Path.GetDirectoryName(path) ?? string.Empty,
                Path.GetFileNameWithoutExtension(path));
            return samples.TryGetValue(withoutExtension, out var actual) ? actual : path;
        }

        private static void CopyDefinedHitsounds(HitsoundCopierVm args, Timeline from,
            Timeline to) {
            foreach (var source in from.TimelineObjects.Where(o => o.HasHitsound)) {
                var destination = to.GetNearestTlo(source.Time, true);
                if (destination is not null && Math.Abs(Math.Round(source.Time) -
                    Math.Round(destination.Time)) <= args.TemporalLeniency) {
                    CopyTimelineObject(args, source, destination);
                }
                source.CanCopy = false;
            }
        }

        private static void CopySmartHitsounds(HitsoundCopierVm args, double firstTime,
            Beatmap beatmapTo, Timeline from, Timeline to,
            List<TimingPointsChange> changes, GameMode mode, string mapDirectory,
            Dictionary<string, string> firstSamples, ref SampleSchema sampleSchema) {
            var customSampledTimes = new HashSet<int>();
            var sliderSlides = new List<TimelineObject>();

            foreach (var source in from.TimelineObjects.Where(o => o.HasHitsound)) {
                var destination = to.GetNearestTlo(source.Time, true);
                if (destination is not null && Math.Abs(Math.Round(source.Time) -
                    Math.Round(destination.Time)) <= args.TemporalLeniency) {
                    CopyTimelineObject(args, source, destination);
                    if (Precision.AlmostBigger(destination.Time, firstTime)) {
                        var point = source.HitsoundTimingPoint.Copy();
                        point.Offset = destination.Time;
                        changes.Add(new TimingPointsChange(point,
                            sampleset: args.CopySampleSets, index: args.CopySampleSets,
                            volume: args.CopyVolumes));
                    }
                } else if (args.CopyToSliderTicks && FindSliderTickInRange(beatmapTo,
                               source.Time - args.TemporalLeniency,
                               source.Time + args.TemporalLeniency,
                               out var tickTime, out var tickSlider) &&
                           !customSampledTimes.Contains((int)tickTime)) {
                    var samples = source.GetFirstPlayingFilenames(mode, mapDirectory,
                            firstSamples, false)
                        .Select(o => new SampleGeneratingArgs(Path.Combine(mapDirectory, o)))
                        .Where(o => SampleImporter.ValidateSampleArgs(o, true)).ToList();
                    if (samples.Count > 0) {
                        sampleSchema.AddHitsound(samples, "slidertick", source.FenoSampleSet,
                            out int index, out var sampleSet, args.StartIndex);
                        tickSlider.SampleSet = SampleSet.None;
                        var point = source.HitsoundTimingPoint.Copy();
                        point.Offset = tickTime;
                        point.SampleIndex = index;
                        point.SampleSet = sampleSet;
                        point.Volume = source.FenoSampleVolume;
                        changes.Add(new TimingPointsChange(point,
                            sampleset: args.CopySampleSets, index: args.CopySampleSets,
                            volume: args.CopyVolumes));
                        var revert = source.HitsoundTimingPoint.Copy();
                        revert.Offset = tickTime + 5;
                        changes.Add(new TimingPointsChange(revert,
                            sampleset: args.CopySampleSets, index: args.CopySampleSets,
                            volume: args.CopyVolumes));
                        customSampledTimes.Add((int)tickTime);
                    }
                } else if (args.CopyToSliderSlides) {
                    sliderSlides.Add(source);
                }
                source.CanCopy = false;
            }

            foreach (var source in sliderSlides) {
                if (!FindSliderAtTime(beatmapTo, source.Time, out var slider) ||
                    customSampledTimes.Contains((int)source.Time)) continue;
                var samples = source.GetFirstPlayingFilenames(mode, mapDirectory,
                        firstSamples, false)
                    .Select(o => new SampleGeneratingArgs(Path.Combine(mapDirectory, o)))
                    .Where(o => SampleImporter.ValidateSampleArgs(o)).ToList();
                if (samples.Count == 0) continue;
                sampleSchema.AddHitsound(samples, "sliderslide", source.FenoSampleSet,
                    out int index, out var sampleSet, args.StartIndex);
                var point = source.HitsoundTimingPoint.Copy();
                point.Offset = source.Time;
                point.SampleIndex = index;
                point.SampleSet = sampleSet;
                point.Volume = source.FenoSampleVolume;
                changes.Add(new TimingPointsChange(point,
                    sampleset: args.CopySampleSets, index: args.CopySampleSets,
                    volume: args.CopyVolumes));
                slider.SampleSet = SampleSet.None;
            }

            foreach (var destination in to.TimelineObjects) {
                if (!destination.CanCopy ||
                    !Precision.AlmostBigger(destination.Time, firstTime)) continue;
                var point = destination.HitsoundTimingPoint.Copy();
                bool holdSampleSet = args.CopySampleSets &&
                                     destination.SampleSet == SampleSet.None;
                bool holdIndex = args.CopySampleSets &&
                                 !(destination.CanCustoms && destination.CustomIndex != 0);
                if (holdSampleSet || holdIndex) {
                    var native = destination.GetFirstPlayingFilenames(mode, mapDirectory,
                        firstSamples);
                    if (holdSampleSet) {
                        var old = destination.FenoSampleSet;
                        var changed = LatestSampleSet(changes, destination.Time, old);
                        point.SampleSet = changed;
                        destination.GiveHitsoundTimingPoint(point);
                        point.SampleSet = native.SequenceEqual(destination
                            .GetFirstPlayingFilenames(mode, mapDirectory, firstSamples))
                            ? changed
                            : old;
                    }
                    if (holdIndex) {
                        var old = destination.FenoCustomIndex;
                        var changed = LatestIndex(changes, destination.Time, old);
                        point.SampleIndex = changed;
                        destination.GiveHitsoundTimingPoint(point);
                        point.SampleIndex = native.SequenceEqual(destination
                            .GetFirstPlayingFilenames(mode, mapDirectory, firstSamples))
                            ? changed
                            : old;
                    }
                    destination.GiveHitsoundTimingPoint(point);
                }
                point.Offset = destination.Time;
                changes.Add(new TimingPointsChange(point, sampleset: holdSampleSet,
                    index: holdIndex, volume: args.CopyVolumes));
            }
        }

        private static SampleSet LatestSampleSet(IEnumerable<TimingPointsChange> changes,
            double time, SampleSet fallback) {
            return changes.Where(o => o.Sampleset && o.MyTp.Offset <= time)
                .OrderBy(o => o.MyTp.Offset).Select(o => o.MyTp.SampleSet)
                .LastOrDefault(fallback);
        }

        private static int LatestIndex(IEnumerable<TimingPointsChange> changes,
            double time, int fallback) {
            return changes.Where(o => o.Index && o.MyTp.Offset <= time)
                .OrderBy(o => o.MyTp.Offset).Select(o => o.MyTp.SampleIndex)
                .LastOrDefault(fallback);
        }

        private static bool FindSliderTickInRange(Beatmap beatmap, double start,
            double end, out double tickTime, out HitObject tickSlider) {
            double tickRate = beatmap.Difficulty.TryGetValue("SliderTickRate", out var value)
                ? value.DoubleValue
                : 1;
            foreach (var slider in beatmap.HitObjects.Where(o => o.IsSlider &&
                         !double.IsNaN(o.SliderVelocity) &&
                         (o.Time < end || o.EndTime > start))) {
                foreach (var time in slider.GetSliderTickTimes(tickRate)) {
                    if (time < start || time > end) continue;
                    tickTime = time;
                    tickSlider = slider;
                    return true;
                }
            }
            tickTime = -1;
            tickSlider = null;
            return false;
        }

        private static bool FindSliderAtTime(Beatmap beatmap, double time,
            out HitObject slider) {
            slider = beatmap.HitObjects.FirstOrDefault(o => o.IsSlider &&
                o.Time < time && o.EndTime > time);
            return slider is not null;
        }

        private static void CopyTimelineObject(HitsoundCopierVm args,
            TimelineObject source, TimelineObject destination) {
            destination.SampleSet = source.SampleSet;
            destination.AdditionSet = source.AdditionSet;
            destination.Normal = source.Normal;
            destination.Whistle = source.Whistle;
            destination.Finish = source.Finish;
            destination.Clap = source.Clap;
            if (destination.CanCustoms) {
                destination.CustomIndex = source.CustomIndex;
                destination.SampleVolume = source.SampleVolume;
                destination.Filename = source.Filename;
            }
            if (destination.IsSliderHead && source.IsSliderHead && args.CopyBodyHitsounds) {
                destination.Origin.Hitsounds = source.Origin.Hitsounds;
                destination.Origin.SampleSet = source.Origin.SampleSet;
                destination.Origin.AdditionSet = source.Origin.AdditionSet;
            }
            destination.HitsoundsToOrigin();
            destination.CanCopy = false;
        }

        private static void ResetHitObjectHitsounds(Beatmap beatmap) {
            foreach (var hitObject in beatmap.HitObjects) {
                hitObject.Clap = false;
                hitObject.Whistle = false;
                hitObject.Finish = false;
                hitObject.SampleSet = 0;
                hitObject.AdditionSet = 0;
                hitObject.CustomIndex = 0;
                hitObject.SampleVolume = 0;
                hitObject.Filename = string.Empty;
                if (!hitObject.IsSlider) continue;
                hitObject.EdgeHitsounds = hitObject.EdgeHitsounds.Select(_ => 0).ToList();
                hitObject.EdgeSampleSets = hitObject.EdgeSampleSets
                    .Select(_ => SampleSet.None).ToList();
                hitObject.EdgeAdditionSets = hitObject.EdgeAdditionSets
                    .Select(_ => SampleSet.None).ToList();
            }
        }

        private static bool ShouldMute(TimelineObject timelineObject, Beatmap beatmap,
            HitsoundCopierVm args) {
            if (!timelineObject.CanCopy ||
                !(timelineObject.IsSliderEnd || timelineObject.IsSpinnerEnd) ||
                timelineObject.Repeat != 1) return false;
            if (timelineObject.Whistle || timelineObject.Finish || timelineObject.Clap ||
                (args.MutedSampleSet != SampleSet.None &&
                 timelineObject.FenoSampleSet != args.MutedSampleSet)) return false;

            var allDivisors = args.BeatDivisors.Concat(args.MutedDivisors).ToList();
            var redline = beatmap.BeatmapTiming
                .GetRedlineAtTime(timelineObject.Time - 1);
            var snapped = beatmap.BeatmapTiming.Resnap(timelineObject.Time,
                allDivisors, false, tp: redline);
            var beats = (snapped - redline.Offset) / redline.MpB;
            var possible = allDivisors.Where(divisor =>
                Precision.AlmostEquals(beats % divisor.GetValue(), 0) ||
                Precision.AlmostEquals(beats % divisor.GetValue(), divisor.GetValue()))
                .ToList();
            if (possible.Count == 0 || possible
                    .TakeWhile(o => !args.MutedDivisors.Contains(o)).Any()) return false;
            return Precision.AlmostBigger(timelineObject.Origin.TemporalLength,
                args.MinLength * redline.MpB);
        }
    }
}
