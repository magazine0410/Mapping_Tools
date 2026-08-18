using Mapping_Tools.Classes.BeatmapHelper;
using Mapping_Tools.Components.Graph;
using MediaColor = System.Windows.Media.Color;
using DrawingColor = System.Drawing.Color;

namespace Mapping_Tools.Classes.ToolHelpers {
    /// <summary>
    /// Joins the portable core to the parts that only Windows or WPF can supply.
    /// These members were in the core before the split.
    /// </summary>
    public static class WpfCoreBridge {
        /// <summary>
        /// Makes a Timing Point from a control point of the editor reader.
        /// </summary>
        public static TimingPoint ToTimingPoint(this Editor_Reader.ControlPoint cp) {
            return new TimingPoint {
                MpB = cp.BeatLength,
                Offset = cp.Offset,
                SampleIndex = cp.CustomSamples,
                SampleSet = (BeatmapHelper.Enums.SampleSet) cp.SampleSet,
                Meter = new BeatmapHelper.TempoSignature(cp.TimeSignature),
                Volume = cp.Volume,
                Kiai = (cp.EffectFlags & 1) > 0,
                OmitFirstBarLine = (cp.EffectFlags & 8) > 0,
                Uninherited = cp.TimingChange
            };
        }

        /// <summary>Converts a WPF colour to the colour type of the core.</summary>
        public static DrawingColor ToDrawingColor(this MediaColor c) {
            return DrawingColor.FromArgb(c.A, c.R, c.G, c.B);
        }

        /// <summary>Converts a colour of the core to a WPF colour.</summary>
        public static MediaColor ToMediaColor(this DrawingColor c) {
            return MediaColor.FromArgb(c.A, c.R, c.G, c.B);
        }

        /// <summary>Makes a graph anchor control from the anchor data.</summary>
        public static Anchor GetAnchor(this AnchorState state) {
            return new Anchor(null, state.Pos, state.Interpolator) { Tension = state.Tension };
        }
    }
}
