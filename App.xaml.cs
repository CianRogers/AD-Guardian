using System;
using System.Configuration;
using System.Windows;

namespace AdHealthMonitor
{
    public partial class App : System.Windows.Application
    {
        protected override void OnExit(ExitEventArgs e)
        {
            Properties.Settings.Default.Save(); // Save settings before exit
            base.OnExit(e);
        }
    }
}
