using System;
using System.Collections.Generic;
using System.Drawing;
using System.IO;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using Mapping_Tools.Classes;
using Mapping_Tools.Classes.BeatmapHelper;
using Mapping_Tools.Classes.SystemTools;
using Mapping_Tools.Classes.SystemTools.Platform;
using Mapping_Tools.Classes.Tools.SlideratorStuff;
using Mapping_Tools.Components.Domain;
using Newtonsoft.Json;
using SkiaSharp;

namespace Mapping_Tools.Viewmodels {
    public class SliderPicturatorVm : BindableBase {
        private long viewportSize = 32768;
        private int quality = 1;
        private long segmentCount;
        private double yResolution = 1080;
        private double sliderStartX = 256;
        private double sliderStartY = 192;
        private double imageStartX;
        private double imageStartY;
        private bool useMapComboColors;
        private Color currentTrackColor = Color.White;
        private Color comboColor = Color.White;
        private Color trackColorPickerColor = Color.White;
        private Color borderColor = Color.White;
        private double timeCode;
        private double duration = 1;
        private string pictureFile;
        private bool blackOn = true;
        private bool borderOn = true;
        private bool redOn = true;
        private bool greenOn = true;
        private bool blueOn = true;
        private bool alphaOn = true;
        private bool setBeatmapColors = true;
        private HitObject selectedSlider;
        private byte[] previewPng;
        private bool isProcessingPreview;
        private string previewError = string.Empty;
        private int previewVersion;

        public SliderPicturatorVm() {
            UploadFileCommand = new CommandImplementation(_ => BrowseImageRequested?.Invoke());
            ImportCommand = new CommandImplementation(_ => ImportSelectedSlider());
            RemoveCommand = new CommandImplementation(_ => SelectedSlider = null);
            CorePlatform.FileDialogs.CurrentBeatmapsChanged += (_, _) =>
                RaisePropertyChanged(nameof(AvailableColors));
        }

        [JsonIgnore] public string Path { get; set; }
        [JsonIgnore] public bool Quick { get; set; }
        [JsonIgnore] public Action BrowseImageRequested { get; set; }
        [JsonIgnore] public IEnumerable<long> ViewportSizes => new long[] { 16384, 32768 };
        [JsonIgnore] public bool IsSliderImportAvailable => CorePlatform.EditorReader.IsAvailable;

        public long ViewportSize { get => viewportSize; set => Set(ref viewportSize, value); }
        public int Quality {
            get => quality;
            set {
                int normalized = Math.Clamp(value, 1, 101);
                if (Set(ref quality, normalized)) RegeneratePreview();
            }
        }
        public long SegmentCount { get => segmentCount; private set => Set(ref segmentCount, value); }
        public double YResolution { get => yResolution; set => Set(ref yResolution, value); }
        public double SliderStartX { get => sliderStartX; set => Set(ref sliderStartX, value); }
        public double SliderStartY { get => sliderStartY; set => Set(ref sliderStartY, value); }
        public double ImageStartX { get => imageStartX; set => Set(ref imageStartX, value); }
        public double ImageStartY { get => imageStartY; set => Set(ref imageStartY, value); }

        [JsonIgnore]
        public IEnumerable<Color> AvailableColors {
            get {
                try {
                    string path = CorePlatform.FileDialogs.GetCurrentBeatmap();
                    if (string.IsNullOrWhiteSpace(path) || !File.Exists(path)) {
                        return Enumerable.Empty<Color>();
                    }
                    var beatmap = new BeatmapEditor(path).Beatmap;
                    var colours = beatmap.ComboColours.Count == 0
                        ? ComboColour.GetDefaultComboColours().ToList()
                        : beatmap.ComboColours;
                    IEnumerable<Color> result = colours.Select(o =>
                        Color.FromArgb(o.Color.R, o.Color.G, o.Color.B));
                    if (beatmap.SpecialColours.TryGetValue("SliderTrackOverride", out var track)) {
                        result = result.Append(Color.FromArgb(
                            track.Color.R, track.Color.G, track.Color.B));
                    }
                    return result.ToArray();
                } catch {
                    return Enumerable.Empty<Color>();
                }
            }
        }

        public bool UseMapComboColors {
            get => useMapComboColors;
            set {
                if (!Set(ref useMapComboColors, value)) return;
                CurrentTrackColor = value ? ComboColor : TrackColorPickerColor;
                RaisePropertyChanged(nameof(ShouldShowCcPicker));
                RaisePropertyChanged(nameof(ShouldShowPalette));
            }
        }
        public Color CurrentTrackColor {
            get => currentTrackColor;
            set { if (Set(ref currentTrackColor, value)) RegeneratePreview(); }
        }
        public Color ComboColor {
            get => comboColor;
            set {
                if (!Set(ref comboColor, value) || !UseMapComboColors) return;
                CurrentTrackColor = value;
                RaisePropertyChanged(nameof(PickedComboColor));
            }
        }
        [JsonIgnore] public string PickedComboColor =>
            $"#{ComboColor.R:X2}{ComboColor.G:X2}{ComboColor.B:X2}";
        public Color TrackColorPickerColor {
            get => trackColorPickerColor;
            set {
                if (!Set(ref trackColorPickerColor, value) || UseMapComboColors) return;
                CurrentTrackColor = value;
            }
        }
        public Color BorderColor {
            get => borderColor;
            set { if (Set(ref borderColor, value)) RegeneratePreview(); }
        }
        [JsonIgnore] public bool ShouldShowCcPicker => UseMapComboColors;
        [JsonIgnore] public bool ShouldShowPalette => !UseMapComboColors;

        public double TimeCode { get => timeCode; set => Set(ref timeCode, value); }
        public double Duration { get => duration; set => Set(ref duration, value); }
        public string PictureFile {
            get => pictureFile;
            set { if (Set(ref pictureFile, value)) RegeneratePreview(); }
        }
        public bool BlackOn { get => blackOn; set { if (Set(ref blackOn, value)) RegeneratePreview(); } }
        public bool BorderOn { get => borderOn; set { if (Set(ref borderOn, value)) RegeneratePreview(); } }
        public bool RedOn { get => redOn; set { if (Set(ref redOn, value)) RegeneratePreview(); } }
        public bool GreenOn { get => greenOn; set { if (Set(ref greenOn, value)) RegeneratePreview(); } }
        public bool BlueOn { get => blueOn; set { if (Set(ref blueOn, value)) RegeneratePreview(); } }
        public bool AlphaOn { get => alphaOn; set { if (Set(ref alphaOn, value)) RegeneratePreview(); } }
        public bool SetBeatmapColors { get => setBeatmapColors; set => Set(ref setBeatmapColors, value); }
        [JsonIgnore]
        public HitObject SelectedSlider {
            get => selectedSlider;
            set { if (Set(ref selectedSlider, value)) RegeneratePreview(); }
        }

        [JsonIgnore]
        public byte[] PreviewPng {
            get => previewPng;
            private set {
                if (Set(ref previewPng, value)) RaisePropertyChanged(nameof(ShouldShowPreviewPrompt));
            }
        }
        [JsonIgnore]
        public bool IsProcessingPreview {
            get => isProcessingPreview;
            private set {
                if (Set(ref isProcessingPreview, value)) RaisePropertyChanged(nameof(ShouldShowPreviewPrompt));
            }
        }
        [JsonIgnore]
        public string PreviewError {
            get => previewError;
            private set {
                if (Set(ref previewError, value)) RaisePropertyChanged(nameof(ShouldShowPreviewPrompt));
            }
        }
        [JsonIgnore] public bool ShouldShowPreviewPrompt => !IsProcessingPreview &&
            (PreviewPng is null || PreviewPng.Length == 0) &&
            string.IsNullOrWhiteSpace(PreviewError);
        [JsonIgnore] public Task PreviewTask { get; private set; } = Task.CompletedTask;
        [JsonIgnore] public CommandImplementation UploadFileCommand { get; }
        [JsonIgnore] public CommandImplementation ImportCommand { get; }
        [JsonIgnore] public CommandImplementation RemoveCommand { get; }

        public void RegeneratePreview() {
            int version = Interlocked.Increment(ref previewVersion);
            string file = PictureFile;
            if (string.IsNullOrWhiteSpace(file) || !File.Exists(file)) {
                PreviewPng = null;
                SegmentCount = 0;
                PreviewError = string.Empty;
                IsProcessingPreview = false;
                PreviewTask = Task.CompletedTask;
                return;
            }

            IsProcessingPreview = true;
            PreviewError = string.Empty;
            Color track = CurrentTrackColor;
            Color border = BorderColor;
            HitObject slider = SelectedSlider?.DeepCopy();
            bool black = BlackOn;
            bool borderEnabled = BorderOn;
            bool alpha = AlphaOn;
            bool red = RedOn;
            bool green = GreenOn;
            bool blue = BlueOn;
            int imageQuality = Quality;

            PreviewTask = Task.Run(() => {
                using var image = SKBitmap.Decode(file) ??
                    throw new InvalidDataException("The selected file is not a supported image.");
                var result = SliderPicturator.Recolor(image, track, border, Color.Black,
                    slider, !black, !borderEnabled, !alpha, red, green, blue, imageQuality);
                using var recoloured = result.Item1;
                return (Png: SliderPicturator.EncodePng(recoloured), Count: result.Item2);
            }).ContinueWith(task => {
                if (version != Volatile.Read(ref previewVersion)) return;
                if (task.IsCompletedSuccessfully) {
                    PreviewPng = task.Result.Png;
                    SegmentCount = task.Result.Count;
                } else if (task.Exception is not null) {
                    PreviewPng = null;
                    SegmentCount = 0;
                    PreviewError = task.Exception.GetBaseException().Message;
                }
                IsProcessingPreview = false;
            }, TaskScheduler.Default);
        }

        private void ImportSelectedSlider() {
            if (!CorePlatform.EditorReader.IsAvailable) {
                CorePlatform.Dialogs.ShowMessage(
                    "Importing a selected slider requires editor-reader support. You can use Slider Picturator without it.",
                    "Editor reader unavailable");
                return;
            }
            try {
                string path = CorePlatform.FileDialogs.GetCurrentBeatmap();
                if (string.IsNullOrWhiteSpace(path)) return;
                CorePlatform.EditorReader.GetNewestVersionOrNot(path, null,
                    out var selected, out var exception);
                if (exception is not null) throw exception;
                SelectedSlider = selected?.FirstOrDefault(o => o.IsSlider);
            } catch (Exception ex) {
                CorePlatform.Dialogs.ShowMessage(ex.Message, "Could not import slider");
            }
        }
    }
}
