using FFXIV_TexTools.Helpers;
using FFXIV_TexTools.Properties;
using HelixToolkit.Wpf.SharpDX.Utilities;
using MahApps.Metro;
using System;
using System.Diagnostics;
using System.Threading.Tasks;
using System.Windows;
using System.Windows.Forms;
using System.Windows.Threading;
using Application = System.Windows.Application;
using Clipboard = System.Windows.Clipboard;

namespace FFXIV_TexTools
{
    /// <summary>
    /// Interaction logic for App.xaml
    /// </summary>
    public partial class App : Application
    {
        static NVOptimusEnabler nvEnabler = new NVOptimusEnabler();

        protected override void OnStartup(StartupEventArgs e)
        {
            var appStyle = ThemeManager.DetectAppStyle(Application.Current);

            ThemeManager.ChangeAppStyle(Application.Current, ThemeManager.GetAccent(appStyle.Item2.Name), ThemeManager.GetAppTheme(Settings.Default.Application_Theme));

            AppDomain.CurrentDomain.UnhandledException += (sender, args) => ReportException(args.ExceptionObject);
            Dispatcher.UnhandledException += (sender, args) => ReportException(args.Exception);
            TaskScheduler.UnobservedTaskException += (sender, args) => ReportException(args.Exception);

            base.OnStartup(e);

            var mainWindow = new MainWindow(e.Args);
            mainWindow.Show();
        }

        private void ReportException(object exception)
        {
            var ver = FileVersionInfo.GetVersionInfo(System.Reflection.Assembly.GetExecutingAssembly().Location).FileVersion;

            const string lineBreak = "\n======================================================\n";

            var errorText = "TexTools ran into an error.\n\n" +
                            "Please submit a bug report with the following information.\n " +
                            lineBreak +
                            exception +
                            lineBreak + "\n" +
                            "Copy to clipboard?";

            if (FlexibleMessageBox.Show(errorText, "Crash Report " + ver, MessageBoxButtons.YesNo, MessageBoxIcon.Error) ==
                DialogResult.Yes)
            {
                Clipboard.SetText(exception.ToString());
            }
        }
    }
}
