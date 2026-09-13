using System.Text;
using BayanCompiler.Lexing;

namespace BayanCompiler.Diagnostics;

/// <summary>
/// ينسق قائمة Tokens كنص واضح لعرضه في Windows Forms.
/// </summary>
public static class TokenPrinter
{
    /// <summary>
    /// يحول الرموز إلى جدول نصي منظم.
    /// </summary>
    public static string Format(IEnumerable<Token> tokens)
    {
        // يجمع الأسطر بكفاءة قبل عرضها في RichTextBox.
        var builder = new StringBuilder();

        // يضيف رأس الأعمدة لتسهيل قراءة النتيجة أثناء العرض أمام الدكتور.
        builder.AppendLine("النوع           | النص               | الموقع");
        builder.AppendLine(new string('-', 64));

        // يضيف كل Token في سطر مستقل مع نوعه ونصه وموقعه.
        foreach (var token in tokens)
        {
            builder.AppendLine(token.ToString());
        }

        // يعيد النص النهائي لترتبط به واجهة Windows Forms.
        return builder.ToString();
    }
}
