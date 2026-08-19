using Mapping_Tools.Classes.MathUtil;
using System;
using System.Collections.Generic;
using System.ComponentModel;
using System.Globalization;
using System.Linq;
using Newtonsoft.Json;
using Mapping_Tools.Classes.BeatmapHelper;
using Mapping_Tools.Components.Domain;
using Mapping_Tools.Classes.BeatmapHelper.Enums;
using Mapping_Tools.Classes.SystemTools;

namespace Mapping_Tools.Classes.HitsoundStuff {
    /// <summary>
    /// Represents a single hitsound and every time that hitsound has to be played.
    /// It is also directly connected to the source of the data.
    /// </summary>
    public class HitsoundLayer : BindableBase {
        private string name;
        /// <summary>
        /// The name of this hitsound layer. This is only for the convenience of the user and not an unique identifier.
        /// </summary>
        public string Name {
            get => name;
            set => Set(ref name, value);
        }

        private SampleSet sampleSet;
        /// <summary>
        /// The sample set that this sample should play on.
        /// </summary>
        public SampleSet SampleSet {
            get => sampleSet;
            set {
                if (Set(ref sampleSet, value)) {
                    RaisePropertyChanged(nameof(SampleSetString));
                }
            }
        }

        private Hitsound hitsound;
        /// <summary>
        /// The hitsound that this sample should play on.
        /// </summary>
        public Hitsound Hitsound {
            get => hitsound;
            set {
                if (Set(ref hitsound, value)) {
                    RaisePropertyChanged(nameof(HitsoundString));
                }
            }
        }

        private int priority;
        /// <summary>
        /// The priority of this hitsound layer. When mixing multiple <see cref="Sample"/>,
        /// the sampleset of the one with the lowest priority will be taken.
        /// This priority value is equal to the index in the hitsound layers list.
        /// </summary>
        public int Priority {
            get => priority;
            set => Set(ref priority, value);
        }

        private LayerImportArgs importArgs;
        /// <summary>
        /// Contains all the information about how this hitsound layer was generated, so it can be reloaded.
        /// </summary>
        public LayerImportArgs ImportArgs {
            get => importArgs;
            set => Set(ref importArgs, value);
        }

        private SampleGeneratingArgs sampleArgs;
        /// <summary>
        /// Contains all the information about how the sound of this hitsound should be generated.
        /// </summary>
        public SampleGeneratingArgs SampleArgs {
            get => sampleArgs;
            set => Set(ref sampleArgs, value);
        }

        private List<double> times;
        /// <summary>
        /// Contains all the times that this hitsound should play.
        /// This list is usually sorted.
        /// </summary>
        public List<double> Times {
            get => times;
            set {
                if (Set(ref times, value)) RaisePropertyChanged(nameof(TimesText));
            }
        }

        /// <summary>
        /// The times of this layer, as text the user can edit.
        /// </summary>
        /// <remarks>
        /// A value that does not parse is refused with a short reason. It must not be
        /// a plain <see cref="FormatException"/>: the binding shows the whole exception,
        /// stack trace and all, under the text box.
        /// </remarks>
        [JsonIgnore]
        public string TimesText {
            get => string.Join(", ", Times.Select(o => o.ToString(CultureInfo.InvariantCulture)));
            set {
                var parts = (value ?? string.Empty)
                    .Split(new[] { ',', ';', ' ', '\r', '\n', '\t' },
                        StringSplitOptions.RemoveEmptyEntries);
                var parsed = new List<double>(parts.Length);
                foreach (var part in parts) {
                    if (!double.TryParse(part, NumberStyles.Float, CultureInfo.InvariantCulture,
                            out double time)) {
                        throw new ValidationMessage($"\"{part}\" is not a number.");
                    }
                    parsed.Add(time);
                }
                parsed.Sort();
                Times = parsed;
            }
        }

        [JsonIgnore]
        public IEnumerable<SampleSet> SampleSets =>
            Enum.GetValues(typeof(SampleSet)) as SampleSet[];

        [JsonIgnore]
        public IEnumerable<Hitsound> Hitsounds =>
            Enum.GetValues(typeof(Hitsound)) as Hitsound[];

        [JsonIgnore]
        public IEnumerable<ImportType> ImportTypes =>
            Enum.GetValues(typeof(ImportType)) as ImportType[];

        /// <summary>
        /// Convenience field for binding with the sampleset combo box.
        /// </summary>
        public string SampleSetString { get => GetSampleSetString(); set => SetSampleSetString(value); }

        private void SetSampleSetString(string value) {
            SampleSet =  (SampleSet)Enum.Parse(typeof(SampleSet), value);
        }

        private string GetSampleSetString() {
            return SampleSet.ToString();
        }

        /// <summary>
        /// Convenience field for binding with the hitsound combo box.
        /// </summary>
        public string HitsoundString { get => GetHitsoundString(); set => SetHitsoundString(value); }

        private void SetHitsoundString(string value) {
            Hitsound = (Hitsound)Enum.Parse(typeof(Hitsound), value);
        }

        private string GetHitsoundString() {
            return Hitsound.ToString();
        }

        /// <inheritdoc />
        public HitsoundLayer() : this(string.Empty, SampleSet.Normal, Hitsound.Normal, int.MaxValue,
            new LayerImportArgs(), new SampleGeneratingArgs(), new List<double>()) {}

        /// <inheritdoc />
        public HitsoundLayer(string name, ImportType importType, SampleSet sampleSet, Hitsound hitsound, string samplePath) :
        this(name, sampleSet, hitsound, int.MaxValue, new LayerImportArgs(importType), new SampleGeneratingArgs(samplePath), new List<double>()) {}

        /// <inheritdoc />
        public HitsoundLayer(string name, SampleSet sampleSet, Hitsound hitsound, SampleGeneratingArgs sampleArgs, LayerImportArgs importArgs) :
        this(name, sampleSet, hitsound, int.MaxValue, importArgs, sampleArgs, new List<double>()) {}

        /// <inheritdoc />
        public HitsoundLayer(string name, SampleSet sampleSet, Hitsound hitsound, int priority, LayerImportArgs importArgs, SampleGeneratingArgs sampleArgs, List<double> times) {
            Name = name;
            SampleSet = sampleSet;
            Hitsound = hitsound;
            Priority = priority;
            ImportArgs = importArgs;
            SampleArgs = sampleArgs;
            Times = times;
        }

        /// <summary>
        /// Reloads this hitsound layer with times from a list of hitsound layers that could be relevant to this one.
        /// </summary>
        /// <param name="layers"></param>
        public void Reload(List<HitsoundLayer> layers) {
            List<HitsoundLayer> sameLayer = layers.FindAll(o => ImportArgs.ReloadCompatible(o.ImportArgs));

            Times.Clear();
            foreach (HitsoundLayer hsl in sameLayer) {
                Times.AddRange(hsl.Times);
            }
            Times.Sort();

            RaisePropertyChanged(nameof(Times));
        }

        /// <summary>
        /// Removes duplicate values from the <see cref="Times"/> list.
        /// </summary>
        public void RemoveDuplicates() {
            if (Times.Count < 2) return;

            for (int i = 1; i < Times.Count; i++) {
                if (Math.Abs(Times[i] - Times[i - 1]) < Precision.DoubleEpsilon) {
                    Times.RemoveAt(i);
                    i--;
                }    
            }
        }
    }
}
