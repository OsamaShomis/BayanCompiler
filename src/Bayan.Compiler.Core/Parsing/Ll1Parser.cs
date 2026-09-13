using BayanCompiler.Frontend;
using BayanCompiler.Lexing;
using BayanCompiler.Syntax;
using System.Text;

namespace BayanCompiler.Parsing;

/// <summary>
/// محلل تنبؤي Table-Driven للغة بيان.
/// يستخدم Stack وجدول LL(1) لبناء شجرة اشتقاق، ثم يحولها في مرور مستقل إلى AST المستخدمة في بقية المترجم.
/// </summary>
public sealed class Ll1Parser
{
    // تربط جدول LL(1) بالمحلل من دون الاعتماد على الاستدعاء الذاتي لقواعد Grammar.
    private static readonly Dictionary<(Ll1NonTerminal NonTerminal, TokenType Lookahead), Ll1GrammarSymbol[]> ParseTable = BuildParseTable();

    // يحتفظ بالـTokens الناتجة من Lexer؛ وهي المدخل الوحيد لمحلل LL(1).
    private readonly IReadOnlyList<Token> _tokens;

    // يجمع الأخطاء النحوية المتوافقة مع واجهة Windows Forms الحالية.
    private readonly DiagnosticBag _diagnostics = new DiagnosticBag();

    // يشير إلى Token التي يفحصها جدول LL(1) الآن.
    private int _position;

    // يسجل قرارات الجدول بالترتيب كي تعرضها الواجهة في تبويب تتبع LL(1).
    private readonly List<Ll1TraceStep> _traceSteps = new List<Ll1TraceStep>();

    // يمنع عرض البرامج الطويلة من ملء الواجهة بسجل غير محدود.
    private const int MaxTraceSteps = 600;

    /// <summary>
    /// يعرض خطوات Stack وLookahead وإجراءات جدول LL(1) الأخيرة بصورة للقراءة فقط.
    /// </summary>
    public IReadOnlyList<Ll1TraceStep> TraceSteps => _traceSteps.AsReadOnly();

    /// <summary>
    /// ينشئ نصاً منظماً للخلايا غير الفارغة في جدول LL(1) ليعرضه تبويب الواجهة.
    /// </summary>
    public static string FormatAnalysisTable()
    {
        // يحافظ على ترتيب ثابت حسب Non-terminal ثم Lookahead ليسهل مقارنة الجدول في العرض.
        var builder = new StringBuilder();
        builder.AppendLine("جدول التحليل Table-Driven LL(1) — لغة بيان");
        builder.AppendLine("الصيغة: M[NonTerminal, Lookahead] = Production");
        builder.AppendLine(new string('=', 92));

        foreach (var cell in ParseTable.OrderBy(item => item.Key.NonTerminal).ThenBy(item => item.Key.Lookahead))
        {
            // يعرض epsilon للإنتاج الفارغ حتى تكون خلايا FIRST/FOLLOW واضحة للمراجع.
            string production = cell.Value.Length == 0
                ? "ε"
                : string.Join(" ", cell.Value.Select(FormatGrammarSymbol));
            builder.AppendLine("M[" + cell.Key.NonTerminal + ", " + cell.Key.Lookahead + "] = " + production);
        }

        // يضيف ملاحظة تعليمية مختصرة توضح معنى غياب الخلية.
        builder.AppendLine();
        builder.AppendLine("غياب خلية من الجدول يعني خطأ نحوي: SYN001 أو SYN002 أو SYN003 وفق الموضع.");
        return builder.ToString();
    }

    /// <summary>
    /// ينشئ نصاً قابلاً للنسخ من سجل خطوات التحليل لهذه المحاولة.
    /// </summary>
    public string FormatTrace()
    {
        // يبدأ بعناوين ثابتة لكي تبدو الخطوات كجدول نصي في RichTextBox.
        var builder = new StringBuilder();
        builder.AppendLine("تتبع LL(1): Stack → Lookahead → Action");
        builder.AppendLine(new string('=', 100));

        if (_traceSteps.Count == 0)
        {
            // يوضح أن المستخدم يجب أن ينفذ التحليل قبل طلب السجل.
            builder.AppendLine("لا توجد خطوات بعد. اضغط «تحليل البرنامج» أو «توليد MIPS» أولاً.");
            return builder.ToString();
        }

        foreach (Ll1TraceStep step in _traceSteps)
        {
            // يطبع رقم الخطوة وحالة Stack وToken وإجراء الجدول بصيغة موحدة.
            builder.AppendLine(step.StepNumber.ToString().PadLeft(3) + " | " + step.Stack + " | " + step.Lookahead + " | " + step.Action);
        }

        // يبين للمستخدم إن كان السجل اختصر لحماية أداء الواجهة.
        if (_traceSteps.Count >= MaxTraceSteps)
        {
            builder.AppendLine("… تم اختصار السجل عند " + MaxTraceSteps + " خطوة لحماية أداء الواجهة.");
        }

        return builder.ToString();
    }

    /// <summary>
    /// ينشئ محلل LL(1) لقائمة Tokens سليمة لغوياً وتنتهي بـEof.
    /// </summary>
    public Ll1Parser(IReadOnlyList<Token> tokens)
    {
        // يحافظ على عقد الإدخال نفسه الذي كان يستخدمه Parser السابق.
        _tokens = tokens;
    }

    /// <summary>
    /// ينفذ التحليل الجدولي ثم يعيد ParseResult نفسه الذي تستهلكه الدلالة وIR والواجهة.
    /// </summary>
    public ParseResult Parse()
    {
        // يمثل الجذر أعلى رمز غير طرفي في Grammar الرسمية.
        ParseTreeNode root = ParseTreeNode.ForNonTerminal(Ll1NonTerminal.Start);

        // يمثل Stack عناصر جدول التحليل وشجرة الاشتقاق التي تتكون أثناء التوسعة.
        Stack<ParseStackEntry> stack = new Stack<ParseStackEntry>();

        // يبدأ الجدول من Start؛ رمز Eof موجود داخل إنتاج Start نفسه.
        stack.Push(new ParseStackEntry(Ll1GrammarSymbol.ForNonTerminal(Ll1NonTerminal.Start), root));

        // يكرر حتى تستهلك عناصر Grammar أو يصل إلى نهاية قائمة Tokens.
        while (stack.Count > 0)
        {
            // يأخذ الرمز الأعلى؛ لا توجد دالة ParseProgram أو ParseExpression في قرار التحليل.
            ParseStackEntry entry = stack.Pop();

            // يلتقط شكل Stack قبل اتخاذ القرار كي يستطيع الطالب مقارنته مع جدول LL(1).
            string stackSnapshot = FormatStack(entry, stack);

            // يطابق الرمز الطرفي مباشرة مع Token الحالية.
            if (entry.Symbol.IsTerminal)
            {
                // يوثق تطابقاً طرفياً قبل انتقال مؤشر Token إلى الرمز التالي.
                AddTrace(stackSnapshot, Current, "مطابقة Terminal " + entry.Symbol.Terminal);
                MatchTerminal(entry.Symbol.Terminal, entry.Node);
                continue;
            }

            // يبحث في الخلية M[NonTerminal, Lookahead] في جدول LL(1).
            if (!ParseTable.TryGetValue((entry.Symbol.NonTerminal, Current.Type), out Ll1GrammarSymbol[]? production))
            {
                // يسجل خطأ جدول واضحاً عند عدم وجود إنتاج صالح للـLookahead الحالي.
                string expected = GetExpectedLookaheads(entry.Symbol.NonTerminal);
                AddTrace(stackSnapshot, Current, "خطأ: لا توجد M[" + entry.Symbol.NonTerminal + ", " + Current.Type + "]");
                Report(GetDiagnosticCode(entry.Symbol.NonTerminal), "أثناء تحليل " + entry.Symbol.NonTerminal + ": متوقع أحد الرموز { " + expected + " } لكن وُجد " + Describe(Current) + ".", Current.Span);

                // يتجاوز رمزاً واحداً كي لا يدخل Stack في حلقة عند برنامج غير صحيح.
                if (!IsAtEnd)
                {
                    Advance();
                }

                // يسقط هذا الرمز غير الطرفي لأن البرنامج احتوى على خطأ نحوي بالفعل.
                continue;
            }

            // يوسع عقدة شجرة الاشتقاق بالإنتاج المختار من جدول LL(1).
            AddTrace(stackSnapshot, Current, "M[" + entry.Symbol.NonTerminal + ", " + Current.Type + "] = " + FormatProduction(production));
            ExpandProduction(entry.Node, production, stack);
        }

        // لا تبنى AST نهائية من شجرة ناقصة؛ تتوقف الواجهة عند التشخيصات قبل الدلالة وIR.
        if (_diagnostics.Items.Count > 0 || root.Children.Count == 0)
        {
            return new ParseResult(CreateFallbackProgram(), _diagnostics.Items);
        }

        // يحول المرور التالي شجرة الاشتقاق السليمة إلى عقد AST ذاتها المستخدمة سابقاً.
        ProgramNode program = BuildProgram(root.Children[0]);

        // يوثق انتهاء مرحلة الجدول وبداية المرور المنفصل لبناء AST.
        AddTrace("∅", Current, "نجح تحليل LL(1) ثم اكتمل تحويل Concrete Parse Tree إلى AST.");

        // يحافظ على عقد ParseResult كي لا تتغير MainForm أو SemanticAnalyzer أو IrGenerator.
        return new ParseResult(program, _diagnostics.Items);
    }

    /// <summary>
    /// يطابق رمزاً طرفياً، ويوثق Token المطابقة داخل شجرة الاشتقاق.
    /// </summary>
    private void MatchTerminal(TokenType expected, ParseTreeNode node)
    {
        // يقبل Token الصحيحة ويثبتها كي يستعملها مرور بناء AST لاحقاً.
        if (Current.Type == expected)
        {
            // يحفظ Eof نفسها؛ لأن Advance لا يتحرك بعد نهاية الملف.
            node.MatchedToken = IsAtEnd ? Current : Advance();
            return;
        }

        // يبلغ عن الرمز المتوقع والرمز الذي وجده جدول التحليل.
        Report("SYN001", "متوقع الرمز " + expected + " لكن وُجد " + Describe(Current) + ".", Current.Span);

        // يتقدم عند الإمكان لتجنب الوقوف عند نفس الرمز الخاطئ.
        if (!IsAtEnd)
        {
            Advance();
        }
    }

    /// <summary>
    /// يضيف أطفال الإنتاج ثم يدفعهم بترتيب عكسي ليعالج Stack الطرف الأيسر أولاً.
    /// </summary>
    private static void ExpandProduction(ParseTreeNode parent, IReadOnlyList<Ll1GrammarSymbol> production, Stack<ParseStackEntry> stack)
    {
        // يمثل الإنتاج الفارغ epsilon بعقدة بلا أطفال.
        List<ParseTreeNode> children = new List<ParseTreeNode>();

        // ينشئ عقدة واحدة لكل رمز في الطرف الأيمن من الإنتاج.
        foreach (Ll1GrammarSymbol symbol in production)
        {
            ParseTreeNode child = symbol.IsTerminal
                ? ParseTreeNode.ForTerminal(symbol.Terminal)
                : ParseTreeNode.ForNonTerminal(symbol.NonTerminal);
            parent.Children.Add(child);
            children.Add(child);
        }

        // يعكس الدفع لأن Stack يعمل من أعلى إلى أسفل بينما Grammar تقرأ من اليسار إلى اليمين.
        for (int index = production.Count - 1; index >= 0; index--)
        {
            stack.Push(new ParseStackEntry(production[index], children[index]));
        }
    }

    /// <summary>
    /// يبني ProgramNode من شجرة الاشتقاق بعد نجاح جدول LL(1).
    /// </summary>
    private static ProgramNode BuildProgram(ParseTreeNode node)
    {
        // ProgramNode → Program Identifier StatementList End.
        Token programKeyword = TokenOf(node.Children[0]);
        Token name = TokenOf(node.Children[1]);
        List<StatementNode> statements = BuildStatementList(node.Children[2]);
        Token end = TokenOf(node.Children[3]);

        // يعيد نفس عقدة البرنامج التي يستهلكها التحليل الدلالي ومولد IR.
        return new ProgramNode(name.Lexeme, statements, Combine(programKeyword.Span, end.Span));
    }

    /// <summary>
    /// يحول القائمة اليمينية StatementList إلى قائمة تعليمات AST مرتبة.
    /// </summary>
    private static List<StatementNode> BuildStatementList(ParseTreeNode node)
    {
        // الإنتاج epsilon يمثل نهاية التعليمات في نهاية أو قبل RightBrace.
        if (node.Children.Count == 0)
        {
            return new List<StatementNode>();
        }

        // StatementList → Statement StatementList.
        List<StatementNode> statements = new List<StatementNode>();
        statements.Add(BuildStatement(node.Children[0]));
        statements.AddRange(BuildStatementList(node.Children[1]));
        return statements;
    }

    /// <summary>
    /// يوجه عقدة Statement إلى منشئ AST المطابق للإنتاج الذي اختاره جدول LL(1).
    /// </summary>
    private static StatementNode BuildStatement(ParseTreeNode node)
    {
        // Statement يتكون من Non-terminal واحد وفق خلية الجدول المختارة.
        ParseTreeNode choice = node.Children[0];

        // يحافظ كل فرع على نوع عقدة AST السابق حتى لا تتغير المراحل اللاحقة.
        return choice.NonTerminal switch
        {
            Ll1NonTerminal.Declaration => BuildDeclaration(choice),
            Ll1NonTerminal.Assignment => BuildAssignment(choice),
            Ll1NonTerminal.PrintStatement => BuildPrint(choice),
            Ll1NonTerminal.IfStatement => BuildIf(choice),
            Ll1NonTerminal.WhileStatement => BuildWhile(choice),
            Ll1NonTerminal.Block => BuildBlock(choice),
            _ => throw new InvalidOperationException("تعليمة LL(1) غير معروفة أثناء بناء AST.")
        };
    }

    /// <summary>
    /// يبني VarDeclarationNode من الإنتاج Declaration.
    /// </summary>
    private static StatementNode BuildDeclaration(ParseTreeNode node)
    {
        // Declaration → Let Identifier Colon Type InitializerOpt Semicolon.
        Token letKeyword = TokenOf(node.Children[0]);
        Token name = TokenOf(node.Children[1]);
        string typeName = BuildType(node.Children[3]);
        ExpressionNode? initializer = BuildInitializerOpt(node.Children[4]);
        Token semicolon = TokenOf(node.Children[5]);
        return new VarDeclarationNode(name.Lexeme, typeName, initializer, Combine(letKeyword.Span, semicolon.Span));
    }

    /// <summary>
    /// يعيد التعبير الاختياري في تعريف المتغير أو null عند إنتاج epsilon.
    /// </summary>
    private static ExpressionNode? BuildInitializerOpt(ParseTreeNode node)
    {
        // InitializerOpt → ε لا يولد قيمة أولية.
        if (node.Children.Count == 0)
        {
            return null;
        }

        // InitializerOpt → Assign Expression؛ لا نحتاج Token Assign في AST الحالية.
        return BuildExpression(node.Children[1]);
    }

    /// <summary>
    /// يعيد النص العربي للنوع من Token المطابقة.
    /// </summary>
    private static string BuildType(ParseTreeNode node)
    {
        // Type يحتوي Token واحدة من الأنواع الأربعة المعتمدة.
        return TokenOf(node.Children[0]).Lexeme;
    }

    /// <summary>
    /// يبني AssignmentNode من إنتاج Assignment.
    /// </summary>
    private static StatementNode BuildAssignment(ParseTreeNode node)
    {
        // Assignment → Identifier Assign Expression Semicolon.
        Token name = TokenOf(node.Children[0]);
        ExpressionNode expression = BuildExpression(node.Children[2]);
        Token semicolon = TokenOf(node.Children[3]);
        return new AssignmentNode(name.Lexeme, expression, Combine(name.Span, semicolon.Span));
    }

    /// <summary>
    /// يبني PrintNode من إنتاج PrintStatement.
    /// </summary>
    private static StatementNode BuildPrint(ParseTreeNode node)
    {
        // PrintStatement → Print LeftParen Expression RightParen Semicolon.
        Token printKeyword = TokenOf(node.Children[0]);
        ExpressionNode expression = BuildExpression(node.Children[2]);
        Token semicolon = TokenOf(node.Children[4]);
        return new PrintNode(expression, Combine(printKeyword.Span, semicolon.Span));
    }

    /// <summary>
    /// يبني IfNode من إنتاج IfStatement مع ElsePart اختياري.
    /// </summary>
    private static StatementNode BuildIf(ParseTreeNode node)
    {
        // IfStatement → If LeftParen Expression RightParen Block ElsePart.
        Token ifKeyword = TokenOf(node.Children[0]);
        ExpressionNode condition = BuildExpression(node.Children[2]);
        BlockNode thenBlock = BuildBlock(node.Children[4]);
        BlockNode? elseBlock = BuildElsePart(node.Children[5]);
        SourceSpan endSpan = elseBlock is null ? thenBlock.Span : elseBlock.Span;
        return new IfNode(condition, thenBlock, elseBlock, Combine(ifKeyword.Span, endSpan));
    }

    /// <summary>
    /// يعيد BlockNode لفرع وإلا أو null عندما يختار الجدول إنتاج epsilon.
    /// </summary>
    private static BlockNode? BuildElsePart(ParseTreeNode node)
    {
        // ElsePart → ε لا يضيف فرعاً بديلاً إلى IfNode.
        if (node.Children.Count == 0)
        {
            return null;
        }

        // ElsePart → Else Block؛ Token Else لا تحتاجه عقدة IfNode الحالية.
        return BuildBlock(node.Children[1]);
    }

    /// <summary>
    /// يبني WhileNode من إنتاج WhileStatement.
    /// </summary>
    private static StatementNode BuildWhile(ParseTreeNode node)
    {
        // WhileStatement → While LeftParen Expression RightParen Block.
        Token whileKeyword = TokenOf(node.Children[0]);
        ExpressionNode condition = BuildExpression(node.Children[2]);
        BlockNode body = BuildBlock(node.Children[4]);
        return new WhileNode(condition, body, Combine(whileKeyword.Span, body.Span));
    }

    /// <summary>
    /// يبني BlockNode من إنتاج Block ويحافظ على ترتيب التعليمات داخله.
    /// </summary>
    private static BlockNode BuildBlock(ParseTreeNode node)
    {
        // Block → LeftBrace StatementList RightBrace.
        Token leftBrace = TokenOf(node.Children[0]);
        List<StatementNode> statements = BuildStatementList(node.Children[1]);
        Token rightBrace = TokenOf(node.Children[2]);
        return new BlockNode(statements, Combine(leftBrace.Span, rightBrace.Span));
    }

    /// <summary>
    /// يبدأ بناء التعبير من أدنى أولوية، كما تنص Grammar LL(1).
    /// </summary>
    private static ExpressionNode BuildExpression(ParseTreeNode node)
    {
        // Expression → Or.
        return BuildOr(node.Children[0]);
    }

    private static ExpressionNode BuildOr(ParseTreeNode node)
    {
        // Or → And OrTail.
        ExpressionNode left = BuildAnd(node.Children[0]);
        return BuildOrTail(node.Children[1], left);
    }

    private static ExpressionNode BuildOrTail(ParseTreeNode node, ExpressionNode left)
    {
        // OrTail → ε ينهي سلسلة || ويعيد الطرف المتراكم.
        if (node.Children.Count == 0)
        {
            return left;
        }

        // OrTail → OrOr And OrTail ويطابق السلسلة اليسارية في AST.
        Token operation = TokenOf(node.Children[0]);
        ExpressionNode right = BuildAnd(node.Children[1]);
        ExpressionNode combined = new BinaryExpressionNode(left, operation.Type, right, Combine(left.Span, right.Span));
        return BuildOrTail(node.Children[2], combined);
    }

    private static ExpressionNode BuildAnd(ParseTreeNode node)
    {
        // And → Equality AndTail.
        ExpressionNode left = BuildEquality(node.Children[0]);
        return BuildAndTail(node.Children[1], left);
    }

    private static ExpressionNode BuildAndTail(ParseTreeNode node, ExpressionNode left)
    {
        // AndTail → ε ينهي سلسلة &&.
        if (node.Children.Count == 0)
        {
            return left;
        }

        // AndTail → AndAnd Equality AndTail.
        Token operation = TokenOf(node.Children[0]);
        ExpressionNode right = BuildEquality(node.Children[1]);
        ExpressionNode combined = new BinaryExpressionNode(left, operation.Type, right, Combine(left.Span, right.Span));
        return BuildAndTail(node.Children[2], combined);
    }

    private static ExpressionNode BuildEquality(ParseTreeNode node)
    {
        // Equality → Comparison EqualityTail.
        ExpressionNode left = BuildComparison(node.Children[0]);
        return BuildEqualityTail(node.Children[1], left);
    }

    private static ExpressionNode BuildEqualityTail(ParseTreeNode node, ExpressionNode left)
    {
        // EqualityTail → ε ينهي == و!=.
        if (node.Children.Count == 0)
        {
            return left;
        }

        // EqualityTail → (EqualEqual | BangEqual) Comparison EqualityTail.
        Token operation = TokenOf(node.Children[0]);
        ExpressionNode right = BuildComparison(node.Children[1]);
        ExpressionNode combined = new BinaryExpressionNode(left, operation.Type, right, Combine(left.Span, right.Span));
        return BuildEqualityTail(node.Children[2], combined);
    }

    private static ExpressionNode BuildComparison(ParseTreeNode node)
    {
        // Comparison → Addition ComparisonTail.
        ExpressionNode left = BuildAddition(node.Children[0]);
        return BuildComparisonTail(node.Children[1], left);
    }

    private static ExpressionNode BuildComparisonTail(ParseTreeNode node, ExpressionNode left)
    {
        // ComparisonTail → ε ينهي سلسلة المقارنات.
        if (node.Children.Count == 0)
        {
            return left;
        }

        // ComparisonTail → عامل مقارنة ثم Addition ثم Tail.
        Token operation = TokenOf(node.Children[0]);
        ExpressionNode right = BuildAddition(node.Children[1]);
        ExpressionNode combined = new BinaryExpressionNode(left, operation.Type, right, Combine(left.Span, right.Span));
        return BuildComparisonTail(node.Children[2], combined);
    }

    private static ExpressionNode BuildAddition(ParseTreeNode node)
    {
        // Addition → Multiplication AdditionTail.
        ExpressionNode left = BuildMultiplication(node.Children[0]);
        return BuildAdditionTail(node.Children[1], left);
    }

    private static ExpressionNode BuildAdditionTail(ParseTreeNode node, ExpressionNode left)
    {
        // AdditionTail → ε ينهي + و-.
        if (node.Children.Count == 0)
        {
            return left;
        }

        // AdditionTail → (Plus | Minus) Multiplication AdditionTail.
        Token operation = TokenOf(node.Children[0]);
        ExpressionNode right = BuildMultiplication(node.Children[1]);
        ExpressionNode combined = new BinaryExpressionNode(left, operation.Type, right, Combine(left.Span, right.Span));
        return BuildAdditionTail(node.Children[2], combined);
    }

    private static ExpressionNode BuildMultiplication(ParseTreeNode node)
    {
        // Multiplication → Unary MultiplicationTail.
        ExpressionNode left = BuildUnary(node.Children[0]);
        return BuildMultiplicationTail(node.Children[1], left);
    }

    private static ExpressionNode BuildMultiplicationTail(ParseTreeNode node, ExpressionNode left)
    {
        // MultiplicationTail → ε ينهي * و/ و%.
        if (node.Children.Count == 0)
        {
            return left;
        }

        // MultiplicationTail → عامل ضرب ثم Unary ثم Tail.
        Token operation = TokenOf(node.Children[0]);
        ExpressionNode right = BuildUnary(node.Children[1]);
        ExpressionNode combined = new BinaryExpressionNode(left, operation.Type, right, Combine(left.Span, right.Span));
        return BuildMultiplicationTail(node.Children[2], combined);
    }

    private static ExpressionNode BuildUnary(ParseTreeNode node)
    {
        // Unary → (Bang | Minus) Unary | Primary.
        if (node.Children[0].Terminal == TokenType.Bang || node.Children[0].Terminal == TokenType.Minus)
        {
            Token operation = TokenOf(node.Children[0]);
            ExpressionNode operand = BuildUnary(node.Children[1]);
            return new UnaryExpressionNode(operation.Type, operand, Combine(operation.Span, operand.Span));
        }

        return BuildPrimary(node.Children[0]);
    }

    private static ExpressionNode BuildPrimary(ParseTreeNode node)
    {
        // Primary → LeftParen Expression RightParen هو البديل الوحيد الذي يحتوي ثلاثة أطفال.
        if (node.Children.Count == 3)
        {
            Token leftParen = TokenOf(node.Children[0]);
            ExpressionNode expression = BuildExpression(node.Children[1]);
            Token rightParen = TokenOf(node.Children[2]);
            return new GroupingNode(expression, Combine(leftParen.Span, rightParen.Span));
        }

        // بقية البدائل تحمل Token طرفية واحدة.
        Token token = TokenOf(node.Children[0]);
        if (token.Type == TokenType.Identifier)
        {
            return new IdentifierNode(token.Lexeme, token.Span);
        }

        return new LiteralNode(token.Type, token.Lexeme, token.Span);
    }

    /// <summary>
    /// يبني جدول LL(1) من خلايا Non-terminal وLookahead إلى إنتاجات Grammar الرسمية.
    /// </summary>
    private static Dictionary<(Ll1NonTerminal NonTerminal, TokenType Lookahead), Ll1GrammarSymbol[]> BuildParseTable()
    {
        // يمثل Dictionary جدول M[A, a] المستخدم في تعريف محللات LL(1).
        var table = new Dictionary<(Ll1NonTerminal NonTerminal, TokenType Lookahead), Ll1GrammarSymbol[]>();

        // يبني الإنتاجات العليا وبنية التعليمات والكتل.
        Add(table, Ll1NonTerminal.Start, new TokenType[] { TokenType.Program }, N(Ll1NonTerminal.ProgramNode), T(TokenType.Eof));
        Add(table, Ll1NonTerminal.ProgramNode, new TokenType[] { TokenType.Program }, T(TokenType.Program), T(TokenType.Identifier), N(Ll1NonTerminal.StatementList), T(TokenType.End));
        Add(table, Ll1NonTerminal.StatementList, StatementStarts(), N(Ll1NonTerminal.Statement), N(Ll1NonTerminal.StatementList));
        Add(table, Ll1NonTerminal.StatementList, new TokenType[] { TokenType.End, TokenType.RightBrace });
        Add(table, Ll1NonTerminal.Statement, new TokenType[] { TokenType.Let }, N(Ll1NonTerminal.Declaration));
        Add(table, Ll1NonTerminal.Statement, new TokenType[] { TokenType.Identifier }, N(Ll1NonTerminal.Assignment));
        Add(table, Ll1NonTerminal.Statement, new TokenType[] { TokenType.Print }, N(Ll1NonTerminal.PrintStatement));
        Add(table, Ll1NonTerminal.Statement, new TokenType[] { TokenType.If }, N(Ll1NonTerminal.IfStatement));
        Add(table, Ll1NonTerminal.Statement, new TokenType[] { TokenType.While }, N(Ll1NonTerminal.WhileStatement));
        Add(table, Ll1NonTerminal.Statement, new TokenType[] { TokenType.LeftBrace }, N(Ll1NonTerminal.Block));

        // يربط تعريف المتغير والتهيئة الاختيارية والأنواع بمسارات الجدول الخاصة بها.
        Add(table, Ll1NonTerminal.Declaration, new TokenType[] { TokenType.Let }, T(TokenType.Let), T(TokenType.Identifier), T(TokenType.Colon), N(Ll1NonTerminal.Type), N(Ll1NonTerminal.InitializerOpt), T(TokenType.Semicolon));
        Add(table, Ll1NonTerminal.InitializerOpt, new TokenType[] { TokenType.Assign }, T(TokenType.Assign), N(Ll1NonTerminal.Expression));
        Add(table, Ll1NonTerminal.InitializerOpt, new TokenType[] { TokenType.Semicolon });
        Add(table, Ll1NonTerminal.Type, new TokenType[] { TokenType.TypeInt }, T(TokenType.TypeInt));
        Add(table, Ll1NonTerminal.Type, new TokenType[] { TokenType.TypeReal }, T(TokenType.TypeReal));
        Add(table, Ll1NonTerminal.Type, new TokenType[] { TokenType.TypeBool }, T(TokenType.TypeBool));
        Add(table, Ll1NonTerminal.Type, new TokenType[] { TokenType.TypeText }, T(TokenType.TypeText));
        Add(table, Ll1NonTerminal.Assignment, new TokenType[] { TokenType.Identifier }, T(TokenType.Identifier), T(TokenType.Assign), N(Ll1NonTerminal.Expression), T(TokenType.Semicolon));
        Add(table, Ll1NonTerminal.PrintStatement, new TokenType[] { TokenType.Print }, T(TokenType.Print), T(TokenType.LeftParen), N(Ll1NonTerminal.Expression), T(TokenType.RightParen), T(TokenType.Semicolon));
        Add(table, Ll1NonTerminal.IfStatement, new TokenType[] { TokenType.If }, T(TokenType.If), T(TokenType.LeftParen), N(Ll1NonTerminal.Expression), T(TokenType.RightParen), N(Ll1NonTerminal.Block), N(Ll1NonTerminal.ElsePart));
        Add(table, Ll1NonTerminal.ElsePart, new TokenType[] { TokenType.Else }, T(TokenType.Else), N(Ll1NonTerminal.Block));
        Add(table, Ll1NonTerminal.ElsePart, StatementFollow());
        Add(table, Ll1NonTerminal.WhileStatement, new TokenType[] { TokenType.While }, T(TokenType.While), T(TokenType.LeftParen), N(Ll1NonTerminal.Expression), T(TokenType.RightParen), N(Ll1NonTerminal.Block));
        Add(table, Ll1NonTerminal.Block, new TokenType[] { TokenType.LeftBrace }, T(TokenType.LeftBrace), N(Ll1NonTerminal.StatementList), T(TokenType.RightBrace));

        // يضيف طبقات التعبيرات من الأقل أولوية إلى الأعلى مع Tail لإزالة التكرار اليساري.
        TokenType[] expressionStarts = ExpressionStarts();
        Add(table, Ll1NonTerminal.Expression, expressionStarts, N(Ll1NonTerminal.Or));
        Add(table, Ll1NonTerminal.Or, expressionStarts, N(Ll1NonTerminal.And), N(Ll1NonTerminal.OrTail));
        Add(table, Ll1NonTerminal.OrTail, new TokenType[] { TokenType.OrOr }, T(TokenType.OrOr), N(Ll1NonTerminal.And), N(Ll1NonTerminal.OrTail));
        Add(table, Ll1NonTerminal.OrTail, new TokenType[] { TokenType.Semicolon, TokenType.RightParen });
        Add(table, Ll1NonTerminal.And, expressionStarts, N(Ll1NonTerminal.Equality), N(Ll1NonTerminal.AndTail));
        Add(table, Ll1NonTerminal.AndTail, new TokenType[] { TokenType.AndAnd }, T(TokenType.AndAnd), N(Ll1NonTerminal.Equality), N(Ll1NonTerminal.AndTail));
        Add(table, Ll1NonTerminal.AndTail, new TokenType[] { TokenType.OrOr, TokenType.Semicolon, TokenType.RightParen });
        Add(table, Ll1NonTerminal.Equality, expressionStarts, N(Ll1NonTerminal.Comparison), N(Ll1NonTerminal.EqualityTail));
        Add(table, Ll1NonTerminal.EqualityTail, new TokenType[] { TokenType.EqualEqual }, T(TokenType.EqualEqual), N(Ll1NonTerminal.Comparison), N(Ll1NonTerminal.EqualityTail));
        Add(table, Ll1NonTerminal.EqualityTail, new TokenType[] { TokenType.BangEqual }, T(TokenType.BangEqual), N(Ll1NonTerminal.Comparison), N(Ll1NonTerminal.EqualityTail));
        Add(table, Ll1NonTerminal.EqualityTail, new TokenType[] { TokenType.AndAnd, TokenType.OrOr, TokenType.Semicolon, TokenType.RightParen });
        Add(table, Ll1NonTerminal.Comparison, expressionStarts, N(Ll1NonTerminal.Addition), N(Ll1NonTerminal.ComparisonTail));
        Add(table, Ll1NonTerminal.ComparisonTail, new TokenType[] { TokenType.Less }, T(TokenType.Less), N(Ll1NonTerminal.Addition), N(Ll1NonTerminal.ComparisonTail));
        Add(table, Ll1NonTerminal.ComparisonTail, new TokenType[] { TokenType.LessEqual }, T(TokenType.LessEqual), N(Ll1NonTerminal.Addition), N(Ll1NonTerminal.ComparisonTail));
        Add(table, Ll1NonTerminal.ComparisonTail, new TokenType[] { TokenType.Greater }, T(TokenType.Greater), N(Ll1NonTerminal.Addition), N(Ll1NonTerminal.ComparisonTail));
        Add(table, Ll1NonTerminal.ComparisonTail, new TokenType[] { TokenType.GreaterEqual }, T(TokenType.GreaterEqual), N(Ll1NonTerminal.Addition), N(Ll1NonTerminal.ComparisonTail));
        Add(table, Ll1NonTerminal.ComparisonTail, new TokenType[] { TokenType.EqualEqual, TokenType.BangEqual, TokenType.AndAnd, TokenType.OrOr, TokenType.Semicolon, TokenType.RightParen });
        Add(table, Ll1NonTerminal.Addition, expressionStarts, N(Ll1NonTerminal.Multiplication), N(Ll1NonTerminal.AdditionTail));
        Add(table, Ll1NonTerminal.AdditionTail, new TokenType[] { TokenType.Plus }, T(TokenType.Plus), N(Ll1NonTerminal.Multiplication), N(Ll1NonTerminal.AdditionTail));
        Add(table, Ll1NonTerminal.AdditionTail, new TokenType[] { TokenType.Minus }, T(TokenType.Minus), N(Ll1NonTerminal.Multiplication), N(Ll1NonTerminal.AdditionTail));
        Add(table, Ll1NonTerminal.AdditionTail, new TokenType[] { TokenType.Less, TokenType.LessEqual, TokenType.Greater, TokenType.GreaterEqual, TokenType.EqualEqual, TokenType.BangEqual, TokenType.AndAnd, TokenType.OrOr, TokenType.Semicolon, TokenType.RightParen });
        Add(table, Ll1NonTerminal.Multiplication, expressionStarts, N(Ll1NonTerminal.Unary), N(Ll1NonTerminal.MultiplicationTail));
        Add(table, Ll1NonTerminal.MultiplicationTail, new TokenType[] { TokenType.Star }, T(TokenType.Star), N(Ll1NonTerminal.Unary), N(Ll1NonTerminal.MultiplicationTail));
        Add(table, Ll1NonTerminal.MultiplicationTail, new TokenType[] { TokenType.Slash }, T(TokenType.Slash), N(Ll1NonTerminal.Unary), N(Ll1NonTerminal.MultiplicationTail));
        Add(table, Ll1NonTerminal.MultiplicationTail, new TokenType[] { TokenType.Percent }, T(TokenType.Percent), N(Ll1NonTerminal.Unary), N(Ll1NonTerminal.MultiplicationTail));
        Add(table, Ll1NonTerminal.MultiplicationTail, new TokenType[] { TokenType.Plus, TokenType.Minus, TokenType.Less, TokenType.LessEqual, TokenType.Greater, TokenType.GreaterEqual, TokenType.EqualEqual, TokenType.BangEqual, TokenType.AndAnd, TokenType.OrOr, TokenType.Semicolon, TokenType.RightParen });
        Add(table, Ll1NonTerminal.Unary, new TokenType[] { TokenType.Bang }, T(TokenType.Bang), N(Ll1NonTerminal.Unary));
        Add(table, Ll1NonTerminal.Unary, new TokenType[] { TokenType.Minus }, T(TokenType.Minus), N(Ll1NonTerminal.Unary));
        Add(table, Ll1NonTerminal.Unary, new TokenType[] { TokenType.Integer, TokenType.Real, TokenType.String, TokenType.True, TokenType.False, TokenType.Identifier, TokenType.LeftParen }, N(Ll1NonTerminal.Primary));
        Add(table, Ll1NonTerminal.Primary, new TokenType[] { TokenType.Integer }, T(TokenType.Integer));
        Add(table, Ll1NonTerminal.Primary, new TokenType[] { TokenType.Real }, T(TokenType.Real));
        Add(table, Ll1NonTerminal.Primary, new TokenType[] { TokenType.String }, T(TokenType.String));
        Add(table, Ll1NonTerminal.Primary, new TokenType[] { TokenType.True }, T(TokenType.True));
        Add(table, Ll1NonTerminal.Primary, new TokenType[] { TokenType.False }, T(TokenType.False));
        Add(table, Ll1NonTerminal.Primary, new TokenType[] { TokenType.Identifier }, T(TokenType.Identifier));
        Add(table, Ll1NonTerminal.Primary, new TokenType[] { TokenType.LeftParen }, T(TokenType.LeftParen), N(Ll1NonTerminal.Expression), T(TokenType.RightParen));

        // يعيد الجدول النهائي كي يعمل Parse عبر Stack فقط.
        return table;
    }

    /// <summary>
    /// يضيف إنتاجاً إلى مجموعة خلايا لها Lookaheads مشتركة.
    /// </summary>
    private static void Add(Dictionary<(Ll1NonTerminal NonTerminal, TokenType Lookahead), Ll1GrammarSymbol[]> table, Ll1NonTerminal nonTerminal, IEnumerable<TokenType> lookaheads, params Ll1GrammarSymbol[] production)
    {
        // يضيف كل Lookahead إلى خلية مستقلة حتى تكون بنية الجدول صريحة وقابلة للفحص.
        foreach (TokenType lookahead in lookaheads)
        {
            table.Add((nonTerminal, lookahead), production);
        }
    }

    // ينشئ رمزاً طرفياً ليضعه الإنتاج في جدول LL(1).
    private static Ll1GrammarSymbol T(TokenType tokenType) => Ll1GrammarSymbol.ForTerminal(tokenType);

    // ينشئ رمزاً غير طرفي ليضعه الإنتاج في جدول LL(1).
    private static Ll1GrammarSymbol N(Ll1NonTerminal nonTerminal) => Ll1GrammarSymbol.ForNonTerminal(nonTerminal);

    // يعرض رمز Grammar الطرفي أو غير الطرفي داخل الجدول النصي.
    private static string FormatGrammarSymbol(Ll1GrammarSymbol symbol)
    {
        return symbol.IsTerminal ? symbol.Terminal.ToString() : symbol.NonTerminal.ToString();
    }

    // يعرض الطرف الأيمن للإنتاج المختار من جدول LL(1).
    private static string FormatProduction(IReadOnlyList<Ll1GrammarSymbol> production)
    {
        return production.Count == 0 ? "ε" : string.Join(" ", production.Select(FormatGrammarSymbol));
    }

    // يحدد Tokens التي يمكن أن تبدأ تعليمة في Grammar الحالية.
    private static TokenType[] StatementStarts() => new TokenType[] { TokenType.Let, TokenType.Identifier, TokenType.Print, TokenType.If, TokenType.While, TokenType.LeftBrace };

    // يحدد FOLLOW المشترك للتعليمات وElsePart في النطاق الحالي.
    private static TokenType[] StatementFollow() => new TokenType[] { TokenType.Let, TokenType.Identifier, TokenType.Print, TokenType.If, TokenType.While, TokenType.LeftBrace, TokenType.End, TokenType.RightBrace };

    // يحدد FIRST المشترك لطبقات التعبيرات من Or حتى Unary.
    private static TokenType[] ExpressionStarts() => new TokenType[] { TokenType.Bang, TokenType.Minus, TokenType.Integer, TokenType.Real, TokenType.String, TokenType.True, TokenType.False, TokenType.Identifier, TokenType.LeftParen };

    // ينشئ ProgramNode آمنة كي تعرض الواجهة التشخيصات عند حدوث خطأ نحوي.
    private ProgramNode CreateFallbackProgram()
    {
        // يربط العقدة المؤقتة بأول Token متاح حتى تظل SourceSpan صالحة.
        SourceSpan span = _tokens.Count > 0 ? _tokens[0].Span : new SourceSpan(0, 0, 1, 1);
        return new ProgramNode("برنامج_غير_مكتمل", new List<StatementNode>(), span);
    }

    // يضيف تشخيصاً نحويّاً قابلاً للعرض في تبويب الأخطاء نفسه.
    private void Report(string code, string message, SourceSpan span)
    {
        _diagnostics.Report(code, message, span);
    }

    // يسجل خطوة واحدة فقط إذا لم يتجاوز السجل الحد الذي يحمي أداء Windows Forms.
    private void AddTrace(string stack, Token lookahead, string action)
    {
        if (_traceSteps.Count >= MaxTraceSteps)
        {
            return;
        }

        _traceSteps.Add(new Ll1TraceStep(_traceSteps.Count + 1, stack, lookahead.Type + " " + Describe(lookahead), action));
    }

    // يعرض أعلى Stack أولاً مع أول ستة عناصر فقط حتى يبقى السجل قابلاً للقراءة.
    private static string FormatStack(ParseStackEntry currentEntry, Stack<ParseStackEntry> remainingStack)
    {
        var symbols = new List<string> { FormatGrammarSymbol(currentEntry.Symbol) };
        symbols.AddRange(remainingStack.Take(5).Select(item => FormatGrammarSymbol(item.Symbol)));
        return string.Join(" ← ", symbols);
    }

    // يجمع Lookaheads التي تمثل الأعمدة الممتلئة للـNon-terminal في جدول LL(1).
    private static string GetExpectedLookaheads(Ll1NonTerminal nonTerminal)
    {
        return string.Join(", ", ParseTable.Keys
            .Where(key => key.NonTerminal == nonTerminal)
            .Select(key => key.Lookahead.ToString())
            .OrderBy(name => name));
    }

    // يحافظ على رموز الأخطاء التعليمية السابقة مع اختلاف آلية التحليل الجديدة.
    private static string GetDiagnosticCode(Ll1NonTerminal nonTerminal)
    {
        // يميز بداية تعليمة غير مدعومة عن بقية أخطاء الجدول.
        if (nonTerminal == Ll1NonTerminal.Statement)
        {
            return "SYN002";
        }

        // يميز غياب قيمة أولية صالحة داخل التعبير.
        if (nonTerminal == Ll1NonTerminal.Primary)
        {
            return "SYN003";
        }

        // يستخدم الرمز العام لفقدان علامة أو تركيب نحوي آخر.
        return "SYN001";
    }

    // يتقدم إلى Token التالية ويعيد السابقة.
    private Token Advance()
    {
        if (!IsAtEnd)
        {
            _position++;
        }

        return Previous;
    }

    // يصف Token في رسالة عربية مفهومة للمستخدم.
    private static string Describe(Token token)
    {
        return token.Type == TokenType.Eof ? "نهاية الملف" : "\"" + token.Lexeme + "\"";
    }

    // يقرأ Token الحالية مع ضمان عدم تجاوز Eof الذي يضيفه Lexer.
    private Token Current => _tokens[Math.Min(_position, _tokens.Count - 1)];

    // يعيد Token السابقة لاستخدامها بعد Advance.
    private Token Previous => _tokens[Math.Max(0, _position - 1)];

    // يحدد ما إذا وصل المحلل إلى Eof.
    private bool IsAtEnd => Current.Type == TokenType.Eof;

    // يجمع SourceSpan لعقدتين حتى يظهر موضع AST الكامل في التشخيصات.
    private static SourceSpan Combine(SourceSpan first, SourceSpan last)
    {
        int end = last.Start + last.Length;
        return new SourceSpan(first.Start, Math.Max(0, end - first.Start), first.Line, first.Column);
    }

    // يستخرج Token المطابقة من عقدة طرفية في شجرة الاشتقاق السليمة.
    private static Token TokenOf(ParseTreeNode node)
    {
        return node.MatchedToken ?? throw new InvalidOperationException("عقدة طرفية بلا Token أثناء بناء AST من LL(1).");
    }

    /// <summary>
    /// يعرّف كل Non-terminal في Grammar LL(1) الرسمية.
    /// </summary>
    private enum Ll1NonTerminal
    {
        Start,
        ProgramNode,
        StatementList,
        Statement,
        Declaration,
        InitializerOpt,
        Type,
        Assignment,
        PrintStatement,
        IfStatement,
        ElsePart,
        WhileStatement,
        Block,
        Expression,
        Or,
        OrTail,
        And,
        AndTail,
        Equality,
        EqualityTail,
        Comparison,
        ComparisonTail,
        Addition,
        AdditionTail,
        Multiplication,
        MultiplicationTail,
        Unary,
        Primary
    }

    /// <summary>
    /// يمثل رمزاً واحداً في يمين إنتاج LL(1)، طرفياً أو غير طرفي.
    /// </summary>
    private sealed class Ll1GrammarSymbol
    {
        private Ll1GrammarSymbol(TokenType terminal)
        {
            // يحفظ رمزاً طرفياً لكي يطابق Token من Lexer.
            IsTerminal = true;
            Terminal = terminal;
        }

        private Ll1GrammarSymbol(Ll1NonTerminal nonTerminal)
        {
            // يحفظ رمزاً غير طرفي لكي يبحث عنه في جدول LL(1).
            IsTerminal = false;
            NonTerminal = nonTerminal;
        }

        public bool IsTerminal { get; }
        public TokenType Terminal { get; }
        public Ll1NonTerminal NonTerminal { get; }

        // ينشئ تمثيل الرمز الطرفي المستخدم في ParseTable.
        public static Ll1GrammarSymbol ForTerminal(TokenType terminal) => new Ll1GrammarSymbol(terminal);

        // ينشئ تمثيل الرمز غير الطرفي المستخدم في ParseTable.
        public static Ll1GrammarSymbol ForNonTerminal(Ll1NonTerminal nonTerminal) => new Ll1GrammarSymbol(nonTerminal);
    }

    /// <summary>
    /// يمثل عقدة Concrete Parse Tree المطلوبة للفصل بين التحليل الجدولي وبناء AST.
    /// </summary>
    private sealed class ParseTreeNode
    {
        private ParseTreeNode(Ll1NonTerminal nonTerminal)
        {
            // يميز عقدة Grammar غير طرفية داخل شجرة الاشتقاق.
            NonTerminal = nonTerminal;
        }

        private ParseTreeNode(TokenType terminal)
        {
            // يميز عقدة طرفية ستستقبل Token بعد المطابقة.
            Terminal = terminal;
        }

        public Ll1NonTerminal? NonTerminal { get; }
        public TokenType? Terminal { get; }
        public Token? MatchedToken { get; set; }
        public List<ParseTreeNode> Children { get; } = new List<ParseTreeNode>();

        // ينشئ عقدة غير طرفية يوسعها جدول LL(1).
        public static ParseTreeNode ForNonTerminal(Ll1NonTerminal nonTerminal) => new ParseTreeNode(nonTerminal);

        // ينشئ عقدة طرفية يملؤها MatchTerminal.
        public static ParseTreeNode ForTerminal(TokenType terminal) => new ParseTreeNode(terminal);
    }

    /// <summary>
    /// يربط رمز Stack بعقدته المقابلة داخل Concrete Parse Tree.
    /// </summary>
    private sealed class ParseStackEntry
    {
        public ParseStackEntry(Ll1GrammarSymbol symbol, ParseTreeNode node)
        {
            // يحافظ على الرابط بين قرار الجدول وشجرة الاشتقاق التي سيستهلكها AST builder.
            Symbol = symbol;
            Node = node;
        }

        public Ll1GrammarSymbol Symbol { get; }
        public ParseTreeNode Node { get; }
    }
}

/// <summary>
/// يمثل خطوة تعليمية واحدة في تنفيذ محلل LL(1) داخل Stack وجدول التحليل.
/// </summary>
public sealed record Ll1TraceStep(int StepNumber, string Stack, string Lookahead, string Action);
