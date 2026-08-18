using System;
using Mapping_Tools.Classes.SystemTools.Platform;
using System.IO;
using System.Linq;
using System.Threading.Tasks;
using System.Windows;
using Mapping_Tools.Classes.BeatmapHelper;
using Mapping_Tools.Classes.Exceptions;
using Mapping_Tools.Classes.ToolHelpers;

namespace Mapping_Tools.Classes.SystemTools {
    public static class BackupManager {
        public static bool SaveMapBackup(string fileToCopy, bool forced = false, string filename = null, string backupCode = "") {
            if (!File.Exists(fileToCopy)) {
                CorePlatform.Dialogs.ShowMessage("Selected beatmap file does not exist! Check if you have the correct file selected in the current beatmap field, or try re-selecting the beatmap file.", "Error");
                return false;
            }

            string destinationDirectory = CorePlatform.Settings.BackupsPath;
            if (!Directory.Exists(destinationDirectory)) {
                CorePlatform.Dialogs.ShowMessage("Backups folder does not exist! Check in the Preferences if the path to your backups folder is correct and the folder exists.", "Error");
                return false;
            }

            try {
                if (!CorePlatform.Settings.MakeBackups && !forced)
                    return false;

                if (string.IsNullOrEmpty(filename))
                    filename = Path.GetFileName(fileToCopy);
                
                // Save normal copy
                DateTime now = DateTime.Now;
                var name = now.ToString("yyyy-MM-dd HH-mm-ss") + "_" + backupCode + "__" + filename;
                File.Copy(fileToCopy,Path.Combine(destinationDirectory, name), true);

                // Save second copy with newest version if possible
                if (CorePlatform.Settings.UseEditorReader && Path.GetExtension(fileToCopy) == ".osu") {
                    fileToCopy = GetNewestVersionPath(fileToCopy, out var exception);
                    
                    if (exception == null) {
                        name = now.ToString("yyyy-MM-dd HH-mm-ss") + "_" + backupCode + "_2_" + filename;
                        File.Copy(fileToCopy, Path.Combine(destinationDirectory, name), true);
                    }
                }

                // Delete old files if the number of backup files are over the limit
                foreach (var fi in new DirectoryInfo(CorePlatform.Settings.BackupsPath).GetFiles().OrderByDescending(x => x.CreationTime).Skip(CorePlatform.Settings.MaxBackupFiles))
                    fi.Delete();

                return true;
            } catch (Exception ex) {
                ex.Show();
                return false;
            }
        }

        public static bool SaveMapBackup(string[] filesToCopy, bool forced = false, string backupCode = "") {
            bool result = true;
            foreach (string fileToCopy in filesToCopy) {
                result = SaveMapBackup(fileToCopy, forced, backupCode: backupCode);
                if (!result)
                    break;
            }
            return result;
        }

        /// <summary>
        /// Copies a backup to replace a beatmap at the destination path.
        /// </summary>
        /// <param name="backupPath">Path to the backup map.</param>
        /// <param name="destination">Path to the destination map.</param>
        /// <param name="allowDifferentFilename">If false, this method throws an exception when the backup and the destination have mismatching beatmap metadata.</param>
        public static void LoadMapBackup(string backupPath, string destination, bool allowDifferentFilename = false) {
            var backupEditor = new BeatmapEditor(backupPath);
            var destinationEditor = new BeatmapEditor(destination);

            var backupFilename = backupEditor.Beatmap.GetFileName();
            var destinationFilename = destinationEditor.Beatmap.GetFileName();

            if (!allowDifferentFilename && !string.Equals(backupFilename, destinationFilename)) {
                throw new BeatmapIncompatibleException($"The backup and the destination beatmap have mismatching metadata.\n{backupFilename}\n{destinationFilename}");
            }

            File.Copy(backupPath, destination, true);
        }

        public static void QuickUndo() {
            try {
                var path = CorePlatform.FileDialogs.GetCurrentBeatmap();
                var backupFile = new DirectoryInfo(CorePlatform.Settings.BackupsPath).GetFiles().OrderByDescending(x => x.CreationTime).FirstOrDefault();
                if (backupFile != null) {
                    try {
                        LoadMapBackup(backupFile.FullName, path);
                    } catch (BeatmapIncompatibleException ex) {
                        ex.Show();
                        var result = CorePlatform.Dialogs.AskYesNo("Do you want to load the backup anyways?", "Load backup");
                        if (result == true) {
                            LoadMapBackup(backupFile.FullName, path, true);
                        } else {
                            return;
                        }
                    }
                    Task.Factory.StartNew(() => CorePlatform.Notifications.Notify("Backup successfully loaded!"));

                    if (CorePlatform.Settings.AutoReload) {
                        CorePlatform.EditorReader.ForceReloadEditor();
                    }
                }
            } catch (Exception ex) {
                ex.Show();
            }
        }

        /// <summary>
        /// Generates a temp file with the newest version of specified map and returns the path to that temp file.
        /// </summary>
        /// <param name="mapPath"></param>
        /// <returns></returns>
        private static string GetNewestVersionPath(string mapPath, out Exception exception) {
            var editor = CorePlatform.EditorReader.GetNewestVersionOrNot(mapPath, out _, out exception);

            // Save temp version
            var tempPath = Path.Combine(CorePlatform.Paths.AppDataPath, "temp.osu");

            Editor.SaveFile(tempPath, editor.Beatmap.GetLines());

            return tempPath;
        }
    }
}