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
    }
}
