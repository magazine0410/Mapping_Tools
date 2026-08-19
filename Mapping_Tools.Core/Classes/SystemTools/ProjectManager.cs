using Mapping_Tools.Classes.SystemTools.Platform;
using Newtonsoft.Json;
using Newtonsoft.Json.Serialization;
using System;
using System.IO;
using System.Linq;
using System.Reflection;
using System.Windows;
using Mapping_Tools.Classes.JsonConverters;

namespace Mapping_Tools.Classes.SystemTools {
    public enum ErrorType
    {
        Success,
        Error,
        Warning
    }

    public static class ProjectManager {
        private static readonly JsonSerializer serializer = new() {
            NullValueHandling = NullValueHandling.Ignore,
            TypeNameHandling = TypeNameHandling.Objects,
            TypeNameAssemblyFormatHandling = TypeNameAssemblyFormatHandling.Simple,
            SerializationBinder = new ProjectSerializationBinder(),
            Formatting = Formatting.Indented,
            ReferenceLoopHandling = ReferenceLoopHandling.Ignore, 
            Converters = { new Vector2Converter()}
        };

        public static void WriteJson(StreamWriter streamWriter, object obj) {
            using (JsonTextWriter reader = new JsonTextWriter(streamWriter)) {
                serializer.Serialize(reader, obj);
            }
        }

        public static void SaveJson(string path, object obj) {
            using (StreamWriter fs = new StreamWriter(path)) {
                WriteJson(fs, obj);
            }
        }
        
        public static T LoadJson<T>(string path) {
            using (StreamReader fs = new StreamReader(path)) {
                using (JsonReader reader = new JsonTextReader(fs)) {
                    return serializer.Deserialize<T>(reader);
                }
            }
        }

        public static T LoadJson<T>(Stream stream) {
            using (StreamReader fs = new StreamReader(stream)) {
                using (JsonReader reader = new JsonTextReader(fs)) {
                    return serializer.Deserialize<T>(reader);
                }
            }
        }

        public static void AutoSaveProject<T>(ISavable<T> view) {
            string path = view.AutoSavePath;
            SaveProject(view, path);

            if (view is IHasExtraAutoSaveTarget hasExtraAutoSaveTarget) {
                SaveProject(view, hasExtraAutoSaveTarget.ExtraAutoSavePath);
            }
        }

        public static void SaveProjectDialog<T>(ISavable<T> view) {
            Directory.CreateDirectory(view.DefaultSaveFolder);
            string path = CorePlatform.FileDialogs.SaveProjectDialog(view.DefaultSaveFolder);
            SaveProject(view, path);
        }

        public static void SaveProject<T>(ISavable<T> view, string path) {
            // If the file name is not an empty string open it for saving.
            if (string.IsNullOrEmpty(path)) return;
            try {
                SaveJson(path, view.GetSaveData());
            } catch (Exception ex) {
                Console.WriteLine(ex.StackTrace);
                Console.WriteLine(ex.Message);

                CorePlatform.Dialogs.ShowMessage("Project could not be saved!");
                ex.Show();
            }
        }

        public static void LoadProject<T>(ISavable<T> view, bool dialog=false, bool message=true) {
            if (dialog)
                Directory.CreateDirectory(view.DefaultSaveFolder);
            string path = dialog ? CorePlatform.FileDialogs.LoadProjectDialog(view.DefaultSaveFolder) : view.AutoSavePath;

            // If the file name is not an empty string open it for saving.  
            if (string.IsNullOrEmpty(path)) return;

            // No auto-save file yet is the normal state the first time a tool opens.
            // It is not a fault, so it does not go to the console as one.
            if (!dialog && !File.Exists(path)) return;

            try {
                T project = LoadJson<T>(path);

                if (project == null) {
                    throw new Exception("Loaded project is a null reference.");
                }

                view.SetSaveData(project);
            } catch (Exception ex) {
                Console.WriteLine(ex.StackTrace);
                Console.WriteLine(ex.Message);

                if (message) {
                    CorePlatform.Dialogs.ShowMessage("Project could not be loaded!");
                    ex.Show();
                }
            }
        }

        public static void NewProject<T>(ISavable<T> view, bool dialog = false, bool message = true) {
            if (dialog) {
                var messageBoxResult = CorePlatform.Dialogs.AskYesNo("Are you sure you want to start a new project? All unsaved progress will be lost.", "Confirm new project");
                if (!messageBoxResult) return;
            }

            try {
                T project = Activator.CreateInstance<T>();
                view.SetSaveData(project);
            } catch (Exception ex) {
                Console.WriteLine(ex.StackTrace);
                Console.WriteLine(ex.Message);

                if (message) {
                    CorePlatform.Dialogs.ShowMessage("New project could not be initialized!");
                    ex.Show();
                }
            }
        }

        /// <summary>
        /// Gets the project file for a savable tool with optional dialog.
        /// Uses default save path if no dialog is used.
        /// </summary>
        /// <typeparam name="T">The type of the project data</typeparam>
        /// <param name="view">The tool to get the project from</param>
        /// <param name="dialog">Whether to use a dialog</param>
        /// <returns></returns>
        public static T GetProject<T>(ISavable<T> view, bool dialog=false) {
            if (dialog)
                Directory.CreateDirectory(view.DefaultSaveFolder);
            string path = dialog ? CorePlatform.FileDialogs.LoadProjectDialog(view.DefaultSaveFolder) : view.AutoSavePath;

            if (string.IsNullOrEmpty(path)) return default;
            return LoadJson<T>(path);
        }

        public static void SaveToolFile<T, T2>(ISavable<T> view, T2 obj, bool dialog = false) {
            if (dialog)
                Directory.CreateDirectory(view.DefaultSaveFolder);
            string path = dialog ? CorePlatform.FileDialogs.SaveProjectDialog(view.DefaultSaveFolder) : view.AutoSavePath;

            if (string.IsNullOrEmpty(path)) return;
            SaveJson(path, obj);
        }

        public static T2 LoadToolFile<T, T2>(ISavable<T> view, bool dialog = false) {
            if (dialog)
                Directory.CreateDirectory(view.DefaultSaveFolder);
            string path = dialog ? CorePlatform.FileDialogs.LoadProjectDialog(view.DefaultSaveFolder) : view.AutoSavePath;

            if (string.IsNullOrEmpty(path)) return default;
            return LoadJson<T2>(path);
        }

        public static bool IsSavable(object obj) {
            return IsSavable(obj.GetType());
        }

        public static bool IsSavable(Type type) {
            return type.GetInterfaces().Any(x =>
                x.IsGenericType &&
                x.GetGenericTypeDefinition() == typeof(ISavable<>));
        }

        /// <summary>
        /// Resolves project types written before the portable core was split out of
        /// the Windows application. Those files name the old "Mapping Tools"
        /// assembly even though the types now live in Mapping_Tools.Core.
        /// </summary>
        private sealed class ProjectSerializationBinder : ISerializationBinder {
            private const string LegacyAssemblyName = "Mapping Tools";

            public Type BindToType(string assemblyName, string typeName) {
                var simpleAssemblyName = string.IsNullOrEmpty(assemblyName)
                    ? null
                    : new AssemblyName(assemblyName).Name;

                if (simpleAssemblyName == LegacyAssemblyName) {
                    var coreType = typeof(ProjectManager).Assembly.GetType(typeName, false);
                    if (coreType is not null) return coreType;
                }

                var assembly = AppDomain.CurrentDomain.GetAssemblies()
                    .FirstOrDefault(candidate =>
                        candidate.GetName().Name == simpleAssemblyName);

                if (assembly is null && !string.IsNullOrEmpty(assemblyName)) {
                    try {
                        assembly = Assembly.Load(new AssemblyName(assemblyName));
                    } catch {
                        // Report one consistent serialization error below.
                    }
                }

                var resolvedType = assembly?.GetType(typeName, false);
                if (resolvedType is not null) return resolvedType;

                throw new JsonSerializationException(
                    $"Could not resolve project type '{typeName}' from assembly '{assemblyName}'.");
            }

            public void BindToName(Type serializedType, out string assemblyName,
                out string typeName) {
                assemblyName = serializedType.Assembly.GetName().Name;
                typeName = serializedType.FullName;
            }
        }
    }
}
