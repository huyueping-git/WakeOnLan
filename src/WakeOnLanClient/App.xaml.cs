using System;
using System.Threading.Tasks;
using System.Windows;
using WakeOnLanClient.Services;

namespace WakeOnLanClient
{
    /// <summary>
    /// 应用程序入口。
    /// </summary>
    public partial class App : Application
    {
        protected override void OnStartup(StartupEventArgs e)
        {
            AppDomain.CurrentDomain.UnhandledException += OnUnhandledException;
            DispatcherUnhandledException += OnDispatcherUnhandledException;
            TaskScheduler.UnobservedTaskException += OnUnobservedTaskException;
            base.OnStartup(e);
        }

        protected override void OnExit(ExitEventArgs e)
        {
            FileLogWriter.Close();
            base.OnExit(e);
        }

        private static void OnDispatcherUnhandledException(object sender, System.Windows.Threading.DispatcherUnhandledExceptionEventArgs e)
        {
            FileLogWriter.Write("[" + DateTime.Now.ToString("yyyy-MM-dd HH:mm:ss") + "] [ERROR] UI 未处理异常: " + e.Exception);
            e.Handled = true;
        }

        private static void OnUnhandledException(object sender, UnhandledExceptionEventArgs e)
        {
            FileLogWriter.Write("[" + DateTime.Now.ToString("yyyy-MM-dd HH:mm:ss") + "] [ERROR] 进程未处理异常: " + e.ExceptionObject);
        }

        private static void OnUnobservedTaskException(object sender, UnobservedTaskExceptionEventArgs e)
        {
            FileLogWriter.Write("[" + DateTime.Now.ToString("yyyy-MM-dd HH:mm:ss") + "] [ERROR] 后台任务未观察异常: " + e.Exception);
            e.SetObserved();
        }
    }
}
