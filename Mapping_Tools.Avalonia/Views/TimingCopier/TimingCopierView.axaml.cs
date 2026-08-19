using System;
using System.Collections.Generic;
using System.ComponentModel;
using System.IO;
using System.Linq;
using Avalonia.Interactivity;
using Avalonia.VisualTree;
using Mapping_Tools.Classes.BeatmapHelper;
using Mapping_Tools.Classes.MathUtil;
using Mapping_Tools.Classes.SystemTools;
using Mapping_Tools.Classes.SystemTools.Platform;
using Mapping_Tools.Classes.ToolHelpers;
using Mapping_Tools.Components;
using Mapping_Tools.Viewmodels;

namespace Mapping_Tools.Avalonia.Views.TimingCopier {
    [VerticalContentScroll]
    public partial class TimingCopierView : SingleRunMappingTool,
        ISavable<TimingCopierVm> {
        public static readonly string ToolName = "Timing Copier";
        public static readonly string ToolDescription =
            $@"Copies timing from one beatmap to one or more others.{Environment.NewLine}Choose whether to preserve beat distances, only resnap, or leave objects where they are.";

        public TimingCopierView() {
            InitializeComponent();
            DataContext = new TimingCopierVm();
            ProjectManager.LoadProject(this, message: false);
        }

        public TimingCopierVm ViewModel => (TimingCopierVm)DataContext;

        private void Start_Click(object sender, RoutedEventArgs e) => RunTool();

        internal void RunTool() {
            if (!CanRun) return;
            var targets = (ViewModel.ExportPath ?? string.Empty)
                .Split('|', StringSplitOptions.RemoveEmptyEntries);
            if (string.IsNullOrWhiteSpace(ViewModel.ImportPath) || targets.Length == 0) {
                CorePlatform.Dialogs.ShowMessage(
                    "Choose a source beatmap and at least one destination beatmap.",
                    "Missing beatmap");
                return;
            }
            foreach (var box in this.GetVisualDescendants().OfType<ValidatedTextBox>()) {
                box.Commit();
            }
            BackupManager.SaveMapBackup(targets);
            BackgroundWorker.RunWorkerAsync(ViewModel);
            CanRun = false;
        }

        protected override void BackgroundWorker_DoWork(object sender, DoWorkEventArgs e) {
            e.Result = RunProgram((TimingCopierVm)e.Argument, sender as BackgroundWorker);
        }

        internal static string RunProgram(TimingCopierVm args, BackgroundWorker worker) {
            string[] paths = (args.ExportPath ?? string.Empty)
                .Split('|', StringSplitOptions.RemoveEmptyEntries);
            int mapsDone = 0;
            var reader = CorePlatform.EditorReader.GetFullEditorReaderOrNot();

            foreach (string exportPath in paths) {
                var editorTo = CorePlatform.EditorReader.GetNewestVersionOrNot(exportPath, reader);
                var editorFrom = CorePlatform.EditorReader.GetNewestVersionOrNot(args.ImportPath, reader);
                var beatmapTo = editorTo.Beatmap;
                var timingTo = beatmapTo.BeatmapTiming;
                var timingFrom = editorFrom.Beatmap.BeatmapTiming;

                var markers = args.ResnapMode ==
                              "Number of beats between objects stays the same"
                    ? GetMarkers(beatmapTo, timingTo)
                    : new List<Marker>();

                var remove = new List<TimingPoint>();
                foreach (var redline in timingTo.Redlines) {
                    if (timingTo.GetGreenlineAtTime(redline.Offset).Offset != redline.Offset) {
                        var greenline = redline.Copy();
                        greenline.Uninherited = false;
                        greenline.MpB = -100;
                        timingTo.Add(greenline);
                    }
                    remove.Add(redline);
                }
                foreach (var point in remove) timingTo.Remove(point);

                var changes = timingFrom.Redlines.Select(point =>
                    new TimingPointsChange(point, mpb: true, meter: true,
                        unInherited: true, omitFirstBarLine: true,
                        fuzzyness: Precision.DoubleEpsilon)).ToList();
                TimingPointsChange.ApplyChanges(timingTo, changes);

                var redlines = timingFrom.Redlines;
                if (args.ResnapMode ==
                    "Number of beats between objects stays the same" && redlines.Count > 0) {
                    redlines = timingTo.Redlines;
                    var newBookmarks = new List<double>();
                    double lastTime = redlines.First().Offset;
                    foreach (var marker in markers) {
                        var redline = timingTo.GetRedlineAtTime(lastTime, redlines.First());
                        double remaining = marker.BeatsFromLastMarker;
                        while (true) {
                            var between = redlines.Where(o =>
                                o.Offset <= lastTime + redline.MpB * remaining &&
                                o.Offset > lastTime).ToList();
                            if (between.Count == 0) break;
                            var first = between.First();
                            remaining -= (first.Offset - lastTime) / redline.MpB;
                            redline = first;
                            lastTime = first.Offset;
                        }
                        marker.Time = timingTo.Resnap(lastTime + redline.MpB * remaining,
                            args.BeatDivisors, firstTp: redlines.First());
                        lastTime = marker.Time;
                    }
                    foreach (var marker in markers.Where(o => o.Object is double)) {
                        newBookmarks.Add(marker.Time);
                    }
                    beatmapTo.SetBookmarks(newBookmarks);
                } else if (args.ResnapMode == "Just resnap" && redlines.Count > 0) {
                    foreach (var hitObject in beatmapTo.HitObjects) {
                        hitObject.ResnapSelf(timingTo, args.BeatDivisors,
                            firstTp: redlines.First());
                    }
                    foreach (var greenline in timingTo.Greenlines) {
                        greenline.ResnapSelf(timingTo, args.BeatDivisors,
                            firstTp: redlines.First());
                    }
                    timingTo.Sort();
                }

                changes = beatmapTo.HitObjects.Where(o => o.IsSlider).Select(hitObject => {
                    var point = hitObject.TimingPoint.Copy();
                    point.Offset = hitObject.Time;
                    point.MpB = hitObject.SliderVelocity;
                    return new TimingPointsChange(point, mpb: true,
                        fuzzyness: Precision.DoubleEpsilon);
                }).ToList();
                TimingPointsChange.ApplyChanges(timingTo, changes);

                if ((args.ResnapMode == "Just resnap" || args.ResnapMode ==
                     "Number of beats between objects stays the same") && redlines.Count > 0) {
                    beatmapTo.GiveObjectsGreenlines();
                    beatmapTo.CalculateSliderEndTimes();
                    foreach (var hitObject in beatmapTo.HitObjects) {
                        hitObject.ResnapEnd(timingTo, args.BeatDivisors,
                            firstTp: redlines.First());
                    }
                }

                editorTo.SaveFile();
                UpdateProgressBar(worker, ++mapsDone * 100 / paths.Length);
            }

            return $"Successfully copied timing to {mapsDone} " +
                   $"{(mapsDone == 1 ? "beatmap" : "beatmaps")}.";
        }

        private static List<Marker> GetMarkers(Beatmap beatmap, Timing timing) {
            var markers = beatmap.HitObjects.Select(o => new Marker(o))
                .Concat(beatmap.GetBookmarks().Select(o => new Marker(o)))
                .Concat(timing.TimingPoints.Select(o => new Marker(o)))
                .OrderBy(o => o.Time).ToList();
            var redlines = timing.Redlines;
            if (markers.Count == 0 || redlines.Count == 0) return markers;

            double lastTime = redlines.First().Offset;
            foreach (var marker in markers) {
                var between = redlines.Where(o => o.Offset < marker.Time &&
                    o.Offset > lastTime).ToList();
                var redline = timing.GetRedlineAtTime(lastTime);
                foreach (var next in between) {
                    marker.BeatsFromLastMarker += (next.Offset - lastTime) / redline.MpB;
                    redline = next;
                    lastTime = next.Offset;
                }
                marker.BeatsFromLastMarker += (marker.Time - lastTime) / redline.MpB;
                lastTime = marker.Time;
            }
            return markers;
        }

        private sealed class Marker {
            public Marker(object value) => Object = value;
            public object Object { get; private set; }
            public double BeatsFromLastMarker { get; set; }
            public double Time {
                get => Object switch {
                    double value => value,
                    HitObject hitObject => hitObject.Time,
                    TimingPoint point => point.Offset,
                    _ => -1
                };
                set {
                    switch (Object) {
                        case double:
                            Object = value;
                            break;
                        case HitObject hitObject:
                            hitObject.Time = value;
                            break;
                        case TimingPoint point:
                            point.Offset = value;
                            break;
                    }
                }
            }
        }

        public TimingCopierVm GetSaveData() => ViewModel;
        public void SetSaveData(TimingCopierVm saveData) => DataContext = saveData;
        public string AutoSavePath => Path.Combine(CorePlatform.Paths.AppDataPath,
            "timingcopierproject.json");
        public string DefaultSaveFolder => Path.Combine(CorePlatform.Paths.AppDataPath,
            "Timing Copier Projects");
    }
}
