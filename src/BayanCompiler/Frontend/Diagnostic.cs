namespace BayanCompiler.Frontend;

/// <summary>
/// يمثل رسالة تشخيص واحدة ناتجة عن مرحلة من مراحل المترجم.
/// </summary>
public sealed record Diagnostic(string Code, string Message, SourceSpan Span)
{
    /// <summary>
    /// يعيد نصاً جاهزاً للعرض في تبويب الأخطاء داخل Windows Forms.
    /// </summary>
    public override string ToString()
    {
        // يربط رمز الخطأ وموقعه برسالة عربية موجزة للمستخدم.
        return $"[{Code}] {Span}: {Message}";
    }
}
