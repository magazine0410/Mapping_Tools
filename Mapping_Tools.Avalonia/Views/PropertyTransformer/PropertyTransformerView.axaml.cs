using System;
using System.Collections.Generic;
using System.ComponentModel;
using System.IO;
using System.Linq;
using Avalonia.Interactivity;
using Avalonia.VisualTree;
using Mapping_Tools.Classes.BeatmapHelper;
using Mapping_Tools.Classes.BeatmapHelper.Events;
using Mapping_Tools.Classes.MathUtil;
using Mapping_Tools.Classes.SystemTools;
using Mapping_Tools.Classes.SystemTools.Platform;
using Mapping_Tools.Classes.ToolHelpers;
using Mapping_Tools.Components;
using Mapping_Tools.Viewmodels;

namespace Mapping_Tools.Avalonia.Views.PropertyTransformer {
    [VerticalContentScroll]
    [HorizontalContentScroll]
    public partial class PropertyTransformerView : SingleRunMappingTool,
        ISavable<PropertyTransformerVm> {
        public static readonly string ToolName = "Property Transformer";

        public static readonly string ToolDescription =
            $@"Multiply and add to properties of all timing points, hit objects, bookmarks and storyboarded samples of the current map.{Environment.NewLine}The new value is the old value times the multiplier plus the offset. Resulting values are rounded where osu! requires integers.";

        public PropertyTransformerView() {
            InitializeComponent();
            DataContext = new PropertyTransformerVm();
            ProjectManager.LoadProject(this, message: false);
        }

        public PropertyTransformerVm ViewModel => (PropertyTransformerVm)DataContext;

        private void Start_Click(object sender, RoutedEventArgs e) =>
            RunTool(CorePlatform.FileDialogs.GetCurrentBeatmaps());

        internal void RunTool(string[] paths) {
            if (!CanRun) return;
            if (paths is null || paths.Length == 0) {
                CorePlatform.Dialogs.ShowMessage(
                    "Open a beatmap or storyboard first, with File then Open beatmap.",
                    "No beatmap");
                return;
            }

            foreach (var box in this.GetVisualDescendants().OfType<ValidatedTextBox>()) {
                box.Commit();
            }

            BackupManager.SaveMapBackup(paths);
            ViewModel.ExportPaths = paths;
            BackgroundWorker.RunWorkerAsync(ViewModel);
            CanRun = false;
        }

        protected override void BackgroundWorker_DoWork(object sender, DoWorkEventArgs e) {
            e.Result = RunProgram((PropertyTransformerVm)e.Argument,
                sender as BackgroundWorker);
        }

        internal static string RunProgram(PropertyTransformerVm vm, BackgroundWorker worker) {
            var reader = CorePlatform.EditorReader.GetFullEditorReaderOrNot();

            bool Filter(double value, double time) {
                bool match = vm.MatchFilter.Length > 0 && vm.EnableFilters;
                bool unmatch = vm.UnmatchFilter.Length > 0 && vm.EnableFilters;
                bool range = (vm.MinTimeFilter != -1 || vm.MaxTimeFilter != -1) &&
                             vm.EnableFilters && !double.IsNaN(time);
                double min = vm.MinTimeFilter == -1
                    ? double.NegativeInfinity
                    : vm.MinTimeFilter;
                double max = vm.MaxTimeFilter == -1
                    ? double.PositiveInfinity
                    : vm.MaxTimeFilter;

                return (!match || vm.MatchFilter.Any(o =>
                           Precision.AlmostEquals(value, o, 0.001))) &&
                       (!unmatch || !vm.UnmatchFilter.Any(o =>
                           Precision.AlmostEquals(value, o, 0.001))) &&
                       (!range || time >= min && time <= max);
            }

            void TransformProperty(double multiplier, double offset, Func<double> getter,
                Action<double> setter, double time, double? min = null,
                double? max = null, bool round = false) {
                if (multiplier == 1 && offset == 0) return;

                var value = getter();
                if (!Filter(value, time)) return;

                var newValue = value * multiplier + offset;
                if (round) newValue = Math.Round(newValue);
                if (vm.ClipProperties) {
                    if (min.HasValue) newValue = Math.Max(newValue, min.Value);
                    if (max.HasValue) newValue = Math.Min(newValue, max.Value);
                }
                setter(newValue);
            }

            void TransformEventTime(Beatmap beatmap, Event ev, double multiplier,
                double offset) {
                var version = beatmap?.Version ?? 14;
                bool relative = ev.ParentEvent is StandardLoop or TriggerLoop;

                if (ev is IHasStartTime start && Filter(start.StartTime, start.StartTime)) {
                    var value = start.StartTime * multiplier + (relative ? 0 : offset);
                    start.StartTime = version < 128 ? (int)Math.Round(value) : value;
                }
                if (ev is IHasEndTime end && Filter(end.EndTime, end.EndTime)) {
                    var value = end.EndTime * multiplier + (relative ? 0 : offset);
                    end.EndTime = version < 128 ? (int)Math.Round(value) : value;
                }
                if (ev is IHasDuration duration && Filter(duration.Duration, double.NaN)) {
                    duration.Duration *= multiplier;
                }

                foreach (var child in ev.ChildEvents) {
                    TransformEventTime(beatmap, child, multiplier, offset);
                }
            }

            foreach (string path in vm.ExportPaths ?? Array.Empty<string>()) {
                Editor editor = string.Equals(Path.GetExtension(path), ".osb",
                    StringComparison.OrdinalIgnoreCase)
                    ? new StoryboardEditor(path)
                    : CorePlatform.EditorReader.GetNewestVersionOrNot(path, reader);

                if (editor is BeatmapEditor beatmapEditor) {
                    var beatmap = beatmapEditor.Beatmap;
                    var timingChanges = new List<TimingPointsChange>();

                    foreach (var timingPoint in beatmap.BeatmapTiming.TimingPoints) {
                        TransformProperty(vm.TimingpointOffsetMultiplier,
                            vm.TimingpointOffsetOffset, () => timingPoint.Offset,
                            value => timingPoint.Offset = value, timingPoint.Offset,
                            round: beatmap.Version < 128);

                        if (timingPoint.Uninherited) {
                            TransformProperty(vm.TimingpointBpmMultiplier,
                                vm.TimingpointBpmOffset, timingPoint.GetBpm,
                                timingPoint.SetBpm, timingPoint.Offset, 15, 10000);
                        }

                        TransformProperty(vm.TimingpointSvMultiplier,
                            vm.TimingpointSvOffset,
                            () => beatmap.BeatmapTiming.GetSvMultiplierAtTime(
                                timingPoint.Offset), value => {
                                var changed = timingPoint.Copy();
                                changed.MpB = -100 / value;
                                timingChanges.Add(new TimingPointsChange(changed,
                                    mpb: true, fuzzyness: 0.4));
                            }, timingPoint.Offset, 0.1, 10);

                        TransformProperty(vm.TimingpointIndexMultiplier,
                            vm.TimingpointIndexOffset, () => timingPoint.SampleIndex,
                            value => timingPoint.SampleIndex = (int)value,
                            timingPoint.Offset, 0, int.MaxValue, true);
                        TransformProperty(vm.TimingpointVolumeMultiplier,
                            vm.TimingpointVolumeOffset, () => timingPoint.Volume,
                            value => timingPoint.Volume = (int)value,
                            timingPoint.Offset, 5, 100, true);
                    }

                    UpdateProgressBar(worker, 20);

                    if (vm.HitObjectTimeMultiplier != 1 || vm.HitObjectTimeOffset != 0) {
                        foreach (var hitObject in beatmap.HitObjects) {
                            double oldEndTime = hitObject.GetEndTime(false);
                            TransformProperty(vm.HitObjectTimeMultiplier,
                                vm.HitObjectTimeOffset, () => hitObject.Time,
                                value => hitObject.Time = value, hitObject.Time,
                                round: beatmap.Version < 128);
                            if (hitObject.IsHoldNote || hitObject.IsSpinner) {
                                TransformProperty(vm.HitObjectTimeMultiplier,
                                    vm.HitObjectTimeOffset, () => oldEndTime,
                                    value => hitObject.EndTime = value, oldEndTime,
                                    round: beatmap.Version < 128);
                            }
                        }
                    }

                    UpdateProgressBar(worker, 25);

                    if (vm.HitObjectVolumeMultiplier != 1 ||
                        vm.HitObjectVolumeOffset != 0) {
                        foreach (var hitObject in beatmap.HitObjects) {
                            TransformProperty(vm.HitObjectVolumeMultiplier,
                                vm.HitObjectVolumeOffset, () => hitObject.SampleVolume,
                                value => hitObject.SampleVolume = value,
                                hitObject.Time, 0, 100, true);
                        }
                    }

                    UpdateProgressBar(worker, 30);

                    if (vm.BookmarkTimeMultiplier != 1 || vm.BookmarkTimeOffset != 0) {
                        var bookmarks = beatmap.GetBookmarks();
                        beatmap.SetBookmarks(bookmarks.Select(bookmark =>
                            Filter(bookmark, bookmark)
                                ? beatmap.Version < 128
                                    ? Math.Round(bookmark * vm.BookmarkTimeMultiplier +
                                                 vm.BookmarkTimeOffset)
                                    : bookmark * vm.BookmarkTimeMultiplier +
                                      vm.BookmarkTimeOffset
                                : bookmark).ToList());
                    }

                    UpdateProgressBar(worker, 40);

                    if (vm.SbEventTimeMultiplier != 1 || vm.SbEventTimeOffset != 0) {
                        foreach (var ev in beatmap.StoryboardLayerBackground
                                     .Concat(beatmap.StoryboardLayerFail)
                                     .Concat(beatmap.StoryboardLayerPass)
                                     .Concat(beatmap.StoryboardLayerForeground)
                                     .Concat(beatmap.StoryboardLayerOverlay)) {
                            TransformEventTime(beatmap, ev, vm.SbEventTimeMultiplier,
                                vm.SbEventTimeOffset);
                        }
                    }

                    UpdateProgressBar(worker, 50);

                    foreach (var sample in beatmap.StoryboardSoundSamples) {
                        TransformProperty(vm.SbSampleTimeMultiplier,
                            vm.SbSampleTimeOffset, () => sample.StartTime,
                            value => sample.StartTime = value, sample.StartTime,
                            round: beatmap.Version < 128);
                        TransformProperty(vm.SbSampleVolumeMultiplier,
                            vm.SbSampleVolumeOffset, () => sample.Volume,
                            value => sample.Volume = value, sample.StartTime, 8, 100, true);
                    }

                    UpdateProgressBar(worker, 60);

                    foreach (var breakPeriod in beatmap.BreakPeriods) {
                        TransformProperty(vm.BreakTimeMultiplier, vm.BreakTimeOffset,
                            () => breakPeriod.StartTime,
                            value => breakPeriod.StartTime = value,
                            breakPeriod.StartTime, round: beatmap.Version < 128);
                        TransformProperty(vm.BreakTimeMultiplier, vm.BreakTimeOffset,
                            () => breakPeriod.EndTime,
                            value => breakPeriod.EndTime = value,
                            breakPeriod.EndTime, round: beatmap.Version < 128);
                    }

                    UpdateProgressBar(worker, 70);

                    foreach (var video in beatmap.BackgroundAndVideoEvents.OfType<Video>()) {
                        TransformProperty(vm.VideoTimeMultiplier, vm.VideoTimeOffset,
                            () => video.StartTime, value => video.StartTime = value,
                            video.StartTime, round: beatmap.Version < 128);
                    }

                    UpdateProgressBar(worker, 80);

                    if (beatmap.General.ContainsKey("PreviewTime") &&
                        beatmap.General["PreviewTime"].IntValue != -1) {
                        var previewTime = beatmap.General["PreviewTime"].DoubleValue;
                        TransformProperty(vm.PreviewTimeMultiplier,
                            vm.PreviewTimeOffset, () => previewTime,
                            value => beatmap.General["PreviewTime"].SetDouble(value),
                            previewTime, round: beatmap.Version < 128);
                    }

                    UpdateProgressBar(worker, 90);
                    TimingPointsChange.ApplyChanges(beatmap.BeatmapTiming, timingChanges);
                    beatmapEditor.SaveFile();
                    UpdateProgressBar(worker, 100);
                } else if (editor is StoryboardEditor storyboardEditor) {
                    var storyboard = storyboardEditor.StoryBoard;

                    foreach (var ev in storyboard.StoryboardLayerBackground
                                 .Concat(storyboard.StoryboardLayerFail)
                                 .Concat(storyboard.StoryboardLayerPass)
                                 .Concat(storyboard.StoryboardLayerForeground)
                                 .Concat(storyboard.StoryboardLayerOverlay)) {
                        TransformEventTime(null, ev, vm.SbEventTimeMultiplier,
                            vm.SbEventTimeOffset);
                    }

                    UpdateProgressBar(worker, 50);
                    foreach (var sample in storyboard.StoryboardSoundSamples) {
                        TransformProperty(vm.SbSampleTimeMultiplier,
                            vm.SbSampleTimeOffset, () => sample.StartTime,
                            value => sample.StartTime = value, sample.StartTime, round: true);
                        TransformProperty(vm.SbSampleVolumeMultiplier,
                            vm.SbSampleVolumeOffset, () => sample.Volume,
                            value => sample.Volume = value, sample.StartTime, 8, 100, true);
                    }

                    UpdateProgressBar(worker, 70);
                    foreach (var video in storyboard.BackgroundAndVideoEvents.OfType<Video>()) {
                        TransformProperty(vm.VideoTimeMultiplier, vm.VideoTimeOffset,
                            () => video.StartTime, value => video.StartTime = value,
                            video.StartTime, round: true);
                    }

                    UpdateProgressBar(worker, 90);
                    storyboardEditor.SaveFile();
                    UpdateProgressBar(worker, 100);
                }
            }

            return "Done!";
        }

        public PropertyTransformerVm GetSaveData() => ViewModel;
        public void SetSaveData(PropertyTransformerVm saveData) => DataContext = saveData;

        public string AutoSavePath => Path.Combine(CorePlatform.Paths.AppDataPath,
            "propertytransformerproject.json");
        public string DefaultSaveFolder => Path.Combine(CorePlatform.Paths.AppDataPath,
            "Property Transformer Projects");
    }
}
