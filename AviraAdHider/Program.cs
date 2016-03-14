using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Linq;
using System.Management;
using System.Runtime.InteropServices;
using System.Text;
using System.Threading;
using System.Threading.Tasks;

namespace AviraAdHider
{
    class Program
    {
        static AutoResetEvent waiter = new AutoResetEvent(false);

        static void Main(string[] args)
        {
            var handle = Utility.GetConsoleWindow();
            Utility.ShowWindow(handle, Utility.SW_HIDE);

            Kill();

            new Thread(new ThreadStart(() => DoMonitor())).Start();
            new Thread(new ThreadStart(() =>
            {
                Console.ReadKey();
                waiter.Set();
            })).Start();

            waiter.WaitOne();
        }

        private async static void Kill()
        {
            while(true)
            {
                var current = Process.GetProcessesByName("ipmgui");
                if (current.Count() != 0)
                    current[0].Kill();
                else
                    break;

                await Task.Delay(15);
            }
        }

        private static void DoMonitor()
        {
            var u = new Utility();
            var watcher = u.WatchForProcessStart("ipmgui.exe");

            watcher.EventArrived += Watcher_EventArrived;
        }

        private static async void Watcher_EventArrived(object sender, EventArrivedEventArgs e)
        {
            var watcher = sender as ManagementEventWatcher;
            watcher.EventArrived -= Watcher_EventArrived;
            watcher.Dispose();
            Kill();
            await Task.Delay(2000);
            DoMonitor();
        }
    }

    class Utility
    {
        [DllImport("kernel32.dll")]
        public static extern IntPtr GetConsoleWindow();

        [DllImport("user32.dll")]
        public static extern bool ShowWindow(IntPtr hWnd, int nCmdShow);

        public const int SW_HIDE = 0;
        public const int SW_SHOW = 5;

        public ManagementEventWatcher WatchForProcessStart(string processName)
        {
            string queryString =
                "SELECT TargetInstance" +
                "  FROM __InstanceCreationEvent " +
                "WITHIN  10 " +
                " WHERE TargetInstance ISA 'Win32_Process' " +
                "   AND TargetInstance.Name = '" + processName + "'";

            // The dot in the scope means use the current machine
            string scope = @"\\.\root\CIMV2";

            // Create a watcher and listen for events
            ManagementEventWatcher watcher = new ManagementEventWatcher(scope, queryString);
            watcher.EventArrived += ProcessStarted;
            watcher.Start();
            return watcher;
        }

        private ManagementEventWatcher WatchForProcessEnd(string processName)
        {
            string queryString =
                "SELECT TargetInstance" +
                "  FROM __InstanceDeletionEvent " +
                "WITHIN  10 " +
                " WHERE TargetInstance ISA 'Win32_Process' " +
                "   AND TargetInstance.Name = '" + processName + "'";

            // The dot in the scope means use the current machine
            string scope = @"\\.\root\CIMV2";

            // Create a watcher and listen for events
            ManagementEventWatcher watcher = new ManagementEventWatcher(scope, queryString);
            watcher.EventArrived += ProcessEnded;
            watcher.Start();
            return watcher;
        }

        private void ProcessEnded(object sender, EventArrivedEventArgs e)
        {
            ManagementBaseObject targetInstance = (ManagementBaseObject)e.NewEvent.Properties["TargetInstance"].Value;
            string processName = targetInstance.Properties["Name"].Value.ToString();
            Console.WriteLine(String.Format("{0} process ended", processName));
        }

        private void ProcessStarted(object sender, EventArrivedEventArgs e)
        {
            ManagementBaseObject targetInstance = (ManagementBaseObject)e.NewEvent.Properties["TargetInstance"].Value;
            string processName = targetInstance.Properties["Name"].Value.ToString();
            Console.WriteLine(String.Format("{0} process started", processName));
        }
    }
}
