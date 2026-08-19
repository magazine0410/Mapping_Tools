using Mapping_Tools.Classes.HitsoundStuff;
using Mapping_Tools.Classes.SystemTools;
using Mapping_Tools.Classes;
using Mapping_Tools.Components.Domain;
using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.Linq;
using Mapping_Tools.Classes.SystemTools.Platform;
using Mapping_Tools.Classes.BeatmapHelper;
using Mapping_Tools.Classes.BeatmapHelper.Enums;
using Newtonsoft.Json;

namespace Mapping_Tools.Viewmodels {
    public class HitsoundStudioVm : BindableBase {
        private string baseBeatmap;
        public string BaseBeatmap {
            get => baseBeatmap;
            set => Set(ref baseBeatmap, value);
        }

        private Sample defaultSample;
        public Sample DefaultSample {
            get => defaultSample;
            set => Set(ref defaultSample, value);
        }

        private string exportFolder;
        public string ExportFolder {
            get => exportFolder;
            set => Set(ref exportFolder, value);
        }

        private string hitsoundDiffName;
        public string HitsoundDiffName {
            get => hitsoundDiffName;
            set => Set(ref hitsoundDiffName, value);
        }

        private bool showResults;
        public bool ShowResults {
            get => showResults;
            set => Set(ref showResults, value);
        }

        private bool exportMap;
        public bool ExportMap {
            get => exportMap;
            set => Set(ref exportMap, value);
        }

        private bool exportSamples;
        public bool ExportSamples {
            get => exportSamples;
            set => Set(ref exportSamples, value);
        }

        private bool deleteAllInExportFirst;
        public bool DeleteAllInExportFirst {
            get => deleteAllInExportFirst;
            set => Set(ref deleteAllInExportFirst, value);
        }

        private bool usePreviousSampleSchema;
        public bool UsePreviousSampleSchema {
            get => usePreviousSampleSchema;
            set => Set(ref usePreviousSampleSchema, value);
        }

        private bool allowGrowthPreviousSampleSchema;
        public bool AllowGrowthPreviousSampleSchema {
            get => allowGrowthPreviousSampleSchema;
            set => Set(ref allowGrowthPreviousSampleSchema, value);
        }

        private bool addCoincidingRegularHitsounds;
        public bool AddCoincidingRegularHitsounds {
            get => addCoincidingRegularHitsounds;
            set => Set(ref addCoincidingRegularHitsounds, value);
        }

        private bool addGreenLineVolumeToMidi;
        public bool AddGreenLineVolumeToMidi {
            get => addGreenLineVolumeToMidi;
            set => Set(ref addGreenLineVolumeToMidi, value);
        }

        public SampleSchema PreviousSampleSchema { get; set; }

        private HitsoundExportMode hitsoundExportModeSetting;
        public HitsoundExportMode HitsoundExportModeSetting {
            get => hitsoundExportModeSetting;
            set {
                if (Set(ref hitsoundExportModeSetting, value)) {
                    RaisePropertyChanged(nameof(IsStandardSettingsVisible));
                    RaisePropertyChanged(nameof(IsCoincidingSettingsVisible));
                    RaisePropertyChanged(nameof(IsStoryboardSettingsVisible));
                    RaisePropertyChanged(nameof(IsMidiSettingsVisible));
                    RaisePropertyChanged(nameof(AreSampleExportSettingsVisible));
                }
            }
        }

        [JsonIgnore]
        public bool IsStandardSettingsVisible =>
            HitsoundExportModeSetting == HitsoundExportMode.Standard;

        [JsonIgnore]
        public bool IsCoincidingSettingsVisible =>
            HitsoundExportModeSetting == HitsoundExportMode.Coinciding;

        [JsonIgnore]
        public bool IsStoryboardSettingsVisible =>
            HitsoundExportModeSetting == HitsoundExportMode.Storyboard;

        [JsonIgnore]
        public bool IsMidiSettingsVisible =>
            HitsoundExportModeSetting == HitsoundExportMode.Midi;

        [JsonIgnore]
        public bool AreSampleExportSettingsVisible =>
            HitsoundExportModeSetting != HitsoundExportMode.Midi;
        
        public IEnumerable<HitsoundExportMode> HitsoundExportModes => Enum.GetValues(typeof(HitsoundExportMode)).Cast<HitsoundExportMode>();

        private GameMode hitsoundExportGameMode;
        public GameMode HitsoundExportGameMode {
            get => hitsoundExportGameMode;
            set => Set(ref hitsoundExportGameMode, value);
        }

        [JsonIgnore]
        public IEnumerable<GameMode> HitsoundExportGameModes => Enum.GetValues(typeof(GameMode)).Cast<GameMode>();

        private double zipLayersLeniency;
        public double ZipLayersLeniency {
            get => zipLayersLeniency;
            set => Set(ref zipLayersLeniency, value);
        }

        private int firstCustomIndex;
        public int FirstCustomIndex {
            get => firstCustomIndex;
            set => Set(ref firstCustomIndex, value);
        }

        private HitsoundExporter.SampleExportFormat singleSampleExportFormat;
        public HitsoundExporter.SampleExportFormat SingleSampleExportFormat {
            get => singleSampleExportFormat;
            set {
                if (Set(ref singleSampleExportFormat, value)) {
                    RaisePropertyChanged(nameof(SingleSampleExportFormatDisplay));
                    if (value == HitsoundExporter.SampleExportFormat.MidiChords) {
                        MixedSampleExportFormat = value;
                    } else if (MixedSampleExportFormat == HitsoundExporter.SampleExportFormat.MidiChords) {
                        MixedSampleExportFormat = value;
                    }
                }
            }
        }

        private HitsoundExporter.SampleExportFormat mixedSampleExportFormat;
        public HitsoundExporter.SampleExportFormat MixedSampleExportFormat {
            get => mixedSampleExportFormat;
            set {
                if (Set(ref mixedSampleExportFormat, value)) {
                    RaisePropertyChanged(nameof(MixedSampleExportFormatDisplay));
                    if (value == HitsoundExporter.SampleExportFormat.MidiChords) {
                        SingleSampleExportFormat = value;
                    } else if (SingleSampleExportFormat == HitsoundExporter.SampleExportFormat.MidiChords) {
                        SingleSampleExportFormat = value;
                    }
                }
            }
        }

        [JsonIgnore]
        public readonly Dictionary<HitsoundExporter.SampleExportFormat, string> SampleExportFormatDisplayNameMapping = 
            new Dictionary<HitsoundExporter.SampleExportFormat, string> {{HitsoundExporter.SampleExportFormat.Default, "Default"}, 
                {HitsoundExporter.SampleExportFormat.WaveIeeeFloat, "IEEE Float (.wav)"},
                {HitsoundExporter.SampleExportFormat.WavePcm, "PCM 16-bit (.wav)"},
                {HitsoundExporter.SampleExportFormat.OggVorbis, "Vorbis (.ogg)"},
                {HitsoundExporter.SampleExportFormat.MidiChords, "Single-chord MIDI (.mid)"}
            };

        [JsonIgnore]
        public IEnumerable<string> SampleExportFormatDisplayNames => SampleExportFormatDisplayNameMapping.Values;

        public string SingleSampleExportFormatDisplay {
            get => SampleExportFormatDisplayNameMapping[SingleSampleExportFormat];
            set {
                foreach (var kvp in SampleExportFormatDisplayNameMapping.Where(kvp => kvp.Value == value)) {
                    SingleSampleExportFormat = kvp.Key;
                    break;
                }
            }
        }

        public string MixedSampleExportFormatDisplay {
            get => SampleExportFormatDisplayNameMapping[MixedSampleExportFormat];
            set {
                foreach (var kvp in SampleExportFormatDisplayNameMapping.Where(kvp => kvp.Value == value)) {
                    MixedSampleExportFormat = kvp.Key;
                    break;
                }
            }
        }

        public ObservableCollection<HitsoundLayer> HitsoundLayers { get; set; }

        private HitsoundLayer selectedLayer;
        [JsonIgnore]
        public HitsoundLayer SelectedLayer {
            get => selectedLayer;
            set => Set(ref selectedLayer, value);
        }

        [JsonIgnore] public CommandImplementation AddLayerCommand { get; }
        [JsonIgnore] public CommandImplementation RemoveLayerCommand { get; }
        [JsonIgnore] public CommandImplementation RaiseLayerCommand { get; }
        [JsonIgnore] public CommandImplementation LowerLayerCommand { get; }
        [JsonIgnore] public CommandImplementation ReloadLayerCommand { get; }
        [JsonIgnore] public CommandImplementation ImportLayersCommand { get; }
        [JsonIgnore] public CommandImplementation PreviewLayerCommand { get; }

        public string EditTimes { get; set; }

        public HitsoundStudioVm() : this("", new Sample {Priority = int.MaxValue}, new ObservableCollection<HitsoundLayer>()) { }

        public HitsoundStudioVm(string baseBeatmap, Sample defaultSample, ObservableCollection<HitsoundLayer> hitsoundLayers) {
            BaseBeatmap = baseBeatmap;
            DefaultSample = defaultSample;
            HitsoundLayers = hitsoundLayers;
            ExportFolder = CorePlatform.Paths.ExportPath;
            HitsoundDiffName = "Hitsounds";
            ShowResults = false;
            ExportMap = true;
            ExportSamples = true;
            DeleteAllInExportFirst = false;
            AddCoincidingRegularHitsounds = true;
            AddGreenLineVolumeToMidi = true;
            HitsoundExportModeSetting = HitsoundExportMode.Standard;
            HitsoundExportGameMode = GameMode.Standard;
            ZipLayersLeniency = 15;
            FirstCustomIndex = 1;
            SingleSampleExportFormat = HitsoundExporter.SampleExportFormat.Default;
            MixedSampleExportFormat = HitsoundExporter.SampleExportFormat.Default;

            AddLayerCommand = new CommandImplementation(_ => {
                var layer = new HitsoundLayer {
                    Name = $"Layer {HitsoundLayers.Count + 1}",
                    Priority = HitsoundLayers.Count
                };
                HitsoundLayers.Add(layer);
                SelectedLayer = layer;
            });
            RemoveLayerCommand = new CommandImplementation(_ => {
                if (SelectedLayer is null) return;
                int index = HitsoundLayers.IndexOf(SelectedLayer);
                HitsoundLayers.Remove(SelectedLayer);
                RecalculatePriorities();
                SelectedLayer = HitsoundLayers.Count == 0
                    ? null
                    : HitsoundLayers[Math.Max(0, Math.Min(index, HitsoundLayers.Count - 1))];
            });
            RaiseLayerCommand = new CommandImplementation(_ => MoveSelectedLayer(-1));
            LowerLayerCommand = new CommandImplementation(_ => MoveSelectedLayer(1));
            ReloadLayerCommand = new CommandImplementation(_ => {
                if (SelectedLayer?.ImportArgs is not { CanImport: true }) return;
                var imported = HitsoundImporter.ImportReloading(
                    SelectedLayer.ImportArgs.GetImportReloadingArgs());
                SelectedLayer.Reload(imported);
            });
            ImportLayersCommand = new CommandImplementation(_ => {
                if (SelectedLayer?.ImportArgs is not { CanImport: true }) return;
                var imported = HitsoundImporter.ImportReloading(
                    SelectedLayer.ImportArgs.GetImportReloadingArgs());
                int insertionIndex = HitsoundLayers.IndexOf(SelectedLayer) + 1;
                foreach (var layer in imported) {
                    HitsoundLayers.Insert(insertionIndex++, layer);
                }
                RecalculatePriorities();
                if (imported.Count > 0) SelectedLayer = imported[0];
            });
            PreviewLayerCommand = new CommandImplementation(_ => {
                try {
                    Classes.Tools.HitsoundStudioRunner.Preview(SelectedLayer);
                } catch (Exception ex) {
                    CorePlatform.Dialogs.ShowMessage(ex.Message, "Could not preview sample");
                }
            });
        }

        private void MoveSelectedLayer(int delta) {
            if (SelectedLayer is null) return;
            int oldIndex = HitsoundLayers.IndexOf(SelectedLayer);
            int newIndex = Math.Clamp(oldIndex + delta, 0, HitsoundLayers.Count - 1);
            if (newIndex == oldIndex) return;
            HitsoundLayers.Move(oldIndex, newIndex);
            RecalculatePriorities();
        }

        private void RecalculatePriorities() {
            for (int i = 0; i < HitsoundLayers.Count; i++) HitsoundLayers[i].Priority = i;
        }

        public enum HitsoundExportMode {
            Standard,
            Coinciding,
            Storyboard,
            Midi,
        }
    }
}
