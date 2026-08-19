using Avalonia.Controls;
using Mapping_Tools.Avalonia.Platform;
using Microsoft.VisualStudio.TestTools.UnitTesting;

namespace Mapping_Tools.Avalonia.Tests {
    /// <summary>
    /// The shell must build and draw. Most of what can go wrong in Avalonia XAML goes
    /// wrong here, at run time, and nowhere else.
    /// </summary>
    [TestClass]
    public class ShellTests {
        [TestMethod]
        public void TheMainWindowOpensAndDraws() {
            var window = new MainWindow();
            window.Show();
            HeadlessApp.Draw(window);
        }

        [TestMethod]
        public void TheMainWindowSetsTheRootDialogHost() {
            var window = new MainWindow();
            window.Show();
            Assert.IsNotNull(Mapping_Tools.Components.DialogHost.Root,
                "Controls with no host of their own have nowhere to show a dialog.");
        }

        [TestMethod]
        public void TheMainWindowShowsTheSharedApplicationVersion() {
            // Read the version, do not write it down here: the release workflow changes
            // it in Directory.Build.props, and a literal would fail at every release.
            var expected = typeof(MainWindow).Assembly.GetName().Version!.ToString(3);

            var window = new MainWindow();
            var shown = window.FindControl<TextBlock>("VersionText")!.Text;

            Assert.AreEqual($"v{expected}", shown);
            Assert.AreNotEqual("v1.0.0", shown,
                "The host has no shared version metadata, so it fell back to the default.");
        }

        [TestMethod]
        public void FolderPathsWithSpacesStayOneShellArgument() {
            const string path = "/tmp/Mapping Tools/backups";

            var startInfo = DesktopShellService.CreateOpenFolderStartInfo(path);

            Assert.AreEqual(1, startInfo.ArgumentList.Count);
            Assert.AreEqual(path, startInfo.ArgumentList[0]);
        }
    }
}
