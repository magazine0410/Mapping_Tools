using System.IO;
using Avalonia;
using Avalonia.Controls.ApplicationLifetimes;
using Avalonia.Markup.Xaml;
using Mapping_Tools.Avalonia.Platform;
using Mapping_Tools.Classes.SystemTools.Platform;

namespace Mapping_Tools.Avalonia {
    public partial class App : global::Avalonia.Application {
        public override void Initialize() => AvaloniaXamlLoader.Load(this);

        public override void OnFrameworkInitializationCompleted() {
            // Give the portable core its services first. Anything that touches the core
            // before this point gets the defaults, which do nothing.
            AvaloniaPlatformServices.Register();

            if (ApplicationLifetime is IClassicDesktopStyleApplicationLifetime desktop) {
                // The core writes backups and temporary files here, and expects the
                // folders to be there already.
                Directory.CreateDirectory(CorePlatform.Paths.AppDataPath);
                Directory.CreateDirectory(CorePlatform.Paths.ExportPath);

                var settings = JsonCoreSettings.Load();
                CorePlatform.Settings = settings;
                Directory.CreateDirectory(settings.BackupsPath);

                var window = new MainWindow();
                desktop.MainWindow = window;
                desktop.ShutdownRequested += (_, _) => settings.Save();
            }

            base.OnFrameworkInitializationCompleted();
        }
    }
}
