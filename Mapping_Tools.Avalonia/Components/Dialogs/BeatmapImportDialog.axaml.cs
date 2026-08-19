using System;
using System.ComponentModel;
using System.Runtime.CompilerServices;
using Avalonia.Controls;
using Avalonia.Interactivity;
using Mapping_Tools.Classes;
using Mapping_Tools.Classes.SystemTools.Platform;

namespace Mapping_Tools.Components.Dialogs {
    /// <summary>
    /// Asks which beatmap to import from.
    /// </summary>
    /// <remarks>
    /// The WPF dialog has two buttons: one browses, and one asks the running osu! which
    /// beatmap is open. On Linux there is no client to ask, so both do the same thing
    /// and only the browse button is left. See section 3.2 of LINUX_PORT.md.
    /// </remarks>
    public partial class BeatmapImportDialog : UserControl, INotifyPropertyChanged {
        private string path;

        /// <summary>The beatmap that the caller reads after the dialog closes.</summary>
        public string Path {
            get => path;
            set {
                if (path == value) return;
                path = value;
                OnPropertyChanged();
            }
        }

        public BeatmapImportDialog() {
            InitializeComponent();
            DataContext = this;
        }

        private void BeatmapBrowse_Click(object sender, RoutedEventArgs e) {
            BrowseBeatmap();
        }

        internal void BrowseBeatmap() {
            try {
                var chosen = CorePlatform.FileDialogs.BeatmapFileDialog();
                if (chosen.Length > 0 && !string.IsNullOrEmpty(chosen[0])) {
                    Path = chosen[0];
                }
            } catch (Exception ex) {
                ex.Show();
            }
        }

        public new event PropertyChangedEventHandler PropertyChanged;

        protected virtual void OnPropertyChanged([CallerMemberName] string propertyName = null) {
            PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(propertyName));
        }
    }
}
