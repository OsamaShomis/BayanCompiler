namespace BayanCompiler.Intermediate;

/// <summary>
/// يحدد أنواع تعليمات التمثيل الوسيط Three-Address Code للغة بيان.
/// </summary>
public enum IrOpcode
{
    // يخزن قيمة في متغير أو قيمة مؤقتة.
    Assign,

    // ينفذ عملية حسابية أو مقارنة أو عملية منطقية بين قيمتين.
    Binary,

    // ينفذ عملية أحادية مثل السالب أو النفي المنطقي.
    Unary,

    // يضع علامة يمكن أن تقفز إليها الفروع والحلقات.
    Label,

    // يقفز إلى علامة من دون شرط.
    Goto,

    // يقفز إلى علامة عندما تكون قيمة الشرط صفراً أو خطأ.
    IfZeroGoto,

    // يطبع عدداً صحيحاً أو قيمة منطقية ممثلة بعدد.
    PrintInt,

    // يطبع نصاً محفوظاً في قسم البيانات.
    PrintString,

    // Reads an integer from standard input into a writable storage value.
    ReadInt,

    // Transfers control to a generated procedure entry label.
    Call,

    // Returns from the current generated procedure to its caller.
    Return,

    // Prints one character value through the MIPS character output syscall.
    PrintChar,

    // Reads one character value through the MIPS character input syscall.
    ReadChar,

    // Loads one scalar word from a list element or record field offset.
    LoadIndexed,

    // Stores one scalar word into a list element or record field offset.
    StoreIndexed,

    // Prints a single-precision floating-point value.
    PrintReal,

    // Reads a single-precision floating-point value.
    ReadReal,

    // Reads a bounded string into an allocated MIPS byte buffer and stores its address.
    ReadString
}
