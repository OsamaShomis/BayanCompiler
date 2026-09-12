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
        Application.Run(new CompilerJourneyForm());
    }
}
