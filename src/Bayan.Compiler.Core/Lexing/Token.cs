using BayanCompiler.Frontend;

namespace BayanCompiler.Lexing;

/// <summary>
/// يمثل رمزاً واحداً أخرجه المحلل اللغوي من النص المصدر.
/// </summary>
public sealed record Token(TokenType Type, string Lexeme, SourceSpan Span)
{
    /// <summary>
    /// يعيد تمثيلاً نصياً مناسباً لتبويب Tokens في الواجهة.
    /// </summary>
    public override string ToString()
    {
        // يضم نوع الرمز والنص الأصلي وموقعه في سطر واحد قابل للقراءة.
        return $"{Type,-14} | {Lexeme} | {Span}";
    }
}
