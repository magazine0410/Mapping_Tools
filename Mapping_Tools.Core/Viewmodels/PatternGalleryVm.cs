using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.Collections.Specialized;
using System.IO;
using System.Linq;
using Mapping_Tools.Classes.BeatmapHelper;
using Mapping_Tools.Classes.BeatmapHelper.BeatDivisors;
using Mapping_Tools.Classes.BeatmapHelper.Enums;
using Mapping_Tools.Classes.MathUtil;
using Mapping_Tools.Classes.SystemTools;
using Mapping_Tools.Classes.SystemTools.Platform;
using Mapping_Tools.Classes.Tools.PatternGallery;
using Mapping_Tools.Components.Domain;
using Newtonsoft.Json;

namespace Mapping_Tools.Viewmodels {
    public class PatternGalleryVm : BindableBase {
        private string collectionName = "My Pattern Collection";
        private ObservableCollection<OsuPattern> patterns;
        private string searchFilter = string.Empty;
        private string sortProperty = "Creation time";
        private int sortDirection;
        private ExportTimeMode exportTimeMode = ExportTimeMode.Pattern;
        private double customExportTime;

        public PatternGalleryVm() {
            Patterns = new ObservableCollection<OsuPattern>();
            FileHandler = new OsuPatternFileHandler();
            OsuPatternMaker = new OsuPatternMaker();
            OsuPatternPlacer = new OsuPatternPlacer();

            RemoveCommand = new CommandImplementation(_ => RemoveSelected());
            OpenExplorerSelectedCommand = new CommandImplementation(_ => {
                string path = Patterns.Where(o => o.IsSelected)
                    .Select(o => FileHandler.GetPatternPath(o.FileName))
                    .FirstOrDefault();
                if (!string.IsNullOrWhiteSpace(path)) CorePlatform.Shell.OpenFolder(path);
            });
        }

        public string CollectionName {
            get => collectionName;
            set => Set(ref collectionName, value);
        }

        public ObservableCollection<OsuPattern> Patterns {
            get => patterns;
            set {
                if (patterns is not null) patterns.CollectionChanged -= PatternsChanged;
                if (Set(ref patterns, value ?? new ObservableCollection<OsuPattern>())) {
                    patterns.CollectionChanged += PatternsChanged;
                    RaisePropertyChanged(nameof(VisiblePatterns));
                    RaisePropertyChanged(nameof(Groups));
                }
            }
        }

        public OsuPatternFileHandler FileHandler { get; set; }
        [JsonIgnore] public OsuPatternMaker OsuPatternMaker { get; set; }
        [JsonIgnore] public OsuPatternPlacer OsuPatternPlacer { get; set; }

        [JsonIgnore]
        public string SearchFilter {
            get => searchFilter;
            set {
                if (Set(ref searchFilter, value ?? string.Empty)) {
                    RaisePropertyChanged(nameof(VisiblePatterns));
                }
            }
        }

        [JsonIgnore]
        public string[] SortableProperties { get; } = {
            "Name", "Creation time", "Last used time", "Usage count",
            "Object count", "Duration", "Beat length"
        };

        [JsonIgnore]
        public string SortProperty {
            get => sortProperty;
            set {
                if (Set(ref sortProperty, value)) RaisePropertyChanged(nameof(VisiblePatterns));
            }
        }

        [JsonIgnore]
        public int SortDirection {
            get => sortDirection;
            set {
                if (Set(ref sortDirection, value)) RaisePropertyChanged(nameof(VisiblePatterns));
            }
        }

        [JsonIgnore]
        public IEnumerable<OsuPattern> VisiblePatterns {
            get {
                IEnumerable<OsuPattern> result = Patterns;
                if (!string.IsNullOrWhiteSpace(SearchFilter)) {
                    result = result.Where(o => (o.Name ?? string.Empty).Contains(
                        SearchFilter, StringComparison.OrdinalIgnoreCase));
                }
                result = SortProperty switch {
                    "Name" => result.OrderBy(o => o.Name),
                    "Creation time" => result.OrderBy(o => o.CreationTime),
                    "Last used time" => result.OrderBy(o => o.LastUsedTime),
                    "Usage count" => result.OrderBy(o => o.UseCount),
                    "Object count" => result.OrderBy(o => o.ObjectCount),
                    "Duration" => result.OrderBy(o => o.Duration),
                    "Beat length" => result.OrderBy(o => o.BeatLength),
                    _ => result
                };
                return SortDirection == 0 ? result : result.Reverse();
            }
        }

        [JsonIgnore]
        public IEnumerable<string> Groups => Patterns.Select(o => o.Group)
            .Where(o => !string.IsNullOrWhiteSpace(o)).Distinct()
            .OrderBy(o => o, StringComparer.OrdinalIgnoreCase);

        public OsuPattern AddFromFile(string path, string name, string filter = null,
            double startTime = -1, double endTime = -1) {
            FileHandler.EnsureCollectionFolderExists();
            var pattern = OsuPatternMaker.FromFileWithSave(path, FileHandler,
                string.IsNullOrWhiteSpace(name) ? $"Pattern {Patterns.Count + 1}" : name,
                filter, startTime, endTime);
            Patterns.Add(pattern);
            return pattern;
        }

        public OsuPattern AddFromCode(string name, string hitObjectCode,
            string timingCode, double globalSv = 1.4,
            GameMode gameMode = GameMode.Standard) {
            var hitObjects = Lines(hitObjectCode)
                .Select(line => TryCreate(() => new HitObject(line)))
                .Where(o => o is not null).ToList();
            if (hitObjects.Count == 0) {
                throw new InvalidOperationException(
                    "At least one valid hit-object line is required.");
            }
            var timing = Lines(timingCode)
                .Select(line => TryCreate(() => new TimingPoint(line)))
                .Where(o => o is not null).ToList();
            FileHandler.EnsureCollectionFolderExists();
            var pattern = OsuPatternMaker.FromObjectsWithSave(hitObjects, timing,
                FileHandler, string.IsNullOrWhiteSpace(name)
                    ? $"Pattern {Patterns.Count + 1}"
                    : name, globalSv: globalSv, gameMode: gameMode);
            Patterns.Add(pattern);
            return pattern;
        }

        public void SetGroupForSelected(string group) {
            foreach (var pattern in Patterns.Where(o => o.IsSelected)) {
                pattern.Group = string.IsNullOrWhiteSpace(group) ? null : group.Trim();
            }
            RaisePropertyChanged(nameof(Groups));
            RaisePropertyChanged(nameof(VisiblePatterns));
        }

        public void RemoveSelected() {
            foreach (var pattern in Patterns.Where(o => o.IsSelected).ToList()) {
                string path = FileHandler.GetPatternPath(pattern.FileName);
                if (File.Exists(path)) File.Delete(path);
                Patterns.Remove(pattern);
            }
        }

        public void SetSelectAll(bool select) {
            foreach (var pattern in Patterns) pattern.IsSelected = select;
        }

        private static IEnumerable<string> Lines(string text) =>
            (text ?? string.Empty).Split(new[] { '\r', '\n' },
                StringSplitOptions.RemoveEmptyEntries).Select(o => o.Trim());

        private static T TryCreate<T>(Func<T> make) where T : class {
            try { return make(); } catch { return null; }
        }

        private void PatternsChanged(object sender, NotifyCollectionChangedEventArgs e) {
            RaisePropertyChanged(nameof(VisiblePatterns));
            RaisePropertyChanged(nameof(Groups));
        }

        public ExportTimeMode ExportTimeMode {
            get => exportTimeMode;
            set {
                if (Set(ref exportTimeMode, value)) {
                    RaisePropertyChanged(nameof(CustomExportTimeVisible));
                }
            }
        }
        [JsonIgnore] public IEnumerable<ExportTimeMode> ExportTimeModes =>
            Enum.GetValues(typeof(ExportTimeMode)) as ExportTimeMode[];
        public double CustomExportTime {
            get => customExportTime;
            set => Set(ref customExportTime, value);
        }
        [JsonIgnore] public bool CustomExportTimeVisible =>
            ExportTimeMode == ExportTimeMode.Custom;

        public double Padding {
            get => OsuPatternPlacer.Padding;
            set {
                if (Set(ref OsuPatternPlacer.Padding, value)) OsuPatternMaker.Padding = value;
            }
        }
        public double PartingDistance {
            get => OsuPatternPlacer.PartingDistance;
            set => Set(ref OsuPatternPlacer.PartingDistance, value);
        }
        public PatternOverwriteMode PatternOverwriteMode {
            get => OsuPatternPlacer.PatternOverwriteMode;
            set => Set(ref OsuPatternPlacer.PatternOverwriteMode, value);
        }
        [JsonIgnore] public IEnumerable<PatternOverwriteMode> PatternOverwriteModes =>
            Enum.GetValues(typeof(PatternOverwriteMode)) as PatternOverwriteMode[];
        public TimingOverwriteMode TimingOverwriteMode {
            get => OsuPatternPlacer.TimingOverwriteMode;
            set => Set(ref OsuPatternPlacer.TimingOverwriteMode, value);
        }
        [JsonIgnore] public IEnumerable<TimingOverwriteMode> TimingOverwriteModes =>
            Enum.GetValues(typeof(TimingOverwriteMode)) as TimingOverwriteMode[];
        public bool IncludeHitsounds {
            get => OsuPatternPlacer.IncludeHitsounds;
            set => Set(ref OsuPatternPlacer.IncludeHitsounds, value);
        }
        public bool IncludeKiai {
            get => OsuPatternPlacer.IncludeKiai;
            set => Set(ref OsuPatternPlacer.IncludeKiai, value);
        }
        public bool ScaleToNewCircleSize {
            get => OsuPatternPlacer.ScaleToNewCircleSize;
            set => Set(ref OsuPatternPlacer.ScaleToNewCircleSize, value);
        }
        public bool ScaleToNewTiming {
            get => OsuPatternPlacer.ScaleToNewTiming;
            set => Set(ref OsuPatternPlacer.ScaleToNewTiming, value);
        }
        public bool SnapToNewTiming {
            get => OsuPatternPlacer.SnapToNewTiming;
            set => Set(ref OsuPatternPlacer.SnapToNewTiming, value);
        }
        public IBeatDivisor[] BeatDivisors {
            get => OsuPatternPlacer.BeatDivisors;
            set => Set(ref OsuPatternPlacer.BeatDivisors, value);
        }
        public bool FixGlobalSv {
            get => OsuPatternPlacer.FixGlobalSv;
            set => Set(ref OsuPatternPlacer.FixGlobalSv, value);
        }
        public bool FixBpmSv {
            get => OsuPatternPlacer.FixBpmSv;
            set => Set(ref OsuPatternPlacer.FixBpmSv, value);
        }
        public bool FixColourHax {
            get => OsuPatternPlacer.FixColourHax;
            set => Set(ref OsuPatternPlacer.FixColourHax, value);
        }
        public bool FixStackLeniency {
            get => OsuPatternPlacer.FixStackLeniency;
            set => Set(ref OsuPatternPlacer.FixStackLeniency, value);
        }
        public bool FixTickRate {
            get => OsuPatternPlacer.FixTickRate;
            set => Set(ref OsuPatternPlacer.FixTickRate, value);
        }
        public double CustomScale {
            get => OsuPatternPlacer.CustomScale;
            set => Set(ref OsuPatternPlacer.CustomScale, value);
        }
        public double CustomRotate {
            get => MathHelper.RadiansToDegrees(OsuPatternPlacer.CustomRotate);
            set => Set(ref OsuPatternPlacer.CustomRotate,
                MathHelper.DegreesToRadians(value));
        }

        [JsonIgnore] public CommandImplementation RemoveCommand { get; }
        [JsonIgnore] public CommandImplementation OpenExplorerSelectedCommand { get; }
        [JsonIgnore] public string[] Paths { get; set; }
        [JsonIgnore] public bool Quick { get; set; }
    }
}
