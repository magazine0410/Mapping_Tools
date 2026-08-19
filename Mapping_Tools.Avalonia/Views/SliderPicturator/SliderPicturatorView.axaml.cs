using System;
using System.ComponentModel;
using System.IO;
using System.Linq;
using System.Threading.Tasks;
using Avalonia.Controls;
using Avalonia.Interactivity;
using Avalonia.Media.Imaging;
using Avalonia.Platform.Storage;
using Avalonia.Threading;
using Avalonia.VisualTree;
using Mapping_Tools.Classes.SystemTools;
using Mapping_Tools.Classes.SystemTools.Platform;
using Mapping_Tools.Classes.Tools;
using Mapping_Tools.Components;
using Mapping_Tools.Viewmodels;

namespace Mapping_Tools.Avalonia.Views.SliderPicturator {
    [VerticalContentScroll]
    [HorizontalContentScroll]
    public partial class SliderPicturatorView : SingleRunMappingTool,
        ISavable<SliderPicturatorVm> {
        private Bitmap previewBitmap;

        public static readonly string ToolName = "Slider Picturator";
        public static readonly string ToolDescription =
            "Turn an image into a distorted slider at a chosen time and position. Selected-slider import is optional and only available when an editor reader exists.";

        public SliderPicturatorView() {
            InitializeComponent();
            DataContext = new SliderPicturatorVm();
            ProjectManager.LoadProject(this, message: false);
            AttachViewModel(ViewModel);
        }

        public SliderPicturatorVm ViewModel => (SliderPicturatorVm)DataContext;

        private void AttachViewModel(SliderPicturatorVm vm) {
            vm.BrowseImageRequested = BrowseImage;
            vm.PropertyChanged += (_, e) => {
                if (e.PropertyName == nameof(SliderPicturatorVm.PreviewPng)) {
                    Dispatcher.UIThread.Post(UpdatePreview);
                }
            };
            UpdatePreview();
        }

        private async void BrowseImage() {
            var storage = TopLevel.GetTopLevel(this)?.StorageProvider;
            if (storage is null) return;
            var files = await storage.OpenFilePickerAsync(new FilePickerOpenOptions {
                Title = "Select an image",
                AllowMultiple = false,
                FileTypeFilter = new[] {
                    new FilePickerFileType("Images") {
                        Patterns = new[] { "*.png", "*.jpg", "*.jpeg", "*.bmp", "*.gif", "*.webp" }
                    }
                }
            });
            if (files.Count > 0) {
                ViewModel.PictureFile = files[0].TryGetLocalPath();
            }
        }

        private void UpdatePreview() {
            byte[] png = ViewModel.PreviewPng;
            if (png is null || png.Length == 0) {
                PreviewImage.Source = null;
                previewBitmap?.Dispose();
                previewBitmap = null;
                return;
            }
            using var stream = new MemoryStream(png, writable: false);
            var next = new Bitmap(stream);
            PreviewImage.Source = next;
            previewBitmap?.Dispose();
            previewBitmap = next;
        }

        private void Start_Click(object sender, RoutedEventArgs e) =>
            RunTool(CorePlatform.FileDialogs.GetCurrentBeatmap());

        internal void RunTool(string path) {
            if (!CanRun) return;
            foreach (var box in this.GetVisualDescendants().OfType<ValidatedTextBox>()) {
                box.Commit();
            }
            if (string.IsNullOrWhiteSpace(path)) {
                CorePlatform.Dialogs.ShowMessage("Open a destination beatmap first.",
                    "No beatmap");
                return;
            }
            BackupManager.SaveMapBackup(path);
            ViewModel.Path = path;
            BackgroundWorker.RunWorkerAsync(ViewModel);
            CanRun = false;
        }

        protected override void BackgroundWorker_DoWork(object sender, DoWorkEventArgs e) =>
            e.Result = SliderPicturatorRunner.Picturate(
                (SliderPicturatorVm)e.Argument, sender as BackgroundWorker);

        public SliderPicturatorVm GetSaveData() => ViewModel;
        public void SetSaveData(SliderPicturatorVm saveData) {
            DataContext = saveData;
            AttachViewModel(saveData);
        }
        public string AutoSavePath => Path.Combine(CorePlatform.Paths.AppDataPath,
            "sliderpicturatorproject.json");
        public string DefaultSaveFolder => Path.Combine(CorePlatform.Paths.AppDataPath,
            "Slider Picturator Projects");
    }
}
