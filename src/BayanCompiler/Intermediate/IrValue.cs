using BayanCompiler.Semantics;

namespace BayanCompiler.Intermediate;

/// <summary>
/// يحدد طبيعة القيمة المستخدمة داخل تعليمة IR.
/// </summary>
public enum IrValueKind
{
    // يمثل متغيراً محفوظاً في الذاكرة داخل قسم البيانات.
    Variable,

    // يمثل عدداً صحيحاً مباشراً مثل 5 أو -1.
    Immediate,

    // يمثل قيمة مؤقتة مثل t1 تحفظ في الذاكرة.
    Temporary,

    // يمثل عنوان نص معرف في قسم البيانات.
    StringLabel,

    // Represents an addressable single-precision floating-point constant in the data section.
    FloatLabel,

    // يمثل اسم علامة للقفز داخل قسم النص.
    Label
}

/// <summary>
/// يمثل معاملاً أو نتيجة داخل Three-Address Code.
/// </summary>
public sealed record IrValue(
    IrValueKind Kind,
    string Name,
    LanguageType Type,
    string? SourceName = null)
{
    /// <summary>
    /// يعرض القيمة بصيغة واضحة عند طباعة IR داخل الواجهة.
    /// </summary>
    public string DisplayName => string.IsNullOrWhiteSpace(SourceName) ? Name : SourceName;

    /// <summary>
    /// يعرض القيمة بصيغة واضحة عند طباعة IR داخل الواجهة.
    /// </summary>
    public override string ToString()
    {
        // Keep source names visible in IR while MIPS keeps the safe internal Name.
        return Kind is IrValueKind.StringLabel or IrValueKind.FloatLabel ? "&" + Name : DisplayName;
    }
}
