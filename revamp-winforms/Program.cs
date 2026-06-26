using System;
using System.Diagnostics;
using System.Runtime.InteropServices;
using System.Threading;
using System.Windows.Forms;

namespace EazyRentRevamp
{
    internal static class Program
    {
        private const string SingleInstanceMutexName = "Global\\EazyRentRevamp_SingleInstance";

        [DllImport("user32.dll")]
        private static extern bool SetForegroundWindow(IntPtr hWnd);

        [DllImport("user32.dll")]
        private static extern bool ShowWindow(IntPtr hWnd, int nCmdShow);

        private const int SW_RESTORE = 9;

        [STAThread]
        static void Main()
        {
            using var mutex = new Mutex(initiallyOwned: true, name: SingleInstanceMutexName, createdNew: out var createdNew);
            if (!createdNew)
            {
                TryActivateRunningInstance();
                return;
            }

            ApplicationConfiguration.Initialize();
            Application.Run(new MainForm());
        }

        private static void TryActivateRunningInstance()
        {
            try
            {
                using var current = Process.GetCurrentProcess();
                var processes = Process.GetProcessesByName(current.ProcessName);
                foreach (var p in processes)
                {
                    if (p.Id == current.Id) continue;
                    var h = p.MainWindowHandle;
                    if (h == IntPtr.Zero) continue;
                    ShowWindow(h, SW_RESTORE);
                    SetForegroundWindow(h);
                    break;
                }
            }
            catch
            {
                // best effort
            }
        }
    }
}
