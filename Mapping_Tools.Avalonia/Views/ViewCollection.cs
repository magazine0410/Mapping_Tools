using System;
using System.Collections.Generic;
using System.Linq;
using System.Reflection;
using Avalonia.Controls;
using Mapping_Tools.Classes.SystemTools;
using Mapping_Tools.Classes.SystemTools.Platform;

namespace Mapping_Tools.Avalonia.Views {
    /// <summary>
    /// Finds the tools, and keeps one instance of each.
    /// </summary>
    /// <remarks>
    /// This mirrors the WPF <c>ViewCollection</c>. There is no list of tools anywhere.
    /// A tool is a class that extends <see cref="MappingTool"/> and declares the static
    /// fields <c>ToolName</c> and <c>ToolDescription</c>. Nothing else registers it.
    /// </remarks>
    public class ViewCollection {
        private static readonly Type acceptableType = typeof(UserControl);
        private static readonly Type mappingToolType = typeof(MappingTool);

        public Dictionary<Type, object> Views = new();

        private static Type[] allViewTypes;
        public static Type[] GetAllViewTypes() {
            return allViewTypes ??= AppDomain.CurrentDomain.GetAssemblies()
                .SelectMany(GetLoadableTypes)
                .Where(x => acceptableType.IsAssignableFrom(x) && !x.IsInterface && !x.IsAbstract)
                .ToArray();
        }

        private static Type[] allToolTypes;
        public static Type[] GetAllToolTypes() {
            return allToolTypes ??= GetAllViewTypes()
                .Where(x => mappingToolType.IsAssignableFrom(x) && !x.IsInterface && !x.IsAbstract)
                .Where(x => x.GetCustomAttribute<HiddenToolAttribute>() is null)
                .Where(x => x.GetField("ToolName") is not null)
                .ToArray();
        }

        /// <summary>
        /// An assembly can fail to give all of its types. Take the ones that load.
        /// </summary>
        private static IEnumerable<Type> GetLoadableTypes(Assembly assembly) {
            try {
                return assembly.GetTypes();
            } catch (ReflectionTypeLoadException e) {
                return e.Types.Where(t => t is not null);
            }
        }

        public static bool ViewExists(string name) {
            return GetAllViewTypes().Any(o => GetName(o) == name);
        }

        public static string[] GetNames(Type[] types) {
            return types.Where(o => o.GetField("ToolName") != null).Select(GetName).ToArray();
        }

        public static string GetName(Type type) {
            return type.GetField("ToolName") == null
                ? type.ToString()
                : type.GetField("ToolName")!.GetValue(null)!.ToString();
        }

        public static string GetDescription(Type type) {
            return type.GetField("ToolDescription") == null
                ? string.Empty
                : type.GetField("ToolDescription")!.GetValue(null)!.ToString();
        }

        public static Type GetType(string name) {
            return GetAllViewTypes().FirstOrDefault(o => GetName(o) == name);
        }

        public object GetView(Type type) {
            try {
                if (!Views.ContainsKey(type)) {
                    Views.Add(type, Activator.CreateInstance(type));
                }
            } catch (Exception ex) {
                CorePlatform.Dialogs.ShowMessage(ex.Message, "Could not open the tool");
                return null;
            }
            return Views[type];
        }

        public object GetView(string name) {
            var type = GetType(name)
                ?? throw new ArgumentException($"There exists no view with name '{name}'");
            return GetView(type);
        }

        public void AutoSaveSettings() {
            foreach (var kvp in Views.Where(kvp => ProjectManager.IsSavable(kvp.Key))) {
                ProjectManager.AutoSaveProject((dynamic)kvp.Value);
            }
        }
    }
}
