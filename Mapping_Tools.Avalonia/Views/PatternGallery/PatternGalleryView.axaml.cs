using System;
using System.ComponentModel;
using System.IO;
using System.IO.Compression;
using System.Linq;
using System.Threading.Tasks;
using Avalonia.Controls;
using Avalonia.Input;
using Avalonia.Interactivity;
using Avalonia.Platform.Storage;
using Avalonia.VisualTree;
using Mapping_Tools.Classes.SystemTools;
using Mapping_Tools.Classes.SystemTools.Platform;
using Mapping_Tools.Classes.Tools.PatternGallery;
using Mapping_Tools.Components;
using Mapping_Tools.Viewmodels;

namespace Mapping_Tools.Avalonia.Views.PatternGallery {
    public partial class PatternGalleryView : SingleRunMappingTool,
        ISavable<PatternGalleryVm>, IHasExtraAutoSaveTarget {
        private string importName = "Pattern";
        private string hitObjectCode = string.Empty;
        private string timingCode = string.Empty;
        private string groupName = string.Empty;

        public static readonly string ToolName = "Pattern Gallery";
        public static readonly string ToolDescription =
            "Build searchable pattern collections from beatmaps or raw .osu code, then place selected patterns into other maps.";

        public PatternGalleryView() {
            InitializeComponent();
            DataContext = new PatternGalleryVm();
            ProjectManager.LoadProject(this, message: false);
            InitializeFileHandler();
        }

        public PatternGalleryVm ViewModel => (PatternGalleryVm)DataContext;
        public string ImportName { get => importName; set => Set(ref importName, value); }
        public string HitObjectCode { get => hitObjectCode; set => Set(ref hitObjectCode, value); }
        public string TimingCode { get => timingCode; set => Set(ref timingCode, value); }
        public string GroupName { get => groupName; set => Set(ref groupName, value); }

        private void InitializeFileHandler() {
            ViewModel.FileHandler.BasePath = DefaultSaveFolder;
            ViewModel.FileHandler.EnsureCollectionFolderExists();
        }

        private async void AddBeatmap_Click(object sender, RoutedEventArgs e) {
            var paths = CorePlatform.FileDialogs.BeatmapFileDialog();
            if (paths.Length == 0) return;
            ViewModel.AddFromFile(paths[0], string.IsNullOrWhiteSpace(ImportName)
                ? Path.GetFileNameWithoutExtension(paths[0])
                : ImportName);
        }

        private void AddCode_Click(object sender, RoutedEventArgs e) =>
            ViewModel.AddFromCode(ImportName, HitObjectCode, TimingCode);

        private void ApplyGroup_Click(object sender, RoutedEventArgs e) =>
            ViewModel.SetGroupForSelected(GroupName);

        private void ReverseSort_Click(object sender, RoutedEventArgs e) =>
            ViewModel.SortDirection = ViewModel.SortDirection == 0 ? 1 : 0;

        private void Pattern_DoubleTapped(object sender, TappedEventArgs e) {
            if (e.Source is not Control { DataContext: OsuPattern pattern }) return;
            ViewModel.SetSelectAll(false);
            pattern.IsSelected = true;
            RunTool(CorePlatform.FileDialogs.GetCurrentBeatmaps());
        }

        private void Start_Click(object sender, RoutedEventArgs e) =>
            RunTool(CorePlatform.FileDialogs.GetCurrentBeatmaps());

        internal void RunTool(string[] paths) {
            if (!CanRun) return;
            foreach (var box in this.GetVisualDescendants().OfType<ValidatedTextBox>()) {
                box.Commit();
            }
            if (paths is null || paths.Length == 0) {
                CorePlatform.Dialogs.ShowMessage("Open a destination beatmap first.",
                    "No beatmap");
                return;
            }
            BackupManager.SaveMapBackup(paths);
            ViewModel.Paths = paths;
            BackgroundWorker.RunWorkerAsync(ViewModel);
            CanRun = false;
        }

        protected override void BackgroundWorker_DoWork(object sender, DoWorkEventArgs e) =>
            e.Result = PatternGalleryRunner.Export((PatternGalleryVm)e.Argument,
                sender as BackgroundWorker);

        private void RestoreCollection_Click(object sender, RoutedEventArgs e) {
            var indexed = ViewModel.Patterns.Select(o => o.FileName).ToHashSet();
            var actual = Directory.GetFiles(ViewModel.FileHandler.GetPatternFilesFolderPath(),
                "*.osu").Select(Path.GetFileName).ToHashSet();
            foreach (var pattern in ViewModel.Patterns
                         .Where(o => !actual.Contains(o.FileName)).ToList()) {
                ViewModel.Patterns.Remove(pattern);
            }
            foreach (string filename in actual.Except(indexed)) {
                string name = Path.GetFileNameWithoutExtension(filename).Split("__")[^1];
                ViewModel.Patterns.Add(ViewModel.OsuPatternMaker.FromFile(
                    ViewModel.FileHandler.GetPatternPath(filename), name, true));
            }
            CorePlatform.Notifications.Notify("Collection restored.");
        }

        private async void ImportCollection_Click(object sender, RoutedEventArgs e) {
            string zipPath = await PickZip();
            if (string.IsNullOrWhiteSpace(zipPath)) return;
            using var archive = ZipFile.OpenRead(zipPath);
            var projectEntry = archive.Entries.FirstOrDefault(o =>
                o.FullName.EndsWith(".json", StringComparison.OrdinalIgnoreCase));
            if (projectEntry is null) throw new InvalidDataException("The archive has no project file.");
            var project = ProjectManager.LoadJson<PatternGalleryVm>(projectEntry.Open());
            foreach (var pattern in project.Patterns) {
                string fileName = Path.GetFileName(pattern.FileName);
                if (string.IsNullOrWhiteSpace(fileName) || fileName != pattern.FileName) {
                    continue;
                }
                var entry = archive.Entries.FirstOrDefault(o =>
                    Path.GetFileName(o.FullName) == fileName);
                if (entry is null) continue;
                string target = ViewModel.FileHandler.GetPatternPath(fileName);
                entry.ExtractToFile(target, overwrite: false);
                ViewModel.Patterns.Add(pattern);
            }
            CorePlatform.Notifications.Notify("Collection imported.");
        }

        private void ExportCollection_Click(object sender, RoutedEventArgs e) {
            Directory.CreateDirectory(CorePlatform.Paths.ExportPath);
            string path = Path.Combine(CorePlatform.Paths.ExportPath,
                ViewModel.CollectionName + ".zip");
            if (File.Exists(path)) File.Delete(path);
            using var archive = ZipFile.Open(path, ZipArchiveMode.Create);
            var projectEntry = archive.CreateEntry(ViewModel.CollectionName + ".json");
            using (var writer = new StreamWriter(projectEntry.Open())) {
                ProjectManager.WriteJson(writer, ViewModel);
            }
            foreach (var pattern in ViewModel.Patterns) {
                archive.CreateEntryFromFile(ViewModel.FileHandler.GetPatternPath(
                    pattern.FileName), pattern.FileName);
            }
            CorePlatform.Shell.OpenFolder(path);
        }

        private async Task<string> PickZip() {
            var storage = TopLevel.GetTopLevel(this)?.StorageProvider;
            if (storage is null) return string.Empty;
            var files = await storage.OpenFilePickerAsync(new FilePickerOpenOptions {
                Title = "Import pattern collection",
                AllowMultiple = false,
                FileTypeFilter = new[] {
                    new FilePickerFileType("ZIP archive") { Patterns = new[] { "*.zip" } }
                }
            });
            return files.Count == 0 ? string.Empty : files[0].TryGetLocalPath() ?? string.Empty;
        }

        public PatternGalleryVm GetSaveData() => ViewModel;
        public void SetSaveData(PatternGalleryVm saveData) {
            DataContext = saveData;
            InitializeFileHandler();
        }
        public string AutoSavePath => Path.Combine(CorePlatform.Paths.AppDataPath,
            "patterngalleryproject.json");
        public string DefaultSaveFolder => Path.Combine(CorePlatform.Paths.AppDataPath,
            "Pattern Gallery Projects");
        public string ExtraAutoSavePath => Path.Combine(
            ViewModel.FileHandler.GetCollectionFolderPath(), "project.json");
    }
}
