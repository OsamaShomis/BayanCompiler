using BayanCompiler.Forms;

namespace BayanCompiler;

/// <summary>
/// تمثل نقطة دخول تطبيق Windows Forms لمحرر لغة بيان.
/// </summary>
internal static class Program
{
    /// <summary>
    /// يجهز إعدادات Windows Forms ثم يعرض واجهة محرر المترجم الرئيسية مباشرة.
    /// </summary>
    [STAThread]
    private static void Main()
    {
        ApplicationConfiguration.Initialize();

        Application.SetUnhandledExceptionMode(UnhandledExceptionMode.CatchException);
        AppDomain.CurrentDomain.UnhandledException += (_, e) =>
        {
            LogCrash(e.ExceptionObject as Exception);
        };
        Application.ThreadException += (_, e) =>
        {
            LogCrash(e.Exception);
        };

        try
        {
            Application.Run(new CompilerJourneyForm());
        }
        catch (Exception ex)
        {
            LogCrash(ex);
            MessageBox.Show("حدث خطأ أثناء تشغيل المحرر:\n" + ex.Message, "خطأ", MessageBoxButtons.OK, MessageBoxIcon.Error);
        }
    }

    private static void LogCrash(Exception? ex)
    {
        if (ex == null) return;
        try
        {
            string logPath = Path.Combine(AppContext.BaseDirectory, "crash.log");
            File.AppendAllText(logPath, $"[{DateTime.Now}] {ex}\n\n");
        }
        catch { }
    }
}
