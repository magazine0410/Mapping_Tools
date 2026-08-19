using System.ComponentModel;
using Mapping_Tools.Classes;
using Mapping_Tools.Classes.SystemTools.Platform;

namespace Mapping_Tools.Avalonia.Views {
    /// <summary>
    /// A tool that does its work on a worker thread, and reports how far it got.
    /// </summary>
    /// <remarks>
    /// The same shape as the WPF <c>SingleRunMappingTool</c>. <see cref="BackgroundWorker"/>
    /// is portable, so the run logic of a tool ports without change. Only the two ways
    /// of telling the user differ, and both now go through <see cref="CorePlatform"/>.
    /// </remarks>
    [HiddenTool]
    public class SingleRunMappingTool : MappingTool {
        protected readonly BackgroundWorker BackgroundWorker;

        private bool canRun = true;

        /// <summary>False while the tool is busy, so the run button greys out.</summary>
        public bool CanRun {
            get => canRun;
            set => Set(ref canRun, value);
        }

        private int progress;

        /// <summary>How far the run got, from 0 to 100.</summary>
        public int Progress {
            get => progress;
            set => Set(ref progress, value);
        }

        private bool verbose;

        /// <summary>
        /// True shows the result in a dialog the user must close. False shows it in the
        /// message bar, which goes away by itself.
        /// </summary>
        public bool Verbose {
            get => verbose;
            set => Set(ref verbose, value);
        }

        public SingleRunMappingTool() {
            BackgroundWorker = new BackgroundWorker { WorkerReportsProgress = true };
            BackgroundWorker.DoWork += BackgroundWorker_DoWork;
            BackgroundWorker.RunWorkerCompleted += BackgroundWorker_RunWorkerCompleted;
            BackgroundWorker.ProgressChanged += BackgroundWorker_ProgressChanged;
        }

        protected virtual void BackgroundWorker_DoWork(object sender, DoWorkEventArgs e) { }

        protected virtual void BackgroundWorker_ProgressChanged(object sender, ProgressChangedEventArgs e) {
            Progress = e.ProgressPercentage;
        }

        /// <summary>
        /// Shows any fault, shows the result when there is one, then lets the tool run
        /// again.
        /// </summary>
        protected virtual void BackgroundWorker_RunWorkerCompleted(object sender, RunWorkerCompletedEventArgs e) {
            if (e.Error != null) {
                e.Error.Show();
            } else if (e.Result is string message && !string.IsNullOrEmpty(message)) {
                if (Verbose) {
                    CorePlatform.Dialogs.ShowMessage(message);
                } else {
                    CorePlatform.Notifications.Notify(message);
                }
            }

            Progress = 0;
            CanRun = true;
        }

        protected static void UpdateProgressBar(BackgroundWorker worker, int progress) {
            if (worker is { WorkerReportsProgress: true }) {
                worker.ReportProgress(progress);
            }
        }
    }
}
