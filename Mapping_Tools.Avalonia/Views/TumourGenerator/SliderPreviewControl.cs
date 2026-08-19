using System;
using System.Collections.Generic;
using Avalonia;
using Avalonia.Controls;
using Avalonia.Media;
using Mapping_Tools.Classes.BeatmapHelper;
using Mapping_Tools.Classes.MathUtil;

namespace Mapping_Tools.Avalonia.Views.TumourGenerator {
    public sealed class SliderPreviewControl : Control {
        public static readonly StyledProperty<HitObject> OriginalProperty =
            AvaloniaProperty.Register<SliderPreviewControl, HitObject>(nameof(Original));
        public static readonly StyledProperty<HitObject> GeneratedProperty =
            AvaloniaProperty.Register<SliderPreviewControl, HitObject>(nameof(Generated));

        static SliderPreviewControl() =>
            AffectsRender<SliderPreviewControl>(OriginalProperty, GeneratedProperty);

        public HitObject Original {
            get => GetValue(OriginalProperty);
            set => SetValue(OriginalProperty, value);
        }
        public HitObject Generated {
            get => GetValue(GeneratedProperty);
            set => SetValue(GeneratedProperty, value);
        }

        public override void Render(DrawingContext context) {
            base.Render(context);
            context.FillRectangle(new SolidColorBrush(Color.FromArgb(25, 255, 255, 255)),
                new Rect(Bounds.Size));
            var original = Sample(Original);
            var generated = Sample(Generated);
            if (original.Count == 0 && generated.Count == 0) return;
            var all = new List<Vector2>(original);
            all.AddRange(generated);
            double minX = double.MaxValue, minY = double.MaxValue;
            double maxX = double.MinValue, maxY = double.MinValue;
            foreach (var point in all) {
                minX = Math.Min(minX, point.X);
                minY = Math.Min(minY, point.Y);
                maxX = Math.Max(maxX, point.X);
                maxY = Math.Max(maxY, point.Y);
            }
            double width = Math.Max(1, maxX - minX);
            double height = Math.Max(1, maxY - minY);
            double scale = Math.Min(Math.Max(1, Bounds.Width - 30) / width,
                Math.Max(1, Bounds.Height - 30) / height);
            Point Map(Vector2 point) => new(
                15 + (point.X - minX) * scale,
                15 + (point.Y - minY) * scale);
            DrawPath(context, original, Map, new Pen(Brushes.Gray, 3));
            DrawPath(context, generated, Map, new Pen(Brushes.DeepSkyBlue, 3));
        }

        private static List<Vector2> Sample(HitObject hitObject) {
            var result = new List<Vector2>();
            if (hitObject is not { IsSlider: true }) return result;
            var path = hitObject.GetSliderPath();
            for (int i = 0; i <= 200; i++) result.Add(path.PositionAt(i / 200d));
            return result;
        }

        private static void DrawPath(DrawingContext context, IReadOnlyList<Vector2> path,
            Func<Vector2, Point> map, Pen pen) {
            for (int i = 1; i < path.Count; i++) {
                context.DrawLine(pen, map(path[i - 1]), map(path[i]));
            }
        }
    }
}
