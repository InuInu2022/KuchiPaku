////////////////////////////////////////////////////////////////////////////
//
// Epoxy template source code.
// Write your own copyright and note.
// (You can use https://github.com/rubicon-oss/LicenseHeaderManager)
//
////////////////////////////////////////////////////////////////////////////

using System.Windows;
using NLog;
using NLog.Config;
using NLog.Targets;

namespace KuchiPaku.Views
{
    public sealed partial class MainWindow : Window
    {
        public MainWindow(){
            InitializeComponent();
            InitLogger();
        }

        private static void InitLogger()
        {
            var config = new LoggingConfiguration();

            var fileTarget = new FileTarget();
            config.AddTarget("file", fileTarget);

            fileTarget.Name = "f";
            fileTarget.FileName = "${basedir}/logs/${shortdate}.log";
            fileTarget.Layout = "${longdate} [${uppercase:${level}}] ${message}";

            var rule1 = new LoggingRule("*", LogLevel.Info, fileTarget);
            config.LoggingRules.Add(rule1);

            LogManager.Configuration = config;
        }
    }
}
