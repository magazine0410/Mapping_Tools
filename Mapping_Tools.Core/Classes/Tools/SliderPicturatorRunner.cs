using System;
using System.Collections.Generic;
using System.ComponentModel;
using System.Drawing;
using System.IO;
using System.Linq;
using Mapping_Tools.Classes.BeatmapHelper;
using Mapping_Tools.Classes.BeatmapHelper.Enums;
using Mapping_Tools.Classes.MathUtil;
using Mapping_Tools.Classes.SystemTools.Platform;
using Mapping_Tools.Classes.ToolHelpers;
using Mapping_Tools.Classes.Tools.SlideratorStuff;
using Mapping_Tools.Viewmodels;
using SkiaSharp;

namespace Mapping_Tools.Classes.Tools {
    public static class SliderPicturatorRunner {
        public static string Picturate(SliderPicturatorVm args,
            BackgroundWorker worker = null) {
            if (args is null) throw new ArgumentNullException(nameof(args));
            if (string.IsNullOrWhiteSpace(args.PictureFile) ||
                !File.Exists(args.PictureFile)) {
                throw new InvalidOperationException("Select an image first.");
            }
            if (string.IsNullOrWhiteSpace(args.Path) || !File.Exists(args.Path)) {
                throw new InvalidOperationException("Open a destination beatmap first.");
            }

            using var image = SKBitmap.Decode(args.PictureFile) ??
                throw new InvalidDataException("The selected file is not a supported image.");
            object reader = CorePlatform.EditorReader.GetFullEditorReaderOrNot();
            var editor = CorePlatform.EditorReader.GetNewestVersionOrNot(args.Path, reader);
            var beatmap = editor.Beatmap;
            double duration = args.SelectedSlider?.TemporalLength ?? args.Duration;
            if (duration <= 0) {
                throw new InvalidOperationException("Duration must be greater than zero.");
            }

            double circleSize = beatmap.Difficulty["CircleSize"].DoubleValue;
            Color sliderColor = args.CurrentTrackColor;
            Color borderColor = args.BorderColor;
            var (sliderPath, frameDistance) = SliderPicturator.Picturate(image,
                sliderColor, borderColor, Color.Black, circleSize,
                new Vector2(args.SliderStartX, args.SliderStartY),
                new Vector2(args.ImageStartX, args.ImageStartY), args.SelectedSlider,
                args.YResolution, args.ViewportSize, !args.BlackOn, !args.BorderOn,
                !args.AlphaOn, args.RedOn, args.GreenOn, args.BlueOn, args.Quality);
            if (sliderPath.Count < 2) {
                throw new InvalidOperationException(
                    "The image did not produce enough points for a slider.");
            }

            double startTime = args.TimeCode;
            int currentColourIndex = 0;
            int previousIndex = beatmap.HitObjects.Select(o => o.Time).ToList()
                .BinarySearch(startTime);
            if (previousIndex < 0) previousIndex = ~previousIndex - 1;
            if (previousIndex >= 0) {
                currentColourIndex = beatmap.HitObjects[previousIndex].ColourIndex;
            }

            int foundColourIndex = beatmap.ComboColours.FindIndex(o =>
                o.Color.R == sliderColor.R && o.Color.G == sliderColor.G &&
                o.Color.B == sliderColor.B);
            if (foundColourIndex < 0) foundColourIndex = 0;

            var hitObject = new HitObject(startTime, 0, SampleSet.None, SampleSet.None) {
                IsCircle = false,
                IsSpinner = false,
                IsHoldNote = false,
                IsSlider = true,
                ComboSkip = foundColourIndex - currentColourIndex - 1
            };
            hitObject.SetAllCurvePoints(sliderPath);
            hitObject.SliderType = PathType.Linear;
            hitObject.PixelLength = OsuStableDistance(sliderPath);
            beatmap.HitObjects.Add(hitObject);
            beatmap.SortHitObjects();

            var timing = beatmap.BeatmapTiming;
            var after = timing.GetRedlineAtTime(hitObject.Time).Copy();
            var onSlider = after.Copy();
            after.Offset = hitObject.Time;
            onSlider.Offset = hitObject.Time - 1;
            after.OmitFirstBarLine = true;
            onSlider.OmitFirstBarLine = true;
            onSlider.MpB = frameDistance == 0
                ? 100 * timing.SliderMultiplier * duration / hitObject.PixelLength
                : 100 * timing.SliderMultiplier / frameDistance;
            hitObject.SliderVelocity = double.NaN;

            var changes = new List<TimingPointsChange> {
                new(onSlider, mpb: true, unInherited: true,
                    omitFirstBarLine: true, fuzzyness: Precision.DoubleEpsilon),
                new(after, mpb: true, unInherited: true,
                    omitFirstBarLine: true, fuzzyness: Precision.DoubleEpsilon)
            };
            hitObject.Time -= 1;
            changes.AddRange(beatmap.HitObjects.Select(o => {
                double velocity = o == hitObject
                    ? o.SliderVelocity
                    : timing.GetSvAtTime(o.Time);
                var point = timing.GetTimingPointAtTime(o.Time).Copy();
                point.MpB = velocity;
                point.Offset = o.Time;
                return new TimingPointsChange(point, mpb: true,
                    fuzzyness: Precision.DoubleEpsilon);
            }));
            TimingPointsChange.ApplyChanges(timing, changes);

            if (args.SetBeatmapColors) {
                if (!args.UseMapComboColors) {
                    beatmap.SpecialColours["SliderTrackOverride"] =
                        new ComboColour(sliderColor.R, sliderColor.G, sliderColor.B);
                }
                beatmap.SpecialColours["SliderBorder"] =
                    new ComboColour(borderColor.R, borderColor.G, borderColor.B);
            }

            editor.SaveFile();
            if (worker is { WorkerReportsProgress: true }) worker.ReportProgress(100);
            return "Created the slider picture.";
        }

        private static double OsuStableDistance(IReadOnlyList<Vector2> points) {
            double length = 0;
            for (int i = 1; i < points.Count; i++) {
                float x = (float)Math.Round(points[i - 1].X) -
                          (float)Math.Round(points[i].X);
                float y = (float)Math.Round(points[i - 1].Y) -
                          (float)Math.Round(points[i].Y);
                length += (float)Math.Sqrt(x * x + y * y);
            }
            return length;
        }
    }
}
