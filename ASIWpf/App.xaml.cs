using System.Configuration;
using System.Data;
using System.Runtime.InteropServices;
using System.Windows;

namespace ASIWpf
{
    /// <summary>
    /// Interaction logic for App.xaml
    /// </summary>
    public partial class App : Application
    {
        [DllImport("kernel32.dll")]
        [return: MarshalAs(UnmanagedType.Bool)]
        private static extern bool AllocConsole();

        protected override void OnStartup(StartupEventArgs e)
        {
            base.OnStartup(e);
            AllocConsole();  // 创建控制台窗口
            Console.WriteLine("Console window is now open.");
        }
    }

}
