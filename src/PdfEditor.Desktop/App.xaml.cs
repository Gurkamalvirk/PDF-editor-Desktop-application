using System.IO;
using System.Text;
using System.Windows;
using System.Windows.Threading;

namespace PdfEditor.Desktop;

public partial class App : System.Windows.Application
{
    private static readonly string LogDirectory = Path.Combine(
        Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData),
        "PdfTextEditor");

    private static readonly string LogPath = Path.Combine(LogDirectory, "startup-error.log");

    public App()
    {
        DispatcherUnhandledException += OnDispatcherUnhandledException;
        AppDomain.CurrentDomain.UnhandledException += OnDomainUnhandledException;
        TaskScheduler.UnobservedTaskException += OnUnobservedTaskException;
    }

    protected override void OnStartup(StartupEventArgs e)
    {
        base.OnStartup(e);

        try
        {
            WriteLog("Application starting.");
            var window = new MainWindow();
            MainWindow = window;
            window.Show();
            WriteLog("Main window shown successfully.");
        }
        catch (Exception ex)
        {
            ReportFatal("PDF Text Editor could not start.", ex);
            Shutdown(-1);
        }
    }

    private void OnDispatcherUnhandledException(object sender, DispatcherUnhandledExceptionEventArgs e)
    {
        ReportFatal("An unexpected UI error occurred.", e.Exception);
        e.Handled = true;
        Shutdown(-2);
    }

    private static void OnDomainUnhandledException(object sender, UnhandledExceptionEventArgs e)
    {
        if (e.ExceptionObject is Exception ex)
            WriteLog("UNHANDLED APPDOMAIN EXCEPTION\r\n" + ex);
        else
            WriteLog("UNHANDLED APPDOMAIN EXCEPTION\r\n" + e.ExceptionObject);
    }

    private static void OnUnobservedTaskException(object? sender, UnobservedTaskExceptionEventArgs e)
    {
        WriteLog("UNOBSERVED TASK EXCEPTION\r\n" + e.Exception);
        e.SetObserved();
    }

    private static void ReportFatal(string message, Exception ex)
    {
        WriteLog(message + "\r\n" + ex);
        try
        {
            var detail = GetDeepestMessage(ex);
            System.Windows.MessageBox.Show(
                $"{message}\n\n{detail}\n\nA diagnostic log was written to:\n{LogPath}",
                "PDF Text Editor - Startup Error",
                MessageBoxButton.OK,
                MessageBoxImage.Error);
        }
        catch
        {
            // If WPF itself cannot show a message, the log still contains the failure.
        }
    }

    private static string GetDeepestMessage(Exception ex)
    {
        var current = ex;
        while (current.InnerException is not null)
            current = current.InnerException;

        return current.Message;
    }

    private static void WriteLog(string text)
    {
        try
        {
            Directory.CreateDirectory(LogDirectory);
            var entry = new StringBuilder()
                .AppendLine("============================================================")
                .AppendLine(DateTimeOffset.Now.ToString("O"))
                .AppendLine($"Process: {Environment.ProcessPath}")
                .AppendLine($"OS: {Environment.OSVersion}")
                .AppendLine($"64-bit process: {Environment.Is64BitProcess}")
                .AppendLine(text)
                .ToString();
            File.AppendAllText(LogPath, entry);
        }
        catch
        {
            // Logging must never cause startup to fail.
        }
    }
}
