using System.Windows;
using Application = System.Windows.Application;

namespace PowerStatus {
    public partial class App : Application {

        protected override void OnStartup(StartupEventArgs e) {
            base.OnStartup(e);
            var mainWindow = new MainWindow();
            mainWindow.Hide();
        }
    }
}
