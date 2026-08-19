using System;
using System.ComponentModel;
using System.IO;
using System.Linq;
using Avalonia.Interactivity;
using Avalonia.VisualTree;
using Mapping_Tools.Classes.SystemTools;
using Mapping_Tools.Classes.SystemTools.Platform;
using Mapping_Tools.Classes.Tools;
using Mapping_Tools.Components;
using Mapping_Tools.Viewmodels;

namespace Mapping_Tools.Avalonia.Views.HitsoundCopier {
    [VerticalContentScroll]
    public partial class HitsoundCopierView : SingleRunMappingTool,
        ISavable<HitsoundCopierVm> {
        public static readonly string ToolName = "Hitsound Copier";
        public static readonly string ToolDescription =
            $@"Copies hitsounds from one beatmap to one or more others.{Environment.NewLine}Overwrite everything, or overwrite only sounds defined by the source map while preserving the rest.";

        public HitsoundCopierView() {
            InitializeComponent();
            DataContext = new HitsoundCopierVm();
            ProjectManager.LoadProject(this, message: false);
        }

        public HitsoundCopierVm ViewModel => (HitsoundCopierVm)DataContext;

        private void Start_Click(object sender, RoutedEventArgs e) => RunTool();

        internal void RunTool() {
            if (!CanRun) return;
            foreach (var box in this.GetVisualDescendants().OfType<ValidatedTextBox>()) {
                box.Commit();
            }
            var targets = (ViewModel.PathTo ?? string.Empty)
                .Split('|', StringSplitOptions.RemoveEmptyEntries);
            if (targets.Length == 0) {
                CorePlatform.Dialogs.ShowMessage(
                    "Choose at least one destination beatmap.", "Missing beatmap");
                return;
            }
            BackupManager.SaveMapBackup(targets);
            BackgroundWorker.RunWorkerAsync(ViewModel);
            CanRun = false;
        }

        protected override void BackgroundWorker_DoWork(object sender, DoWorkEventArgs e) {
            e.Result = global::Mapping_Tools.Classes.Tools.HitsoundCopier.Copy(
                (HitsoundCopierVm)e.Argument, sender as BackgroundWorker);
        }

        public HitsoundCopierVm GetSaveData() => ViewModel;
        public void SetSaveData(HitsoundCopierVm saveData) => DataContext = saveData;
        public string AutoSavePath => Path.Combine(CorePlatform.Paths.AppDataPath,
            "hitsoundcopierproject.json");
        public string DefaultSaveFolder => Path.Combine(CorePlatform.Paths.AppDataPath,
            "Hitsound Copier Projects");
    }
}
