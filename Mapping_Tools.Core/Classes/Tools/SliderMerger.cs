using System;
using System.Collections.Generic;
using System.ComponentModel;
using System.Linq;
using Mapping_Tools.Classes.BeatmapHelper;
using Mapping_Tools.Classes.BeatmapHelper.Enums;
using Mapping_Tools.Classes.MathUtil;
using Mapping_Tools.Classes.SystemTools.Platform;
using Mapping_Tools.Classes.ToolHelpers.Sliders;
using Mapping_Tools.Viewmodels;

namespace Mapping_Tools.Classes.Tools {
    public static class SliderMerger {
        public static string Merge(SliderMergerVm args, BackgroundWorker worker = null) {
            int slidersMerged = 0;
            object reader = CorePlatform.EditorReader.GetFullEditorReaderOrNot();
            if (args.ImportModeSetting == SliderMergerVm.ImportMode.Selected &&
                !CorePlatform.EditorReader.IsAvailable) {
                throw new InvalidOperationException(
                    "Selected-object mode requires the editor reader. Choose bookmarked, time, or everything on Linux.");
            }

            foreach (string path in args.Paths ?? Array.Empty<string>()) {
                var editor = CorePlatform.EditorReader.GetNewestVersionOrNot(path, reader,
                    out var selected, out var readerError);
                if (args.ImportModeSetting == SliderMergerVm.ImportMode.Selected &&
                    readerError is not null) {
                    throw new InvalidOperationException("Could not fetch selected hit objects.",
                        readerError);
                }
                var beatmap = editor.Beatmap;
                var markedObjects = args.ImportModeSetting switch {
                    SliderMergerVm.ImportMode.Selected => selected,
                    SliderMergerVm.ImportMode.Bookmarked => beatmap.GetBookmarkedObjects(),
                    SliderMergerVm.ImportMode.Time => beatmap.QueryTimeCode(args.TimeCode).ToList(),
                    SliderMergerVm.ImportMode.Everything => new List<HitObject>(beatmap.HitObjects),
                    _ => throw new ArgumentOutOfRangeException()
                };

                bool mergeLast = false;
                for (int i = 0; i < markedObjects.Count - 1; i++) {
                    if (worker is { WorkerReportsProgress: true }) {
                        worker.ReportProgress(markedObjects.Count == 0 ? 100 :
                            i * 100 / markedObjects.Count);
                    }
                    var first = markedObjects[i];
                    var second = markedObjects[i + 1];
                    var firstEnd = first.IsSlider
                        ? args.MergeOnSliderEnd
                            ? first.GetSliderPath().PositionAt(1)
                            : first.CurvePoints.Last()
                        : first.Pos;
                    double distance = Vector2.Distance(firstEnd, second.Pos);
                    if (distance > args.Leniency ||
                        !(first.IsSlider || first.IsCircle) ||
                        !(second.IsSlider || second.IsCircle)) {
                        mergeLast = false;
                        continue;
                    }

                    if (first.IsSlider && second.IsSlider) {
                        if (args.MergeOnSliderEnd) {
                            first.SetAllCurvePoints(SliderPathUtil.MoveAnchorsToLength(
                                first.GetAllCurvePoints(), first.SliderType, first.PixelLength,
                                out var pathType));
                            first.SliderType = pathType;
                        }
                        var firstAnchors = BezierConverter.ConvertToBezierAnchors(
                            first.GetAllCurvePoints(), first.SliderType);
                        var secondAnchors = BezierConverter.ConvertToBezierAnchors(
                            second.GetAllCurvePoints(), second.SliderType);
                        double extraLength = 0;
                        if (args.ConnectionModeSetting == SliderMergerVm.ConnectionMode.Move) {
                            Move(secondAnchors, firstAnchors.Last() - secondAnchors.First());
                        } else {
                            firstAnchors.Add(firstAnchors.Last());
                            firstAnchors.Add(secondAnchors.First());
                            extraLength = (first.CurvePoints.Last() - second.Pos).Length;
                        }
                        var merged = firstAnchors.Concat(secondAnchors).ToList();
                        merged.Round();
                        bool linear = args.LinearOnLinear && IsLinearBezier(firstAnchors) &&
                                      IsLinearBezier(secondAnchors);
                        if (linear) RemoveDuplicateAnchors(merged);
                        first.SetAllCurvePoints(merged);
                        first.SliderType = linear ? PathType.Linear : PathType.Bezier;
                        first.PixelLength += second.PixelLength + extraLength;
                        Remove(second, beatmap.HitObjects, markedObjects);
                        i--;
                    } else if (first.IsSlider && second.IsCircle) {
                        if (Precision.DefinitelyBigger(distance, 0)) {
                            var anchors = BezierConverter.ConvertToBezierAnchors(
                                first.GetAllCurvePoints(), first.SliderType);
                            anchors.Add(anchors.Last());
                            anchors.Add(second.Pos);
                            first.PixelLength += (first.CurvePoints.Last() - second.Pos).Length;
                            anchors.Round();
                            bool linear = args.LinearOnLinear && IsLinearBezier(anchors);
                            if (linear) RemoveDuplicateAnchors(anchors);
                            first.SetAllCurvePoints(anchors);
                            first.SliderType = linear ? PathType.Linear : PathType.Bezier;
                        }
                        Remove(second, beatmap.HitObjects, markedObjects);
                        i--;
                    } else if (first.IsCircle && second.IsSlider) {
                        if (Precision.DefinitelyBigger(distance, 0)) {
                            var anchors = BezierConverter.ConvertToBezierAnchors(
                                second.GetAllCurvePoints(), second.SliderType);
                            anchors.Insert(0, anchors.First());
                            anchors.Insert(0, first.Pos);
                            second.PixelLength += (first.Pos - second.Pos).Length;
                            anchors.Round();
                            bool linear = args.LinearOnLinear && IsLinearBezier(anchors);
                            if (linear) RemoveDuplicateAnchors(anchors);
                            second.SetAllCurvePoints(anchors);
                            second.SliderType = linear ? PathType.Linear : PathType.Bezier;
                        }
                        Remove(first, beatmap.HitObjects, markedObjects);
                        i--;
                    } else {
                        if (Precision.DefinitelyBigger(distance, 0)) {
                            first.SetAllCurvePoints(new List<Vector2> { first.Pos, second.Pos });
                            first.SliderType = args.LinearOnLinear
                                ? PathType.Linear
                                : PathType.Bezier;
                            first.PixelLength = (first.Pos - second.Pos).Length;
                            first.IsCircle = false;
                            first.IsSlider = true;
                            first.Repeat = 1;
                            first.EdgeHitsounds = new List<int> {
                                first.GetHitsounds(), second.GetHitsounds()
                            };
                            first.EdgeSampleSets = new List<SampleSet> {
                                first.SampleSet, second.SampleSet
                            };
                            first.EdgeAdditionSets = new List<SampleSet> {
                                first.AdditionSet, second.AdditionSet
                            };
                        }
                        Remove(second, beatmap.HitObjects, markedObjects);
                        i--;
                    }

                    slidersMerged++;
                    if (!mergeLast) slidersMerged++;
                    mergeLast = true;

                    if (args.Leniency == 727) {
                        first.SetAllCurvePoints(MakeShape(first.GetAllCurvePoints(),
                            first.PixelLength));
                        first.PixelLength *= 2;
                        first.SliderType = PathType.Bezier;
                    }
                }
                editor.SaveFile();
            }
            if (worker is { WorkerReportsProgress: true }) worker.ReportProgress(100);
            return $"Successfully merged {slidersMerged} " +
                   $"{(slidersMerged == 1 ? "slider" : "sliders")}!";
        }

        private static void Remove(HitObject hitObject,
            ICollection<HitObject> all,
            ICollection<HitObject> marked) {
            all.Remove(hitObject);
            marked.Remove(hitObject);
        }

        /// <summary>
        /// The shape that a leniency of exactly 727 makes. WYSI.
        /// </summary>
        private static List<Vector2> MakeShape(List<Vector2> points, double sliderLength) {
            var newPoints = new List<Vector2> {
                new(0, 0),
                new(40, -40),
                new(0, -70),
                new(-40, -40),
                new(0, 0),
                new(0, 0),
                new(96, 24),
                new(168, 0),
                new(168, 0),
                new(96, -24),
                new(0, 0),
                new(0, 0),
                new(-40, 40),
                new(0, 70),
                new(40, 40),
                new(0, 0)
            };

            double sizeMultiplier = sliderLength / 591 * 2;  // 591 is the size of the shape
            double normalAngle = -(points.Last() - points.First()).Theta;
            var mat = Matrix2.CreateRotation(normalAngle);
            mat *= sizeMultiplier;

            for (int i = 0; i < newPoints.Count; i++) {
                newPoints[i] = points.First() + Matrix2.Mult(mat, newPoints[i]);
            }

            return newPoints;
        }

        private static void RemoveDuplicateAnchors(List<Vector2> points) {
            for (int i = 0; i < points.Count - 1; i++) {
                if (points[i] != points[i + 1]) continue;
                points.RemoveAt(i--);
            }
        }

        public static bool IsLinearBezier(IReadOnlyList<Vector2> points) {
            for (int i = 1; i < points.Count - 1; i++) {
                if (points[i] != points[i - 1] && points[i] != points[i + 1]) return false;
            }
            return true;
        }

        public static void Move(IList<Vector2> points, Vector2 delta) {
            for (int i = 0; i < points.Count; i++) points[i] += delta;
        }
    }
}
