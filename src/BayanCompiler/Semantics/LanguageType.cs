namespace BayanCompiler.Semantics;

/// <summary>
/// يحدد الأنواع الداخلية التي يفهمها التحليل الدلالي للغة بيان.
/// </summary>
public enum LanguageType
{
    // يمثل قيمة لا يمكن تحديد نوعها بسبب خطأ سابق.
    Unknown,

    // يمثل النوع عدد.
    Int,

    // يمثل النوع حقيقي.
    Real,

    // يمثل النوع منطقي.
    Bool,

    // يمثل النوع نص.
    Text,

    // Represents one character literal delimited by single quotes.
    Char,

    // Represents a fixed-size list declared by the official language grammar.
    List,

    // Represents a named record type declared by the official language grammar.
    Record,

    // Represents a procedure symbol with formal parameters.
    Procedure,

    // يمثل نوعاً بلا قيمة؛ محفوظ للتوسعات المستقبلية.
    Void
}
