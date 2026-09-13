namespace BayanCompiler.Lexing;

/// <summary>
/// يعرّف كل الرموز التي يستطيع المحلل اللغوي إنتاجها في لغة بيان الإصدار 1.0.
/// </summary>
public enum TokenType
{
    // نهاية النص المصدر.
    Eof,

    // معرفات وثوابت.
    Identifier,
    Integer,
    Real,
    String,

    // كلمات بداية ونهاية البرنامج.
    Program,
    End,

    // كلمات العبارات.
    Let,
    If,
    Else,
    While,
    Print,

    // كلمات الأنواع والقيم المنطقية.
    TypeInt,
    TypeReal,
    TypeBool,
    TypeText,
    True,
    False,

    // المعاملات الحسابية والمنطقية والمقارنة.
    Plus,
    Minus,
    Star,
    Slash,
    Percent,
    Assign,
    EqualEqual,
    Bang,
    BangEqual,
    Less,
    LessEqual,
    Greater,
    GreaterEqual,
    AndAnd,
    OrOr,

    // علامات الترقيم وبناء الكتل.
    LeftParen,
    RightParen,
    LeftBrace,
    RightBrace,
    Colon,
    Semicolon,

    // Doctor-specification literals and punctuation that do not exist in the first language version.
    Character,
    Comma,
    Dot,
    LeftBracket,
    RightBracket,
    Caret,
    Backslash,

    // Doctor-specification declaration and procedure keywords.
    Const,
    TypeDeclaration,
    Variable,
    Procedure,
    ByValue,
    ByReference,
    List,
    From,
    Record,

    // Doctor-specification input, branching, and loop keywords.
    Read,
    Then,
    Repeat,
    To,
    Step,
    Continue,
    RepeatUntil,
    Until,

    // Doctor-specification primitive type keywords.
    TypeChar,
    TypeString,
    TypeStringQualifier
}
