using BayanCompiler.Frontend;
using BayanCompiler.Lexing;
using BayanCompiler.Syntax;

namespace BayanCompiler.Parsing;

/// <summary>
/// يحول Tokens إلى شجرة AST ويتحقق من القواعد النحوية للنسخة الحالية من لغة بيان.
/// </summary>
public sealed class Parser
{
    // يحتفظ بالرموز التي أنتجها Lexer؛ لا يقرأ Parser النص الخام مباشرة.
    private readonly IReadOnlyList<Token> _tokens;

    // يجمع الأخطاء النحوية لعرضها في تبويب الأخطاء داخل Windows Forms.
    private readonly DiagnosticBag _diagnostics = new DiagnosticBag();

    // يحدد الرمز الحالي في قائمة Tokens.
    private int _position;

    /// <summary>
    /// ينشئ محللاً نحوياً لقائمة Tokens يجب أن تنتهي دائماً بالرمز Eof.
    /// </summary>
    public Parser(IReadOnlyList<Token> tokens)
    {
        // يربط Parser بقائمة Tokens الناتجة من Lexer.
        _tokens = tokens;
    }

    /// <summary>
    /// يحلل ملف البرنامج كاملاً ويعيد شجرة AST والتشخيصات.
    /// </summary>
    public ParseResult Parse()
    {
        // يبدأ من أعلى قاعدة في اللغة: برنامج اسم {تعليمات} نهاية.
        ProgramNode program = ParseProgram();

        // يعيد النتيجة الموحدة إلى واجهة Windows Forms.
        return new ParseResult(program, _diagnostics.Items);
    }

    /// <summary>
    /// يحلل بداية البرنامج واسمه والتعليمات ثم كلمة نهاية.
    /// </summary>
    private ProgramNode ParseProgram()
    {
        // يتوقع كلمة برنامج في بداية الملف.
        Token programKeyword = Consume(TokenType.Program, "متوقع الكلمة \"برنامج\" في بداية الملف.");

        // يتوقع اسم البرنامج بعد كلمة برنامج.
        Token name = Consume(TokenType.Identifier, "متوقع اسم للبرنامج بعد الكلمة \"برنامج\".");

        // يجمع تعليمات المستوى الأعلى في AST.
        List<StatementNode> statements = new List<StatementNode>();

        // يستمر حتى كلمة نهاية أو نهاية الملف.
        while (!Check(TokenType.End) && !IsAtEnd)
        {
            // يحلل تعليمة واحدة في كل دورة.
            StatementNode? statement = ParseStatement();

            // يضيف العقدة فقط إذا حُللت بنجاح.
            if (statement is not null)
            {
                statements.Add(statement);
            }
            else
            {
                // يسترد التحليل بعد التعليمة الخاطئة.
                Synchronize();
            }
        }

        // يتوقع كلمة نهاية لإغلاق البرنامج.
        Token end = Consume(TokenType.End, "متوقع الكلمة \"نهاية\" لإنهاء البرنامج.");

        // ينشئ العقدة العليا للبرنامج.
        return new ProgramNode(name.Lexeme, statements, Combine(programKeyword.Span, end.Span));
    }

    /// <summary>
    /// يوجه رمز بداية التعليمة إلى دالة التحليل الخاصة بها.
    /// </summary>
    private StatementNode? ParseStatement()
    {
        // يحلل تعريف المتغير الذي يبدأ بكلمة دع.
        if (Match(TokenType.Let))
        {
            return ParseVarDeclaration(Previous);
        }

        // يحلل أمر الطباعة الذي يبدأ بكلمة اطبع.
        if (Match(TokenType.Print))
        {
            return ParsePrint(Previous);
        }

        // يحلل شرط إذا مع فرع وإلا اختياري.
        if (Match(TokenType.If))
        {
            return ParseIf(Previous);
        }

        // يحلل حلقة طالما.
        if (Match(TokenType.While))
        {
            return ParseWhile(Previous);
        }

        // يميز الإسناد من بداية معرف متبوع بعلامة الإسناد.
        if (Check(TokenType.Identifier) && Peek(1).Type == TokenType.Assign)
        {
            return ParseAssignment();
        }

        // يبلغ عن بداية تعليمة غير مدعومة أو غير صحيحة.
        Report("SYN002", "متوقع تعليمة تبدأ بـ \"دع\" أو \"اطبع\" أو \"إذا\" أو \"طالما\" أو اسم متغير للإسناد.", Current.Span);

        // يعيد null لكي تبدأ آلية استرداد الخطأ.
        return null;
    }

    /// <summary>
    /// يحلل تعريف متغير مثل: دع س : عدد = 5؛
    /// </summary>
    private StatementNode ParseVarDeclaration(Token letKeyword)
    {
        // يتوقع اسم المتغير بعد دع.
        Token name = Consume(TokenType.Identifier, "متوقع اسم متغير بعد الكلمة \"دع\".");

        // يتوقع النقطتين قبل نوع البيانات.
        Consume(TokenType.Colon, "متوقع الرمز \":\" بعد اسم المتغير.");

        // يقرأ نوع البيانات المعتمد في مواصفات اللغة.
        Token type = ConsumeType();

        // يقرأ التعبير الابتدائي فقط إذا وجدت علامة الإسناد.
        ExpressionNode? initializer = null;
        if (Match(TokenType.Assign))
        {
            // يربط قيمة التهيئة بعقدة تعريف المتغير.
            initializer = ParseExpression();
        }

        // يتأكد من وجود فاصلة منقوطة في نهاية التعريف.
        Token semicolon = Consume(TokenType.Semicolon, "متوقع الرمز \"؛\" بعد تعريف المتغير.");

        // ينشئ عقدة التعريف التي ستفحصها الدلالة لاحقاً.
        return new VarDeclarationNode(name.Lexeme, type.Lexeme, initializer, Combine(letKeyword.Span, semicolon.Span));
    }

    /// <summary>
    /// يحلل الإسناد مثل: مجموع ← مجموع + س؛
    /// </summary>
    private StatementNode ParseAssignment()
    {
        // يستهلك اسم المتغير المستهدف في الإسناد.
        Token name = Consume(TokenType.Identifier, "متوقع اسم المتغير قبل الإسناد.");

        // يستهلك علامة الإسناد ← أو =.
        Consume(TokenType.Assign, "متوقع علامة الإسناد \"←\" أو \"=\" بعد اسم المتغير.");

        // يحلل التعبير الذي سيصبح القيمة الجديدة للمتغير.
        ExpressionNode expression = ParseExpression();

        // يتأكد من إنهاء الإسناد بفاصلة منقوطة.
        Token semicolon = Consume(TokenType.Semicolon, "متوقع الرمز \"؛\" بعد الإسناد.");

        // ينشئ عقدة الإسناد داخل AST.
        return new AssignmentNode(name.Lexeme, expression, Combine(name.Span, semicolon.Span));
    }

    /// <summary>
    /// يحلل الطباعة مثل: اطبع(س + 1)؛
    /// </summary>
    private StatementNode ParsePrint(Token printKeyword)
    {
        // يتوقع القوس الافتتاحي بعد اطبع.
        Consume(TokenType.LeftParen, "متوقع الرمز \"(\" بعد الكلمة \"اطبع\".");

        // يحلل التعبير داخل القوسين.
        ExpressionNode expression = ParseExpression();

        // يتوقع القوس الختامي بعد التعبير.
        Consume(TokenType.RightParen, "متوقع الرمز \")\" بعد تعبير الطباعة.");

        // يتوقع الفاصلة المنقوطة في نهاية الطباعة.
        Token semicolon = Consume(TokenType.Semicolon, "متوقع الرمز \"؛\" بعد تعليمة الطباعة.");

        // ينشئ عقدة الطباعة داخل AST.
        return new PrintNode(expression, Combine(printKeyword.Span, semicolon.Span));
    }

    /// <summary>
    /// يحلل شرط إذا مثل: إذا (س > 3) { ... } وإلا { ... }
    /// </summary>
    private StatementNode ParseIf(Token ifKeyword)
    {
        // يتوقع القوس الافتتاحي قبل الشرط.
        Consume(TokenType.LeftParen, "متوقع الرمز \"(\" بعد الكلمة \"إذا\".");

        // يحلل التعبير المنطقي للشرط.
        ExpressionNode condition = ParseExpression();

        // يتوقع القوس الختامي بعد الشرط.
        Consume(TokenType.RightParen, "متوقع الرمز \")\" بعد شرط إذا.");

        // يحلل كتلة التعليمات التي تنفذ عند تحقق الشرط.
        BlockNode thenBlock = ParseBlock();

        // يحلل كتلة وإلا فقط عند وجود الكلمة المحجوزة وإلا.
        BlockNode? elseBlock = null;
        if (Match(TokenType.Else))
        {
            // يربط الفرع البديل بعقدة الشرط.
            elseBlock = ParseBlock();
        }

        // يحدد نهاية العقدة حسب وجود فرع وإلا أو غيابه.
        SourceSpan endSpan = elseBlock is null ? thenBlock.Span : elseBlock.Span;

        // ينشئ عقدة الشرط الشاملة.
        return new IfNode(condition, thenBlock, elseBlock, Combine(ifKeyword.Span, endSpan));
    }

    /// <summary>
    /// يحلل حلقة طالما مثل: طالما (س <= 5) { ... }
    /// </summary>
    private StatementNode ParseWhile(Token whileKeyword)
    {
        // يتوقع القوس الافتتاحي قبل شرط الحلقة.
        Consume(TokenType.LeftParen, "متوقع الرمز \"(\" بعد الكلمة \"طالما\".");

        // يحلل شرط الاستمرار في الحلقة.
        ExpressionNode condition = ParseExpression();

        // يتوقع القوس الختامي بعد شرط الحلقة.
        Consume(TokenType.RightParen, "متوقع الرمز \")\" بعد شرط طالما.");

        // يحلل جسم الحلقة بين القوسين المعقوفين.
        BlockNode body = ParseBlock();

        // ينشئ عقدة الحلقة داخل AST.
        return new WhileNode(condition, body, Combine(whileKeyword.Span, body.Span));
    }

    /// <summary>
    /// يحلل كتلة محاطة بـ{ و} وتحتوي تعليمات متعددة.
    /// </summary>
    private BlockNode ParseBlock()
    {
        // يتوقع بداية الكتلة بالقوس المعقوف الافتتاحي.
        Token leftBrace = Consume(TokenType.LeftBrace, "متوقع الرمز \"{\" لبدء كتلة التعليمات.");

        // يجمع تعليمات الكتلة بترتيبها.
        List<StatementNode> statements = new List<StatementNode>();

        // يقرأ التعليمات حتى يجد القوس الختامي أو نهاية الملف.
        while (!Check(TokenType.RightBrace) && !IsAtEnd)
        {
            // يحلل تعليمة داخل الكتلة.
            StatementNode? statement = ParseStatement();

            // يضيف العقدة السليمة إلى الكتلة.
            if (statement is not null)
            {
                statements.Add(statement);
            }
            else
            {
                // يسترد من الخطأ داخل الكتلة من دون كسر بقية التعليمات.
                Synchronize();
            }
        }

        // يتوقع القوس الختامي للكتلة.
        Token rightBrace = Consume(TokenType.RightBrace, "متوقع الرمز \"}\" لإغلاق كتلة التعليمات.");

        // ينشئ عقدة الكتلة داخل AST.
        return new BlockNode(statements, Combine(leftBrace.Span, rightBrace.Span));
    }

    /// <summary>
    /// يبدأ تحليل التعبيرات بأقل أولوية: العامل المنطقي أو.
    /// </summary>
    private ExpressionNode ParseExpression()
    {
        // يضمن أن || تعالج بعد جميع العمليات ذات الأولوية الأعلى.
        return ParseOr();
    }

    /// <summary>
    /// يحلل || بين تعبيرين منطقيين.
    /// </summary>
    private ExpressionNode ParseOr()
    {
        // يبدأ بتحليل المستوى المنطقي &&.
        ExpressionNode expression = ParseAnd();

        // يربط أي عوامل || متتالية بالشجرة.
        while (Match(TokenType.OrOr))
        {
            // يحتفظ بالعامل الذي استهلكته Match.
            Token operation = Previous;

            // يحلل الطرف الأيمن من العملية.
            ExpressionNode right = ParseAnd();

            // ينشئ عقدة ثنائية للعامل ||.
            expression = new BinaryExpressionNode(expression, operation.Type, right, Combine(expression.Span, right.Span));
        }

        // يعيد التعبير المنطقي الأعلى.
        return expression;
    }

    /// <summary>
    /// يحلل && بين تعبيرين منطقيين.
    /// </summary>
    private ExpressionNode ParseAnd()
    {
        // يبدأ بتحليل مستوى المساواة.
        ExpressionNode expression = ParseEquality();

        // يربط أي عوامل && متتالية بالشجرة.
        while (Match(TokenType.AndAnd))
        {
            // يحتفظ بالعامل الذي استهلكته Match.
            Token operation = Previous;

            // يحلل الطرف الأيمن من العملية.
            ExpressionNode right = ParseEquality();

            // ينشئ عقدة ثنائية للعامل &&.
            expression = new BinaryExpressionNode(expression, operation.Type, right, Combine(expression.Span, right.Span));
        }

        // يعيد التعبير المنطقي في هذا المستوى.
        return expression;
    }

    /// <summary>
    /// يحلل == و!= بين تعبيرين.
    /// </summary>
    private ExpressionNode ParseEquality()
    {
        // يبدأ بتحليل المقارنات العددية.
        ExpressionNode expression = ParseComparison();

        // يربط عوامل المساواة بالشجرة.
        while (Match(TokenType.EqualEqual, TokenType.BangEqual))
        {
            // يحتفظ بالعامل الذي استهلكته Match.
            Token operation = Previous;

            // يحلل الطرف الأيمن للمساواة.
            ExpressionNode right = ParseComparison();

            // ينشئ عقدة ثنائية للمساواة أو عدم المساواة.
            expression = new BinaryExpressionNode(expression, operation.Type, right, Combine(expression.Span, right.Span));
        }

        // يعيد تعبير المساواة.
        return expression;
    }

    /// <summary>
    /// يحلل < و<= و> و>= بين تعبيرين.
    /// </summary>
    private ExpressionNode ParseComparison()
    {
        // يبدأ بتحليل الجمع والطرح.
        ExpressionNode expression = ParseAddition();

        // يربط عوامل المقارنة بالشجرة.
        while (Match(TokenType.Less, TokenType.LessEqual, TokenType.Greater, TokenType.GreaterEqual))
        {
            // يحتفظ بالعامل الذي استهلكته Match.
            Token operation = Previous;

            // يحلل الطرف الأيمن للمقارنة.
            ExpressionNode right = ParseAddition();

            // ينشئ عقدة ثنائية للمقارنة.
            expression = new BinaryExpressionNode(expression, operation.Type, right, Combine(expression.Span, right.Span));
        }

        // يعيد تعبير المقارنة.
        return expression;
    }

    /// <summary>
    /// يحلل + و- مع أولوية أقل من الضرب والقسمة.
    /// </summary>
    private ExpressionNode ParseAddition()
    {
        // يبدأ بتحليل الضرب والقسمة والباقي.
        ExpressionNode expression = ParseMultiplication();

        // يربط عوامل الجمع والطرح بالشجرة.
        while (Match(TokenType.Plus, TokenType.Minus))
        {
            // يحتفظ بالعامل الذي استهلكته Match.
            Token operation = Previous;

            // يحلل الطرف الأيمن للجمع أو الطرح.
            ExpressionNode right = ParseMultiplication();

            // ينشئ عقدة ثنائية للجمع أو الطرح.
            expression = new BinaryExpressionNode(expression, operation.Type, right, Combine(expression.Span, right.Span));
        }

        // يعيد التعبير الحسابي.
        return expression;
    }

    /// <summary>
    /// يحلل * و/ و% مع أولوية أعلى من الجمع والطرح.
    /// </summary>
    private ExpressionNode ParseMultiplication()
    {
        // يبدأ بتحليل عامل أحادي أو قيمة أولية.
        ExpressionNode expression = ParseUnary();

        // يربط عوامل الضرب والقسمة والباقي بالشجرة.
        while (Match(TokenType.Star, TokenType.Slash, TokenType.Percent))
        {
            // يحتفظ بالعامل الذي استهلكته Match.
            Token operation = Previous;

            // يحلل الطرف الأيمن للعملية.
            ExpressionNode right = ParseUnary();

            // ينشئ عقدة ثنائية للضرب أو القسمة أو الباقي.
            expression = new BinaryExpressionNode(expression, operation.Type, right, Combine(expression.Span, right.Span));
        }

        // يعيد التعبير الأعلى أولوية حسابياً.
        return expression;
    }

    /// <summary>
    /// يحلل ! والنفي السالب قبل القيم الأولية.
    /// </summary>
    private ExpressionNode ParseUnary()
    {
        // يتعرف على النفي المنطقي أو السالب الأحادي.
        if (Match(TokenType.Bang, TokenType.Minus))
        {
            // يحتفظ بالعامل الأحادي.
            Token operation = Previous;

            // يحلل التعبير الذي يلي العامل الأحادي.
            ExpressionNode operand = ParseUnary();

            // ينشئ عقدة أحادية داخل AST.
            return new UnaryExpressionNode(operation.Type, operand, Combine(operation.Span, operand.Span));
        }

        // ينتقل إلى الأعداد والنصوص والأسماء والأقواس.
        return ParsePrimary();
    }

    /// <summary>
    /// يحلل القيم البسيطة والأسماء والتعابير بين قوسين.
    /// </summary>
    private ExpressionNode ParsePrimary()
    {
        // يحول العدد الصحيح إلى قيمة ثابتة.
        if (Match(TokenType.Integer))
        {
            return new LiteralNode(Previous.Type, Previous.Lexeme, Previous.Span);
        }

        // يحول العدد الحقيقي إلى قيمة ثابتة.
        if (Match(TokenType.Real))
        {
            return new LiteralNode(Previous.Type, Previous.Lexeme, Previous.Span);
        }

        // يحول النص إلى قيمة ثابتة.
        if (Match(TokenType.String))
        {
            return new LiteralNode(Previous.Type, Previous.Lexeme, Previous.Span);
        }

        // يحول صحيح وخطأ إلى قيم ثابتة منطقية.
        if (Match(TokenType.True, TokenType.False))
        {
            return new LiteralNode(Previous.Type, Previous.Lexeme, Previous.Span);
        }

        // يحول اسم المتغير إلى عقدة معرف.
        if (Match(TokenType.Identifier))
        {
            return new IdentifierNode(Previous.Lexeme, Previous.Span);
        }

        // يحلل التعبير الموضوع بين قوسين.
        if (Match(TokenType.LeftParen))
        {
            // يحتفظ بموقع القوس الافتتاحي.
            Token leftParen = Previous;

            // يحلل التعبير الداخلي.
            ExpressionNode expression = ParseExpression();

            // يتوقع إغلاق التعبير بقوس.
            Token rightParen = Consume(TokenType.RightParen, "متوقع الرمز \")\" بعد التعبير.");

            // ينشئ عقدة تجميع تحفظ بنية الأقواس.
            return new GroupingNode(expression, Combine(leftParen.Span, rightParen.Span));
        }

        // يسجل خطأ عند غياب قيمة صالحة داخل التعبير.
        Report("SYN003", "متوقع عدد أو نص أو قيمة منطقية أو اسم متغير أو تعبير بين قوسين.", Current.Span);

        // يحتفظ بالرمز المخالف لإسناد موقعه لعقدة مؤقتة.
        Token invalid = Current;
        if (!IsAtEnd)
        {
            // يتقدم لتجنب البقاء على الرمز الخاطئ نفسه.
            Advance();
        }

        // يعيد عقدة مؤقتة حتى يستطيع Parser إكمال تحليل بقية الملف.
        return new LiteralNode(TokenType.Integer, "0", invalid.Span);
    }

    /// <summary>
    /// يستهلك نوعاً معتمداً للمتغير أو يسجل خطأ عند غيابه.
    /// </summary>
    private Token ConsumeType()
    {
        // يقبل الأنواع الأربع المحددة في اللغة.
        if (Match(TokenType.TypeInt, TokenType.TypeReal, TokenType.TypeBool, TokenType.TypeText))
        {
            return Previous;
        }

        // يسجل خطأ نوع مفقود ويعيد Token مؤقتاً لإكمال التحليل.
        Report("SYN001", "متوقع نوع البيانات: عدد أو حقيقي أو منطقي أو نص.", Current.Span);
        return new Token(TokenType.TypeInt, "عدد", Current.Span);
    }

    /// <summary>
    /// يستهلك Token متوقعاً أو يسجل تشخيصاً نحويّاً مع موضعه.
    /// </summary>
    private Token Consume(TokenType expected, string message)
    {
        // يستهلك الرمز الصحيح عند تطابق النوع.
        if (Check(expected))
        {
            return Advance();
        }

        // يسجل الرسالة ويصف الرمز الذي وجده بدلاً من المتوقع.
        Report("SYN001", message + " الرمز الحالي: " + Describe(Current) + ".", Current.Span);

        // يعيد Token مؤقتاً كي يكمل بناء AST بطريقة آمنة.
        return new Token(expected, string.Empty, Current.Span);
    }

    /// <summary>
    /// يتحقق من أنواع متعددة ويستهلك أول نوع يطابق الرمز الحالي.
    /// </summary>
    private bool Match(params TokenType[] types)
    {
        // يمر على الأنواع المقبولة في القاعدة الحالية.
        foreach (TokenType type in types)
        {
            // يستهلك الرمز ويبلغ بالنجاح عند التطابق.
            if (Check(type))
            {
                Advance();
                return true;
            }
        }

        // يبلغ المتصل بعدم وجود تطابق.
        return false;
    }

    /// <summary>
    /// يتحقق من نوع الرمز الحالي من دون استهلاكه.
    /// </summary>
    private bool Check(TokenType type)
    {
        // يقارن نوع Token الحالي بالنوع المتوقع من القاعدة.
        return Current.Type == type;
    }

    /// <summary>
    /// يعيد Token في موقع نسبي من الموضع الحالي من دون تجاوز نهاية القائمة.
    /// </summary>
    private Token Peek(int offset)
    {
        // يحسب الفهرس المطلوب مع حمايته ضمن حدود القائمة.
        int index = Math.Min(_position + offset, _tokens.Count - 1);

        // يعيد Token المطلوب لاستخدامه في قرار تحليل الإسناد.
        return _tokens[index];
    }

    /// <summary>
    /// يتقدم إلى Token التالي ويعيد Token الذي كان حالياً.
    /// </summary>
    private Token Advance()
    {
        // يزيد الموضع فقط إن لم يصل إلى Eof.
        if (!IsAtEnd)
        {
            _position++;
        }

        // يعيد Token السابق بعد التقدم.
        return Previous;
    }

    /// <summary>
    /// يتخطى الرموز بعد الخطأ حتى يصل إلى حد آمن بين التعليمات.
    /// </summary>
    private void Synchronize()
    {
        // يتحرك خطوة واحدة عند الإمكان لتجاوز الرمز المخالف.
        if (!IsAtEnd)
        {
            Advance();
        }

        // يستمر حتى يجد نهاية تعليمة أو بداية تعليمة أو إغلاق كتلة.
        while (!IsAtEnd)
        {
            // يتوقف بعد فاصلة منقوطة لأنها نهاية طبيعية للتعليمة.
            if (Previous.Type == TokenType.Semicolon)
            {
                return;
            }

            // يتوقف قبل الرموز التي قد تبدأ تعليمة أو تنهي كتلة.
            if (Current.Type == TokenType.Let ||
                Current.Type == TokenType.Print ||
                Current.Type == TokenType.If ||
                Current.Type == TokenType.While ||
                Current.Type == TokenType.End ||
                Current.Type == TokenType.RightBrace)
            {
                return;
            }

            // يتجاوز رمزاً لا يصلح نقطة استرداد.
            Advance();
        }
    }

    /// <summary>
    /// يضيف خطأ نحوياً إلى DiagnosticBag المشتركة مع الواجهة.
    /// </summary>
    private void Report(string code, string message, SourceSpan span)
    {
        // يربط الرسالة بالموقع الذي ستعرضه واجهة Windows Forms.
        _diagnostics.Report(code, message, span);
    }

    /// <summary>
    /// يصف Token الحالي في رسالة عربية سهلة الفهم.
    /// </summary>
    private static string Describe(Token token)
    {
        // يصف Eof بوصف مفهوم بدلاً من نص فارغ.
        if (token.Type == TokenType.Eof)
        {
            return "نهاية الملف";
        }

        // يحيط النص الفعلي بعلامتي اقتباس في رسالة الخطأ.
        return "\"" + token.Lexeme + "\"";
    }

    /// <summary>
    /// يجمع موقع عقدتين في موقع واحد يغطي كامل التعليمة أو التعبير.
    /// </summary>
    private static SourceSpan Combine(SourceSpan first, SourceSpan last)
    {
        // يحسب نهاية العقدة الأخيرة في النص المصدر.
        int end = last.Start + last.Length;

        // ينشئ المدى من بداية العقدة الأولى حتى نهاية العقدة الأخيرة.
        return new SourceSpan(first.Start, Math.Max(0, end - first.Start), first.Line, first.Column);
    }

    /// <summary>
    /// يعيد Token الحالي؛ Lexer يضمن وجود Eof دائماً في نهاية القائمة.
    /// </summary>
    private Token Current => _tokens[_position];

    /// <summary>
    /// يعيد Token السابق، أو الأول في حال لم يتحرك Parser بعد.
    /// </summary>
    private Token Previous => _tokens[Math.Max(0, _position - 1)];

    /// <summary>
    /// يحدد ما إذا كان Parser وصل إلى نهاية قائمة Tokens.
    /// </summary>
    private bool IsAtEnd => Current.Type == TokenType.Eof;
}

/// <summary>
/// يجمع برنامج AST والتشخيصات النحوية لعرضهما في واجهة Windows Forms.
/// </summary>
public sealed record ParseResult(ProgramNode Program, IReadOnlyList<Diagnostic> Diagnostics);
