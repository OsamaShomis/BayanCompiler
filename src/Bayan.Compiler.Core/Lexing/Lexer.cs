using BayanCompiler.Frontend;

namespace BayanCompiler.Lexing;

/// <summary>
/// يحول النص المصدر للغة بيان إلى قائمة من الرموز Tokens مع تشخيصات الأخطاء اللغوية.
/// </summary>
public sealed class Lexer
{
    /*
    // Preserves the first project vocabulary for archival tests without activating it in official lexing.
    private static readonly Dictionary<string, TokenType> LegacyKeywords = new(StringComparer.Ordinal)
    {
        ["برنامج"] = TokenType.Program,
        ["نهاية"] = TokenType.End,
        ["دع"] = TokenType.Let,
        ["إذا"] = TokenType.If,
        ["وإلا"] = TokenType.Else,
        ["طالما"] = TokenType.While,
        ["اطبع"] = TokenType.Print,
        ["عدد"] = TokenType.TypeInt,
        ["حقيقي"] = TokenType.TypeReal,
        ["منطقي"] = TokenType.TypeBool,
        ["نص"] = TokenType.TypeText,
    };
    */

    // Maps only the vocabulary explicitly required by the doctor specification.
    private static readonly Dictionary<string, TokenType> Keywords = new(StringComparer.Ordinal)
    {
        ["برنامج"] = TokenType.Program,
        ["إذا"] = TokenType.If,
        ["وإلا"] = TokenType.Else,
        ["طالما"] = TokenType.While,
        ["اطبع"] = TokenType.Print,
        ["صحيح"] = TokenType.TypeInt,
        ["حقيقي"] = TokenType.TypeReal,
        ["منطقي"] = TokenType.TypeBool,
        ["خطأ"] = TokenType.False,

        // Doctor-specification declarations and composite type vocabulary.
        ["ثابت"] = TokenType.Const,
        ["نوع"] = TokenType.TypeDeclaration,
        ["متغير"] = TokenType.Variable,
        ["إجراء"] = TokenType.Procedure,
        ["اجراء"] = TokenType.Procedure,
        ["بالقيمة"] = TokenType.ByValue,
        ["بالمرجع"] = TokenType.ByReference,
        ["قائمة"] = TokenType.List,
        ["من"] = TokenType.From,
        ["سجل"] = TokenType.Record,

        // Doctor-specification commands and control-flow vocabulary.
        ["اقرأ"] = TokenType.Read,
        ["اقرا"] = TokenType.Read,
        ["فإن"] = TokenType.Then,
        ["فان"] = TokenType.Then,
        ["كرر"] = TokenType.Repeat,
        ["إلى"] = TokenType.To,
        ["الى"] = TokenType.To,
        ["أضف"] = TokenType.Step,
        ["اضف"] = TokenType.Step,
        ["استمر"] = TokenType.Continue,
        ["أعد"] = TokenType.RepeatUntil,
        ["اعد"] = TokenType.RepeatUntil,
        ["حتى"] = TokenType.Until,

        // Doctor-specification literals and primitive type names.
        ["صح"] = TokenType.True,
        ["حرفي"] = TokenType.TypeChar,
        ["خيط"] = TokenType.TypeString,
        ["رمزي"] = TokenType.TypeStringQualifier
    };

    // يحتفظ بالنص الذي أرسله محرر Windows Forms أو الملف المفتوح.
    private readonly string _source;

    // يحتفظ بالرموز الناتجة لإعادتها إلى الواجهة أو Parser لاحقاً.
    private readonly List<Token> _tokens = new();

    // يجمع الأخطاء التي تظهر في تبويب الأخطاء بالواجهة.
    private readonly DiagnosticBag _diagnostics = new();

    // يحدد الفهرس الحالي داخل النص المصدر.
    private int _position;

    // يحتفظ برقم السطر الحالي للرسائل العربية.
    private int _line = 1;

    // يحتفظ برقم العمود الحالي للرسائل العربية.
    private int _column = 1;

    /// <summary>
    /// ينشئ محللاً لغوياً لنص لغة بيان.
    /// </summary>
    public Lexer(string source)
    {
        // يحفظ النص؛ القيمة الفارغة آمنة عند إنشاء ملف جديد في الواجهة.
        _source = source ?? string.Empty;
    }

    /// <summary>
    /// ينفذ التحليل اللغوي ويعيد الرموز والتشخيصات معاً.
    /// </summary>
    public LexResult Lex()
    {
        // يقرأ النص حتى نهايته، ويعالج رمزاً واحداً أو مجموعة متجانسة في كل دورة.
        while (!IsAtEnd)
        {
            ScanToken();
        }

        // يضيف رمز نهاية الملف حتى يعرف Parser لاحقاً أن الإدخال انتهى.
        _tokens.Add(new Token(TokenType.Eof, string.Empty, CreateSpan(_position, 0, _line, _column)));

        // يعيد نتيجة موحدة تستطيع الواجهة عرضها مباشرة.
        return new LexResult(_tokens, _diagnostics.Items);
    }

    /// <summary>
    /// يتعرف على الرمز التالي في المصدر، أو يتجاهل المسافات والتعليقات.
    /// </summary>
    private void ScanToken()
    {
        // يحفظ بداية الرمز وموقعه قبل استهلاك أي حرف.
        var start = _position;
        var startLine = _line;
        var startColumn = _column;

        // يستهلك الحرف الحالي ويتقدم بالمؤشر والموقع.
        var current = Advance();

        // يعالج المحارف البسيطة التي تقابل Token واحداً مباشرة.
        switch (current)
        {
            case ' ':
            case '\t':
            case '\r':
                return;

            case '\n':
                return;

            case '(':
                AddToken(TokenType.LeftParen, start, startLine, startColumn);
                return;

            case ')':
                AddToken(TokenType.RightParen, start, startLine, startColumn);
                return;

            case '{':
                AddToken(TokenType.LeftBrace, start, startLine, startColumn);
                return;

            case '}':
                AddToken(TokenType.RightBrace, start, startLine, startColumn);
                return;

            case '[':
                AddToken(TokenType.LeftBracket, start, startLine, startColumn);
                return;

            case ']':
                AddToken(TokenType.RightBracket, start, startLine, startColumn);
                return;

            case ':':
                AddToken(TokenType.Colon, start, startLine, startColumn);
                return;

            case ';':
            case '؛':
                AddToken(TokenType.Semicolon, start, startLine, startColumn);
                return;

            case ',':
            case '،':
                AddToken(TokenType.Comma, start, startLine, startColumn);
                return;

            case '.':
                AddToken(TokenType.Dot, start, startLine, startColumn);
                return;

            case '+':
                AddToken(TokenType.Plus, start, startLine, startColumn);
                return;

            case '-':
                AddToken(TokenType.Minus, start, startLine, startColumn);
                return;

            case '*':
                AddToken(TokenType.Star, start, startLine, startColumn);
                return;

            case '%':
                AddToken(TokenType.Percent, start, startLine, startColumn);
                return;

            case '^':
                AddToken(TokenType.Caret, start, startLine, startColumn);
                return;

            case '\\':
                AddToken(TokenType.Backslash, start, startLine, startColumn);
                return;

            case '←':
                AddToken(TokenType.Assign, start, startLine, startColumn);
                return;

            case '=':
                if (Match('='))
                {
                    AddToken(TokenType.EqualEqual, start, startLine, startColumn);
                }
                else if (Match('>'))
                {
                    AddToken(TokenType.LessEqual, start, startLine, startColumn);
                }
                else if (Match('<'))
                {
                    AddToken(TokenType.GreaterEqual, start, startLine, startColumn);
                }
                else if (Match('!'))
                {
                    AddToken(TokenType.BangEqual, start, startLine, startColumn);
                }
                else
                {
                    AddToken(TokenType.Assign, start, startLine, startColumn);
                }

                return;

            case '!':
                AddToken(Match('=') ? TokenType.BangEqual : TokenType.Bang, start, startLine, startColumn);
                return;

            case '<':
                AddToken(Match('=') ? TokenType.LessEqual : TokenType.Greater, start, startLine, startColumn);
                return;

            case '>':
                AddToken(Match('=') ? TokenType.GreaterEqual : TokenType.Less, start, startLine, startColumn);
                return;

            case '&':
                if (Match('&'))
                {
                    AddToken(TokenType.AndAnd, start, startLine, startColumn);
                }
                else
                {
                    ReportUnexpectedCharacter(start, startLine, startColumn, current);
                }

                return;

            case '|':
                if (Match('|'))
                {
                    AddToken(TokenType.OrOr, start, startLine, startColumn);
                }
                else
                {
                    ReportUnexpectedCharacter(start, startLine, startColumn, current);
                }

                return;

            case '/':
                if (Match('/'))
                {
                    SkipComment();
                }
                else if (Match('*'))
                {
                    SkipBlockComment(start, startLine, startColumn);
                }
                else
                {
                    AddToken(TokenType.Slash, start, startLine, startColumn);
                }

                return;

            case '"':
                ScanString(start, startLine, startColumn);
                return;

            case '\'':
            case '’':
                ScanCharacter(start, startLine, startColumn, current);
                return;
        }

        // يوجه الأرقام إلى قارئ الأعداد قبل معالجة المعرفات.
        if (IsAsciiDigit(current))
        {
            ScanNumber(start, startLine, startColumn);
            return;
        }

        // يوجه الحروف العربية والشرطة السفلية إلى قارئ المعرفات والكلمات المحجوزة.
        if (IsIdentifierStart(current))
        {
            ScanIdentifier(start, startLine, startColumn);
            return;
        }

        // يسجل أي محرف لا تنص عليه مواصفات اللغة كخطأ لغوي واضح.
        ReportUnexpectedCharacter(start, startLine, startColumn, current);
    }

    /// <summary>
    /// يتجاوز أحرف التعليق حتى نهاية السطر من دون إنتاج Tokens.
    /// </summary>
    private void SkipComment()
    {
        // يتوقف قبل \n كي تعالجه Advance وتحدث رقم السطر بشكل موحد.
        while (!IsAtEnd && Peek() != '\n')
        {
            Advance();
        }
    }

    /// <summary>
    /// يتجاوز التعليق الكتلي المتعدد الأسطر /* ... */.
    /// </summary>
    private void SkipBlockComment(int start, int startLine, int startColumn)
    {
        while (!IsAtEnd)
        {
            if (Peek() == '*' && PeekNext() == '/')
            {
                Advance(); // '*'
                Advance(); // '/'
                return;
            }

            Advance();
        }

        _diagnostics.Report("LEX004", "تعليق كتلي غير مغلق.", CreateSpan(start, _position - start, startLine, startColumn));
    }

    /// <summary>
    /// يقرأ ثابتاً نصياً بين علامتي اقتباس مزدوجتين مع دعم محارف الهروب.
    /// </summary>
    private void ScanString(int start, int startLine, int startColumn)
    {
        var sb = new System.Text.StringBuilder();

        while (!IsAtEnd && Peek() != '"')
        {
            if (Peek() == '\\')
            {
                Advance(); // '\'
                if (IsAtEnd) break;
                char escaped = Advance();
                switch (escaped)
                {
                    case 'n': sb.Append('\n'); break;
                    case 't': sb.Append('\t'); break;
                    case 'r': sb.Append('\r'); break;
                    case '\\': sb.Append('\\'); break;
                    case '"': sb.Append('"'); break;
                    default: sb.Append(escaped); break;
                }
            }
            else
            {
                sb.Append(Advance());
            }
        }

        if (IsAtEnd)
        {
            _diagnostics.Report("LEX002", "سلسلة نصية غير مغلقة.", CreateSpan(start, _position - start, startLine, startColumn));
            return;
        }

        Advance(); // '"'
        var span = CreateSpan(start, _position - start, startLine, startColumn);
        _tokens.Add(new Token(TokenType.String, sb.ToString(), span));
    }

    /// <summary>
    /// Scans a single character literal delimited by an ASCII or Arabic single quote with escape support.
    /// </summary>
    private void ScanCharacter(int start, int startLine, int startColumn, char delimiter)
    {
        if (IsAtEnd || Peek() == '\n' || Peek() == delimiter)
        {
            _diagnostics.Report("LEX003", "قيمة الحرف يجب أن تحتوي رمزاً واحداً بين علامتي اقتباس مفردتين.", CreateSpan(start, _position - start, startLine, startColumn));
            return;
        }

        char value;
        if (Peek() == '\\')
        {
            Advance(); // '\'
            if (IsAtEnd)
            {
                _diagnostics.Report("LEX003", "قيمة الحرف يجب أن تنتهي بعلامة الاقتباس المطابقة.", CreateSpan(start, _position - start, startLine, startColumn));
                return;
            }
            char escaped = Advance();
            value = escaped switch
            {
                'n' => '\n',
                't' => '\t',
                'r' => '\r',
                '\\' => '\\',
                '\'' => '\'',
                _ => escaped
            };
        }
        else
        {
            value = Advance();
        }

        if (IsAtEnd || Peek() != delimiter)
        {
            _diagnostics.Report("LEX003", "قيمة الحرف يجب أن تنتهي بعلامة الاقتباس المطابقة.", CreateSpan(start, _position - start, startLine, startColumn));
            return;
        }

        Advance(); // delimiter
        _tokens.Add(new Token(TokenType.Character, value.ToString(), CreateSpan(start, _position - start, startLine, startColumn)));
    }

    /// <summary>
    /// يقرأ عدداً صحيحاً أو عشرياً مكتوباً بأرقام لاتينية.
    /// </summary>
    private void ScanNumber(int start, int startLine, int startColumn)
    {
        // يستهلك بقية الجزء الصحيح من العدد.
        while (IsAsciiDigit(Peek()))
        {
            Advance();
        }

        // يتعرف على الجزء العشري فقط إذا أتت نقطة يتبعها رقم.
        if (Peek() == '.' && IsAsciiDigit(PeekNext()))
        {
            Advance();

            // يستهلك الأرقام بعد النقطة العشرية.
            while (IsAsciiDigit(Peek()))
            {
                Advance();
            }

            AddToken(TokenType.Real, start, startLine, startColumn);
            return;
        }

        // يضيف العدد الصحيح عند عدم وجود جزء عشري صحيح.
        AddToken(TokenType.Integer, start, startLine, startColumn);
    }

    /// <summary>
    /// يقرأ معرفاً ثم يميّز بين الاسم العادي والكلمة المحجوزة.
    /// </summary>
    private void ScanIdentifier(int start, int startLine, int startColumn)
    {
        // يستهلك الحروف العربية والأرقام اللاتينية والشرطة السفلية المسموح بها داخل الاسم.
        while (IsIdentifierPart(Peek()))
        {
            Advance();
        }

        // يستخرج النص الكامل للمعرف أو الكلمة المحجوزة.
        var text = _source.Substring(start, _position - start);

        // يختار نوع الكلمة المحجوزة إن وجدت، أو يعامل النص كمعرف عادي.
        var type = Keywords.TryGetValue(text, out var keywordType) ? keywordType : TokenType.Identifier;

        // يضيف الرمز وموقعه إلى النتيجة.
        AddToken(type, start, startLine, startColumn);
    }

    /// <summary>
    /// يضيف Token مستخرجاً من النص الأصلي إلى قائمة النتيجة.
    /// </summary>
    private void AddToken(TokenType type, int start, int startLine, int startColumn)
    {
        // يستخرج النص الأصلي الذي أنتج هذا الرمز.
        var lexeme = _source.Substring(start, _position - start);

        // يربط النص بالنوع والموقع ليستخدمهما Form وParser لاحقاً.
        _tokens.Add(new Token(type, lexeme, CreateSpan(start, _position - start, startLine, startColumn)));
    }

    /// <summary>
    /// يسجل خطأ لمحرف لا تقبله مواصفات لغة بيان.
    /// </summary>
    private void ReportUnexpectedCharacter(int start, int line, int column, char character)
    {
        // يضيف رسالة تفيد بالمحرف المخالف وموقعه للمستخدم العربي.
        _diagnostics.Report("LEX001", $"المحرف '{character}' غير مسموح في لغة بيان.", CreateSpan(start, 1, line, column));
    }

    /// <summary>
    /// يستهلك الحرف التالي إذا كان مطابقاً للقيمة المتوقعة.
    /// </summary>
    private bool Match(char expected)
    {
        // يفشل المطابقة عند نهاية النص أو عند اختلاف الحرف التالي.
        if (IsAtEnd || Peek() != expected)
        {
            return false;
        }

        // يستهلك الحرف المطابق ويبلغ المتصل بالنجاح.
        Advance();
        return true;
    }

    /// <summary>
    /// يعيد الحرف الحالي من دون استهلاكه، أو \0 عند نهاية النص.
    /// </summary>
    private char Peek()
    {
        // يحمي الفهرس من تجاوز طول المصدر.
        return IsAtEnd ? '\0' : _source[_position];
    }

    /// <summary>
    /// يعيد الحرف الذي يلي الحرف الحالي من دون استهلاك، أو \0 إن لم يوجد.
    /// </summary>
    private char PeekNext()
    {
        // يحمي القراءة من تجاوز نهاية النص بموقعين.
        return _position + 1 >= _source.Length ? '\0' : _source[_position + 1];
    }

    /// <summary>
    /// يستهلك الحرف الحالي ويحدث موضع السطر والعمود.
    /// </summary>
    private char Advance()
    {
        // يقرأ الحرف ثم ينتقل إلى الموضع التالي في النص.
        var current = _source[_position++];

        // يعيد ضبط العمود ويزيد السطر عند نهاية السطر.
        if (current == '\n')
        {
            _line++;
            _column = 1;
        }
        else
        {
            _column++;
        }

        // يعيد الحرف الذي استهلك للتعرف عليه.
        return current;
    }

    /// <summary>
    /// ينشئ موقعاً موحداً لرمز أو خطأ.
    /// </summary>
    private static SourceSpan CreateSpan(int start, int length, int line, int column)
    {
        // يوحد إنشاء المواقع ليستعمله Token وDiagnostic بالطريقة نفسها.
        return new SourceSpan(start, length, line, column);
    }

    /// <summary>
    /// يحدد ما إذا وصل القارئ إلى نهاية النص المصدر.
    /// </summary>
    private bool IsAtEnd => _position >= _source.Length;

    /// <summary>
    /// يتحقق من أن المحرف رقم لاتيني مسموح داخل الأعداد والمعرفات.
    /// </summary>
    private static bool IsAsciiDigit(char character)
    {
        // يقبل نطاق الأرقام اللاتينية فقط وفق مواصفات الإصدار 1.0.
        return character is >= '0' and <= '9';
    }

    /// <summary>
    /// يتحقق من بداية معرف عربي أو شرطة سفلية.
    /// </summary>
    private static bool IsIdentifierStart(char character)
    {
        // يقبل الحرف العربي أو الشرطة السفلية في أول الاسم.
        return character == '_' || IsArabicLetter(character);
    }

    /// <summary>
    /// يتحقق من محرف مسموح داخل المعرف بعد أول حرف.
    /// </summary>
    private static bool IsIdentifierPart(char character)
    {
        // يقبل الحروف العربية والأرقام اللاتينية والشرطة السفلية داخل الاسم.
        return IsIdentifierStart(character) || IsAsciiDigit(character);
    }

    /// <summary>
    /// يتحقق من أن المحرف حرف عربي غير مشكّل ضمن نطاق العربية الأساسي.
    /// </summary>
    private static bool IsArabicLetter(char character)
    {
        // يستبعد التشكيل لأنه ليس حرفاً وفق سياسة الإصدار 1.0.
        return char.IsLetter(character) && character is >= '\u0621' and <= '\u06FF';
    }
}

/// <summary>
/// يجمع ناتج التحليل اللغوي لعرضه في Windows Forms أو تمريره إلى Parser لاحقاً.
/// </summary>
public sealed record LexResult(IReadOnlyList<Token> Tokens, IReadOnlyList<Diagnostic> Diagnostics);
