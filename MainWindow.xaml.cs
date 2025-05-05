using System.IO;
using System.Windows;
using System.Windows.Threading;
using System.Windows.Media;
using System.Windows.Media.Imaging;
using Microsoft.Win32.TaskScheduler;
using Application = System.Windows.Application;
using MessageBox = System.Windows.MessageBox;

namespace PowerStatus {

    public partial class MainWindow : Window {

        private const string APP_NAME = "PowerStatus";
        private const double INTERVAL_SECONDS = 5; // Interval for checking power requests

        private const string ICON_FILE_SLEEP = "assets/sleep.ico";
        private const string ICON_FILE_WAKE = "assets/wake.ico";
        private readonly Icon ICON_SLEEP = LoadIconFromResource(ICON_FILE_SLEEP);
        private readonly Icon ICON_WAKE = LoadIconFromResource(ICON_FILE_WAKE);
        private readonly BitmapImage BITMAP_SLEEP = new BitmapImage(new Uri($"pack://application:,,,/{ICON_FILE_SLEEP}"));
        private readonly BitmapImage BITMAP_WAKE = new BitmapImage(new Uri($"pack://application:,,,/{ICON_FILE_WAKE}"));

        private readonly SolidColorBrush BRUSH_BLACK = new SolidColorBrush(Colors.Black);
        private readonly SolidColorBrush BRUSH_RED = new SolidColorBrush(Colors.Red);

        private NotifyIcon _notifyIcon;
        private DispatcherTimer _timer;
        private PowerConfig _powerConfig = new PowerConfig();

        public MainWindow() {
            InitializeComponent();
            this.Icon = BITMAP_SLEEP;

            string[] args = Environment.GetCommandLineArgs();
            if (args.Length > 1 && args[1].Equals("\\delay", StringComparison.OrdinalIgnoreCase)) {
                WaitUntilReady();   // delay if started from task scheduler
            }

            _notifyIcon = SetupTrayIcon();
            _timer = StartPowerRequestsTimer();
            
            EnableDisable();
        }

        private NotifyIcon SetupTrayIcon() {
            var notifyIcon = new NotifyIcon {
                Icon = ICON_SLEEP,
                Visible = true,
                Text = "Power Status"
            };

            // Add a context menu to the tray icon
            var contextMenu = new ContextMenuStrip();
            contextMenu.Items.Add("Open", null, (s, e) => ShowWindow());
            contextMenu.Items.Add("Exit", null, (s, e) => ExitApplication());
            notifyIcon.ContextMenuStrip = contextMenu;

            // Handle double-click to restore the window
            notifyIcon.DoubleClick += (s, e) => ShowWindow();

            return notifyIcon;
        }

        private DispatcherTimer StartPowerRequestsTimer() {
            var timer = new DispatcherTimer {
                Interval = TimeSpan.FromSeconds(INTERVAL_SECONDS)
            };
            timer.Tick += (s, e) => CheckPowerRequests();
            
            CheckPowerRequests();
            timer.Start();
            
            return timer;
        }

        public void ShowWindow() {
            this.Show();
            this.WindowState = WindowState.Normal;
            this.Activate();
        }

        private void ExitApplication() {
            _notifyIcon.Dispose();
            Application.Current.Shutdown();
        }

        protected override void OnStateChanged(EventArgs e) {
            base.OnStateChanged(e);
            if (WindowState == WindowState.Minimized) {
                this.Hide();
            }
        }

        protected override void OnClosing(System.ComponentModel.CancelEventArgs e) {
            base.OnClosing(e);
            e.Cancel = true;
            this.Hide();
        }

        protected override void OnClosed(EventArgs e) {
            _timer?.Stop();
            _notifyIcon.Dispose();
            base.OnClosed(e);
        }

        private void Menu_Exit_Click(object sender, RoutedEventArgs e) {
            ExitApplication();
        }

        private static void WaitUntilReady() {
            // This program starts at logon. Wait until explorer.exe is ready to add a tray icon.
            while (true) {
                try {
                    var process = System.Diagnostics.Process.GetProcessesByName("explorer").FirstOrDefault();
                    if (process != null) {
                        // wait 10 seconds for explorer.exe to be ready
                        for (int i = 0; i < 10; i++) {
                            if (process.Responding) {
                                break;
                            }
                            Thread.Sleep(1000);
                        }
                        Thread.Sleep(5 * 1000); // sleep another 5 seconds for safety
                        break;
                    }
                } catch (Exception) {
                }
                Thread.Sleep(1000);
            }
        }

        private void EnableDisable() {
            if (IsTaskScheduled()) {
                Menu_RemoveStartupTask.IsEnabled = true;
            } else {
                Menu_RemoveStartupTask.IsEnabled = false;
            }
        }

        private void Menu_About_Click(object sender, RoutedEventArgs e) {
            var assembly = System.Reflection.Assembly.GetExecutingAssembly();
            var version = assembly.GetName().Version;
            string vtext = "";
            if (version != null) {
                vtext = $"\nVersion {version.Major}.{version.Minor}.{version.Build}";
            }
            MessageBox.Show($"{APP_NAME}{version}", "About");
        }

        private void Menu_AddStartupTask_Click(object sender, RoutedEventArgs e) {
            try {
                AddToStartupTaskScheduler();
                EnableDisable();
                MessageBox.Show("Power Status has been added to startup tasks.", "Startup Task Added", MessageBoxButton.OK, MessageBoxImage.Information);
            } catch (Exception ex) {
                MessageBox.Show(ex.Message, "Error", MessageBoxButton.OK, MessageBoxImage.Error);
            }
        }

        private void Menu_RemoveStartupTask_Click(object sender, RoutedEventArgs e) {
            try {
                RemoveStartupTask();
                EnableDisable();
                MessageBox.Show("Power Status has been removed from startup tasks.", "Startup Task Removed", MessageBoxButton.OK, MessageBoxImage.Information);
            } catch (Exception ex) {
                MessageBox.Show(ex.Message, "Error", MessageBoxButton.OK, MessageBoxImage.Error);
            }
        }

        private void AddToStartupTaskScheduler() {
            if (IsTaskScheduled()) {
                RemoveStartupTask();
            }

            using (var taskService = new TaskService()) {
                string appPath = System.Reflection.Assembly.GetExecutingAssembly().Location.Replace(".dll", ".exe");
                var task = taskService.NewTask();
                task.RegistrationInfo.Description = "Power Status Application";
                task.Principal.UserId = Environment.UserName;
                task.Principal.LogonType = TaskLogonType.InteractiveToken;
                task.Triggers.Add(new LogonTrigger());
                task.Actions.Add(new ExecAction(appPath, "\\delay", null));
                task.Principal.RunLevel = TaskRunLevel.Highest;
                taskService.RootFolder.RegisterTaskDefinition(APP_NAME, task);
            }
        }

        private void RemoveStartupTask() {
            using (var taskService = new TaskService()) {
                taskService.RootFolder.DeleteTask(APP_NAME); // remove Task
            }
        }

        private bool IsTaskScheduled() {
            using (var taskService = new TaskService()) {
                var task = taskService.FindTask(APP_NAME);
                return task != null;
            }
        }

        private void CheckPowerRequests() {
            try {
                _powerConfig.UpdatePowerRequests();
                PowerStatus.Foreground = BRUSH_BLACK;

                string statusText = _powerConfig.GetStatusText();
                if (PowerStatus.Text != statusText) {
                    PowerStatus.Text = statusText;
                }

                if (_powerConfig.CanSleep) {
                    this.Title = "Power Status - Can Sleep";
                    if (_notifyIcon.Icon != ICON_SLEEP) {
                        _notifyIcon.Icon = ICON_SLEEP;
                        this.Icon = BITMAP_SLEEP;
                    }
                } else {
                    this.Title = "Power Status - Can NOT Sleep";
                    if (_notifyIcon.Icon != ICON_WAKE) {
                        _notifyIcon.Icon = ICON_WAKE;
                        this.Icon = BITMAP_WAKE;
                    }
                }
            } catch (Exception ex) {
                // Set TextBox color red
                PowerStatus.Foreground = BRUSH_RED;
                PowerStatus.Text = "Error: " + ex.Message;
            }
        }

        private static Icon LoadIconFromResource(string resourcePath) {
            var resourceUri = new Uri($"pack://application:,,,/{resourcePath}");
            using (var stream = Application.GetResourceStream(resourceUri)?.Stream) {
                if (stream == null) {
                    throw new FileNotFoundException($"Resource '{resourcePath}' not found.");
                }
                return new Icon(stream);
            }
        }
    }
}