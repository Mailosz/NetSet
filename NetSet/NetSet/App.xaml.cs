using System;
using System.Collections.Generic;
using System.Configuration;
using System.Data;
using System.IO;
using System.Linq;
using System.Security.Principal;
using System.Threading.Tasks;
using System.Diagnostics;
using System.Windows;

namespace NetSet
{
    /// <summary>
    /// Interaction logic for App.xaml
    /// </summary>
    /// 
    public enum IPver { IPv4, IPv6}
    
    public partial class App : Application
    {

        protected override async void OnStartup(StartupEventArgs e)
        {
            base.OnStartup(e);
        }

        public static void RunNetsh(string arg)
        {
            ProcessStartInfo procInfo = new ProcessStartInfo
            {
                WorkingDirectory = Path.GetPathRoot(Environment.SystemDirectory),
                FileName = Path.Combine(Environment.SystemDirectory, "netsh.exe"),
                Arguments = arg,
                RedirectStandardOutput = true,
                RedirectStandardError = true,
                UseShellExecute = false,
                CreateNoWindow = true,
                WindowStyle = ProcessWindowStyle.Hidden
            };

            Process proc = Process.Start(procInfo);
            proc.WaitForExit();
        }
    }
}
