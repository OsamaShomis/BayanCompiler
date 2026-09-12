namespace BayanCompiler.Frontend;

/// <summary>
/// يحدد موقع جزء من النص المصدر داخل ملف لغة بيان.
/// </summary>
public readonly record struct SourceSpan(int Start, int Length, int Line, int Column)
{
    /// <summary>
    /// يعيد موضعاً نصياً مناسباً للعرض في واجهة Windows Forms.
    /// </summary>
    public override string ToString()
    {
        // ينسق الموقع ليستخدم في رسائل الأخطاء وقائمة Tokens.
        return $"سطر {Line}، عمود {Column}";
    }
}
