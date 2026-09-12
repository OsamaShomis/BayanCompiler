namespace BayanCompiler.Frontend;

/// <summary>
/// تجمع رسائل الأخطاء الناتجة عن مرحلة واحدة من المترجم.
/// </summary>
public sealed class DiagnosticBag
{
    // تحفظ قائمة الأخطاء التي ستعرض لاحقاً في تبويب الأخطاء.
    private readonly List<Diagnostic> _items = new List<Diagnostic>();

    /// <summary>
    /// يعرض التشخيصات للواجهة أو للمرحلة التالية بدون السماح بتعديل القائمة داخلياً.
    /// </summary>
    public IReadOnlyList<Diagnostic> Items => _items;

    /// <summary>
    /// يحدد ما إذا كانت المرحلة سجلت خطأ واحداً على الأقل.
    /// </summary>
    public bool HasErrors => _items.Count > 0;

    /// <summary>
    /// يضيف خطأ إلى القائمة مع رمز ورسالة وموقع في النص المصدر.
    /// </summary>
    public void Report(string code, string message, SourceSpan span)
    {
        // يربط بيانات الخطأ بكائن Diagnostic موحد تفهمه واجهة المستخدم.
        _items.Add(new Diagnostic(code, message, span));
    }
}
