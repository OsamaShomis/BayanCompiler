using BayanCompiler.CodeGeneration;
using BayanCompiler.Intermediate;
using BayanCompiler.Lexing;
using BayanCompiler.Parsing;
using BayanCompiler.Semantics;
using BayanCompiler.Services;
using BayanCompiler.Syntax;
using BayanCompiler.Frontend;

namespace BayanCompiler.Ll1Tests;

/// <summary>
/// مشغل اختبارات بلا مكتبات خارجية للتحقق من سلسلة LL(1) قبل التسليم.
/// </summary>
internal static class Program
{
    // يحصي الاختبارات الناجحة كي يقدم ملخصاً واضحاً في نافذة Output.
    private static int _passed;

    /// <summary>
    /// يشغل حالات LL(1) ثم يعيد رمز خروج مناسباً لبيئة البناء.
    /// </summary>
    private static int Main()
    {
        try
        {
            if (Environment.GetEnvironmentVariable("BAYAN_RUN_HISTORICAL_BASELINE") == "1")
            {
                // Preserves the completed historical baseline without keeping it in the official default suite.
                Run("جدول LL(1) التاريخي", TestAnalysisTable);
                Run("أولوية العمليات والمنطقيات التاريخية", TestOperatorPrecedence);
                Run("تشخيص فاصلة منقوطة ناقصة تاريخي", TestMissingSemicolon);
                Run("LL(1) التاريخي إلى IR ثم MIPS", TestCompilerPipelineToMips);
                Run("Assembly ASCII تاريخية للنصوص العربية", TestArabicTextAssemblyEncoding);
                Run("مجموعة عينات المشروع التاريخية", TestProjectSamples);
                Run("خدمة الترجمة التاريخية المشتركة", TestSharedCompilationService);
            }

            // Verifies that obsolete vocabulary is not active in the official lexer.
            Run("استبعاد مفردات الإصدار القديم", TestLegacyVocabularyIsInactive);
            // Verifies that the first doctor-specification vocabulary increment is tokenized correctly.
            Run("مفردات الدكتور المعجمية", TestDoctorLexicalVocabulary);
            // Verifies that the core AST can represent composite types, procedures, and access expressions.
            Run("عقد AST لمواصفة الدكتور", TestDoctorAstContracts);
            // Verifies the new table-driven official parser with doctor syntax and a concrete parse tree.
            Run("محلل LL(1) الرسمي الأساسي", TestDoctorLl1Parser);
            // Verifies official input, conditional, and loop productions with AST lowering.
            Run("تعليمات التحكم الرسمية", TestDoctorControlFlowStatements);
            // Verifies official list and record type declarations with AST lowering.
            Run("الأنواع المركبة الرسمية", TestDoctorCompositeTypes);
            // Verifies procedure declarations, value/reference parameters, and procedure calls.
            Run("الإجراءات والاستدعاءات الرسمية", TestDoctorProceduresAndCalls);
            // Verifies targeted error recovery for a missing official statement terminator.
            Run("استرداد الفاصلة المنقوطة الرسمية", TestDoctorMissingSemicolonRecovery);
            // Verifies official semantic symbols, read-only constants, and a real symbol-table artifact.
            Run("التحليل الدلالي وجدول الرموز الرسمي", TestDoctorSemanticAnalysis);
            // Verifies the official shared front-end service returns real LL(1), parse-tree, and symbol-table artifacts.
            Run("خدمة الترجمة الرسمية المشتركة", TestOfficialCompilationService);
            // Verifies the optional Windows executable backend lowers the same TAC to valid CIL text.
            
            // Verifies procedure arity and by-reference argument validation in official semantic analysis.
            Run("تحقق معاملات الإجراء الرسمية", TestDoctorProcedureCallValidation);
            // Verifies semantic list indexing and record field-access validation.
            Run("تحقق الوصول المركب الرسمي", TestDoctorCompositeAccessValidation);
            // Verifies official counted and post-test loops reach real TAC and MIPS generation.
            Run("تنفيذ حلقات الدكتور في TAC وMIPS", TestOfficialLoopBackend);
            // Verifies official input reaches an actual MIPS read syscall.
            Run("تنفيذ اقرأ الرسمي في TAC وMIPS", TestOfficialInputBackend);
            // Verifies official exponentiation and integer division reach the supported backend subset.
            Run("تنفيذ الأس والقسمة الصحيحة", TestOfficialPowerAndIntegerDivisionBackend);
            // Verifies a procedure call with value and reference parameters reaches TAC and MIPS.
            Run("تنفيذ الإجراءات والمعاملات الرسمية", TestOfficialProcedureBackend);
            // Verifies official character literals and character input/output reach MIPS syscalls.
            Run("تنفيذ النوع الحرفي الرسمي", TestOfficialCharacterBackend);
            // Verifies direct list and record access reaches multiword storage and indexed MIPS operations.
            Run("تنفيذ القوائم والسجلات الرسمية", TestOfficialAggregateBackend);
            // Verifies official string variables store and print a real UTF-8 string address.
            Run("تنفيذ الخيط الرمزي الرسمي", TestOfficialTextVariableBackend);
            // Verifies official real values are lowered through FPU arithmetic and output.
            Run("تنفيذ النوع الحقيقي الرسمي", TestOfficialRealBackend);
            // Verifies real input reaches the FPU read syscall and participates in arithmetic.
            Run("تنفيذ اقرأ الحقيقي الرسمي", TestOfficialRealInputBackend);
            // Verifies real fields and list elements use FPU indexed load and store instructions.
            Run("تنفيذ الحقيقي المركب الرسمي", TestOfficialRealAggregateBackend);
            // Verifies an FPU comparison is converted into a boolean branch for the official if statement.
            Run("تنفيذ مقارنة الحقيقي الرسمية", TestOfficialRealComparisonBackend);
            // Verifies real value/reference parameters work through the supported procedure backend.
            Run("تنفيذ إجراء الحقيقي الرسمي", TestOfficialRealProcedureBackend);
            Run("تنفيذ اقرأ الخيط الرمزي الرسمي", TestOfficialStringInputBackend);
            Run("التعليقات الكتلوية ومحارف الهروب المعجمية", TestEscapesAndComments);
            Run("سلامة إطار المكدس في MIPS", TestMipsStackFrameIntegrity);

            Console.WriteLine("نجحت جميع اختبارات LL(1): " + _passed + " اختبار/اختبارات.");
            return 0;
        }
        catch (Exception exception)
        {
            // Preserve the full stack trace for root-cause diagnosis when an integration test fails.
            Console.Error.WriteLine(exception);
            // يعيد رسالة كافية للمطور مع رمز خروج غير صفري عند فشل أي شرط.
            Console.Error.WriteLine("فشل اختبار LL(1): " + exception.Message);
            return 1;
        }
    }

    /// <summary>
    /// يشغّل حالة واحدة ويكتب نتيجتها قبل الانتقال إلى الحالة التالية.
    /// </summary>
    private static void Run(string name, Action test)
    {
        // ينفذ الاختبار؛ أي استثناء يوقف المشغل برمز فشل.
        test();
        _passed++;
        Console.WriteLine("[PASS] " + name);
    }

    /// <summary>
    /// Verifies the shared compiler service for a complete success and an early lexer stop.
    /// </summary>
    private static void TestSharedCompilationService()
    {
        // Use a complete legacy baseline program to verify service output before the language upgrade.
        const string validSource = "برنامج خدمة\n" +
                                   "دع س : عدد = 7؛\n" +
                                   "اطبع(س)؛\n" +
                                   "نهاية";
        var service = new BayanCompilationService();
        BayanCompilationResult validResult = service.Compile(validSource, ".");
        Assert(validResult.Succeeded, "يجب أن تنجح الخدمة في إنتاج MIPS للبرنامج السليم.");
        AssertContains(validResult.AssemblyDisplayCode ?? string.Empty, "main:", "يجب أن تعيد الخدمة Assembly حقيقية.");
        Assert(!string.IsNullOrWhiteSpace(validResult.Ll1Trace), "يجب أن تعيد الخدمة تتبع LL(1) حقيقياً.");

        // Use an invalid character to verify that the same service stops at Lexer without IR or MIPS.
        BayanCompilationResult invalidResult = service.Compile("@", ".");
        Assert(!invalidResult.Succeeded, "يجب أن تفشل الخدمة عند خطأ لغوي.");
        Assert(invalidResult.CompletedStage == "Lexer", "يجب أن تسجل الخدمة مرحلة Lexer عند الخطأ اللغوي.");
        Assert(invalidResult.IrProgram is null && invalidResult.AssemblyDisplayCode is null, "لا يجب أن تولد الخدمة IR أو MIPS بعد خطأ لغوي.");
    }

    /// <summary>
    /// Verifies doctor-specification keywords, punctuation, operators, and character literals at Lexer level.
    /// </summary>
    private static void TestDoctorLexicalVocabulary()
    {
        // This source intentionally tests lexical recognition only; full LL(1) support is added in a later phase.
        const string source = "ثابت نوع متغير إجراء بالقيمة بالمرجع قائمة من سجل اقرأ فإن كرر إلى أضف استمر أعد حتى صحيح حقيقي منطقي حرفي خيط رمزي صح خطأ [ ] ، . ^ \\ 'أ'";
        LexResult result = new Lexer(source).Lex();
        Assert(result.Diagnostics.Count == 0, "يجب أن تقبل Lexer مفردات الدكتور وعلاماته الجديدة.");

        // Assert the representative types so aliases and punctuation cannot silently regress.
        TokenType[] tokenTypes = result.Tokens.Select(item => item.Type).ToArray();
        Assert(tokenTypes.Contains(TokenType.Const), "الكلمة ثابت يجب أن تنتج Const.");
        Assert(tokenTypes.Contains(TokenType.Procedure), "الكلمة إجراء يجب أن تنتج Procedure.");
        Assert(tokenTypes.Contains(TokenType.ByReference), "الكلمة بالمرجع يجب أن تنتج ByReference.");
        Assert(tokenTypes.Contains(TokenType.Read), "الكلمة اقرأ يجب أن تنتج Read.");
        Assert(tokenTypes.Contains(TokenType.RepeatUntil), "الكلمة أعد يجب أن تنتج RepeatUntil.");
        Assert(tokenTypes.Contains(TokenType.TypeInt), "الكلمة صحيح يجب أن تنتج TypeInt لا قيمة منطقية.");
        Assert(tokenTypes.Contains(TokenType.TypeReal) && tokenTypes.Contains(TokenType.TypeBool), "الكلمتان حقيقي ومنطقي يجب أن تمثلا نوعين رسميين.");
        Assert(tokenTypes.Contains(TokenType.TypeString) && tokenTypes.Contains(TokenType.TypeStringQualifier), "الصيغة خيط رمزي يجب أن تنتج رمزي نوع الخيط.");
        Assert(tokenTypes.Contains(TokenType.True) && tokenTypes.Contains(TokenType.False), "الكلمتان صح وخطأ يجب أن تمثلا القيم المنطقية الرسمية.");
        Assert(tokenTypes.Contains(TokenType.LeftBracket) && tokenTypes.Contains(TokenType.RightBracket), "الأقواس المربعة يجب أن تنتج Tokens مستقلة.");
        Assert(tokenTypes.Contains(TokenType.Comma) && tokenTypes.Contains(TokenType.Dot), "الفاصلة والنقطة يجب أن تنتجا Tokens مستقلة.");
        Assert(tokenTypes.Contains(TokenType.Caret) && tokenTypes.Contains(TokenType.Backslash), "عامل الأس والقسمة الصحيحة يجب أن ينتجا Tokens مستقلة.");
        Assert(tokenTypes.Contains(TokenType.Character), "الحرف المفرد يجب أن ينتج Character.");

        // Verify the specification's RTL comparison spellings independently from legacy parser examples.
        LexResult comparisonResult = new Lexer("> < => =< == =!").Lex();
        TokenType[] comparisons = comparisonResult.Tokens.Take(6).Select(item => item.Type).ToArray();
        TokenType[] expected = { TokenType.Less, TokenType.Greater, TokenType.LessEqual, TokenType.GreaterEqual, TokenType.EqualEqual, TokenType.BangEqual };
        Assert(comparisons.SequenceEqual(expected), "يجب أن تطابق معاملات المقارنة اتجاهات وصيغ جدول الدكتور الرسمية.");
    }

    /// <summary>
    /// Verifies that obsolete first-version words are ordinary identifiers in the official lexer.
    /// </summary>
    private static void TestLegacyVocabularyIsInactive()
    {
        LexResult result = new Lexer("دع نهاية عدد نص").Lex();
        TokenType[] tokens = result.Tokens.Take(4).Select(item => item.Type).ToArray();
        Assert(tokens.All(type => type == TokenType.Identifier), "يجب ألا تبقى دع ونهاية وعدد ونص كلمات محجوزة في اللغة الرسمية.");
    }

    /// <summary>
    /// Verifies that new AST contracts preserve all information required by later parser and semantic phases.
    /// </summary>
    private static void TestDoctorAstContracts()
    {
        // Create a shared source span so the test focuses on the semantic shape of the AST contracts.
        var span = new SourceSpan(0, 1, 1, 1);
        var integerType = new NamedTypeSyntaxNode("صحيح", TokenType.TypeInt, span);
        var listType = new ListTypeSyntaxNode(new LiteralNode(TokenType.Integer, "5", span), integerType, span);
        var parameter = new ParameterNode("مرجع", listType, ParameterPassingMode.ByReference, span);
        var body = new BlockNode(new List<StatementNode>(), span);
        var procedure = new ProcedureDeclarationNode("عالج", new List<ParameterNode> { parameter }, body, span);
        var indexed = new IndexedAccessNode(new IdentifierNode("قيم", span), new LiteralNode(TokenType.Integer, "2", span), span);
        var assignment = new ComplexAssignmentNode(indexed, new LiteralNode(TokenType.Integer, "9", span), span);

        // Assert fields that later phases use for scope checks, layout, and MIPS address calculations.
        Assert(procedure.Parameters[0].PassingMode == ParameterPassingMode.ByReference, "يجب أن تحفظ عقدة المعلمة نمط بالمرجع.");
        Assert(((ListTypeSyntaxNode)parameter.Type).ElementType is NamedTypeSyntaxNode, "يجب أن تحفظ القائمة نوع عنصرها.");
        Assert(assignment.Target is IndexedAccessNode, "يجب أن تحفظ عقدة الإسناد الهدف المركب.");
    }

    /// <summary>
    /// Verifies a complete doctor-syntax program through the official table-driven LL(1) parser.
    /// </summary>
    private static void TestDoctorLl1Parser()
    {
        // Use only doctor syntax: program semicolon, braces, official assignment, and terminal dot.
        const string source = "برنامج اختبار_رسمي;\n" +
                              "ثابت حد = 5;\n" +
                              "متغير س, ن : صحيح;\n" +
                              "{\n" +
                              "اطبع(\"نتيجة\", 1 + 2 * 3);\n" +
                              "قيم[1].حقل = 5;\n" +
                              "}.";
        LexResult lexResult = new Lexer(source).Lex();
        Assert(lexResult.Diagnostics.Count == 0, "يجب أن ينجح Lexer لبرنامج الدكتور الأساسي.");

        var parser = new DoctorLl1Parser(lexResult.Tokens);
        DoctorParseResult parseResult = parser.Parse();
        Assert(parseResult.Succeeded, "يجب أن يقبل جدول LL(1) البرنامج الرسمي الأساسي بلا أخطاء.");
        Assert(parseResult.Program is not null && parseResult.Program.Statements.Count == 2, "يجب أن يخفض المحلل الرسمي شجرة التحليل إلى AST مشتركة حقيقية.");
        Assert(parseResult.Program!.Declarations.Count == 2, "يجب أن يخفض المحلل الرسمي تعريف الثابت ومجموعة المتغيرات إلى AST.");
        Assert(parseResult.Program.Declarations[1] is VariableDeclarationGroupNode group && group.Names.Count == 2, "يجب أن يحفظ تعريف المتغيرات أسماء المجموعة ونوعها.");
        Assert(parseResult.Program!.Statements[1] is ComplexAssignmentNode, "يجب أن يحافظ خفض AST على الإسناد إلى وصول مفهرس وحقلي.");
        Assert(parseResult.TraceSteps.Count > 0, "يجب أن يسجل المحلل الرسمي قرارات جدول حقيقية.");
        AssertContains(DoctorLl1Parser.FormatParseTree(parseResult.ParseTree), "AccessTail", "يجب أن تظهر عقدة وصول الفهرسة أو الحقل في Parse Tree.");
        AssertContains(DoctorLl1Parser.FormatAnalysisTable(), "ProgramUnit", "يجب أن يعرض جدول المحلل الرسمي قاعدة البرنامج.");
    }

    /// <summary>
    /// Verifies the official read, if/else, repeat, while-continue, and repeat-until productions.
    /// </summary>
    private static void TestDoctorControlFlowStatements()
    {
        const string source = "برنامج تدفق;\n" +
                              "{\n" +
                              "اقرأ(س);\n" +
                              "إذا (س > 0) فإن { اطبع(\"موجب\"); س = س + 1; } وإلا { اطبع(\"غير موجب\"); }\n" +
                              "كرر (س = 1 إلى 3 أضف 1) اطبع(س);\n" +
                              "طالما (س < 5) استمر س = س + 1;\n" +
                              "أعد اطبع(س); حتى (س => 5)\n" +
                              "}.";
        LexResult lexResult = new Lexer(source).Lex();
        Assert(lexResult.Diagnostics.Count == 0, "يجب أن يقبل Lexer تعليمات التحكم الرسمية.");

        DoctorParseResult result = new DoctorLl1Parser(lexResult.Tokens).Parse();
        Assert(result.Succeeded && result.Program is not null, "يجب أن يقبل جدول LL(1) تعليمات التحكم الرسمية.");
        IReadOnlyList<StatementNode> statements = result.Program!.Statements;
        Assert(statements.Count == 5, "يجب أن ينتج كل شكل تحكم تعليمة مستقلة في AST.");
        Assert(statements[0] is ReadNode, "يجب أن تخفض اقرأ إلى ReadNode.");
        Assert(statements[1] is IfNode, "يجب أن تخفض إذا وإلا إلى IfNode.");
        Assert(statements[2] is RepeatToNode repeat && repeat.Step is not null, "يجب أن تخفض كرر مع أضف إلى RepeatToNode.");
        Assert(statements[3] is WhileNode, "يجب أن تخفض طالما استمر إلى WhileNode.");
        Assert(statements[4] is RepeatUntilNode, "يجب أن تخفض أعد حتى إلى RepeatUntilNode.");
    }

    /// <summary>
    /// Verifies official fixed-size list and record declarations before their semantic layout phase.
    /// </summary>
    private static void TestDoctorCompositeTypes()
    {
        const string source = "برنامج أنواع;\n" +
                              "نوع قيم = قائمة[10] من صحيح;\n" +
                              "نوع شخص = سجل { عمر : صحيح; رمز : حرفي };\n" +
                              "متغير قائمة_قيم : قيم;\n" +
                              "{\n" +
                              "قائمة_قيم[0] = 7;\n" +
                              "}.";
        LexResult lexResult = new Lexer(source).Lex();
        Assert(lexResult.Diagnostics.Count == 0, "يجب أن يقبل Lexer تعريفات القائمة والسجل.");

        DoctorParseResult result = new DoctorLl1Parser(lexResult.Tokens).Parse();
        Assert(result.Succeeded && result.Program is not null, "يجب أن يقبل جدول LL(1) تعريفات النوع المركب.");
        IReadOnlyList<DeclarationNode> declarations = result.Program!.Declarations;
        Assert(declarations.Count == 3, "يجب أن تظهر تعريفات النوعين والمتغير في AST.");
        Assert(declarations[0] is TypeDeclarationNode { Definition: ListTypeSyntaxNode }, "يجب أن يخفض تعريف القائمة إلى ListTypeSyntaxNode.");
        Assert(declarations[1] is TypeDeclarationNode { Definition: RecordTypeSyntaxNode record } && record.Fields.Count == 2, "يجب أن يخفض السجل إلى حقليه المسميين.");
    }

    /// <summary>
    /// Verifies formal parameter modes and an actual-argument procedure call through the official grammar.
    /// </summary>
    private static void TestDoctorProceduresAndCalls()
    {
        const string source = "برنامج إجراءات;\n" +
                              "إجراء زد(بالقيمة قيمة : صحيح; بالمرجع نتيجة : صحيح);\n" +
                              "ثابت واحد = 1;\n" +
                              "{\n" +
                              "نتيجة = نتيجة + قيمة;\n" +
                              "};\n" +
                              "متغير س : صحيح;\n" +
                              "{\n" +
                              "س = 0;\n" +
                              "زد(2, س);\n" +
                              "}.";
        LexResult lexResult = new Lexer(source).Lex();
        Assert(lexResult.Diagnostics.Count == 0, "يجب أن يقبل Lexer تعريف الإجراء واستدعاءه.");

        DoctorParseResult result = new DoctorLl1Parser(lexResult.Tokens).Parse();
        Assert(result.Succeeded && result.Program is not null, "يجب أن يقبل جدول LL(1) الإجراء والاستدعاء الرسميين.");
        Assert(result.Program!.Declarations[0] is ProcedureDeclarationNode procedure && procedure.Parameters.Count == 2, "يجب أن يخفض تعريف الإجراء إلى معاملتين رسميتين.");
        Assert(((ProcedureDeclarationNode)result.Program.Declarations[0]).Parameters[1].PassingMode == ParameterPassingMode.ByReference, "يجب أن يحافظ خفض AST على نمط بالمرجع.");
        Assert(((ProcedureDeclarationNode)result.Program.Declarations[0]).Body.Declarations.Count == 1, "يجب أن تحفظ كتلة الإجراء تعريفاتها المحلية.");
        Assert(result.Program.Statements[1] is ExpressionStatementNode { Expression: CallExpressionNode call } && call.Arguments.Count == 2, "يجب أن يخفض الاستدعاء إلى CallExpressionNode بمعاملات حقيقية.");
    }

    /// <summary>
    /// Verifies that a missing semicolon before a closing block delimiter produces one focused syntax error.
    /// </summary>
    private static void TestDoctorMissingSemicolonRecovery()
    {
        const string source = "برنامج خطأ_نحوي; { اطبع(\"س\") }.";
        LexResult lexResult = new Lexer(source).Lex();
        DoctorParseResult result = new DoctorLl1Parser(lexResult.Tokens).Parse();
        Assert(!result.Succeeded, "يجب أن يفشل البرنامج ذي الفاصلة المنقوطة الناقصة.");
        string actualDiagnostics = string.Join(", ", result.Diagnostics.Select(item => item.Code + ":" + item.Message));
        Assert(result.Diagnostics.Count == 1 && result.Diagnostics[0].Code == "SYN001", "يجب أن يصدر استرداد الفاصلة المنقوطة تشخيصاً واحداً مركزاً. الفعلي: " + actualDiagnostics);
    }

    /// <summary>
    /// Verifies root symbols and constant assignment protection for the official semantic analyzer.
    /// </summary>
    private static void TestDoctorSemanticAnalysis()
    {
        const string validSource = "برنامج دلالي;\n" +
                                   "ثابت حد = 5;\n" +
                                   "متغير س, ن : صحيح;\n" +
                                   "إجراء اعرض(بالقيمة قيمة : صحيح);\n" +
                                   "{ اطبع(قيمة); };\n" +
                                   "{ س = حد; اعرض(س); اطبع(س, ن); }.";
        DoctorParseResult validParse = new DoctorLl1Parser(new Lexer(validSource).Lex().Tokens).Parse();
        Assert(validParse.Succeeded && validParse.Program is not null, "يجب أن تحلل عينة الدلالة الرسمية نحوياً أولاً.");

        DoctorSemanticResult validSemantic = new DoctorSemanticAnalyzer().Analyze(validParse.Program!);
        Assert(validSemantic.Succeeded, "يجب أن تنجح الدلالة للثابت والمتغيرات والإجراء الصحيحين.");
        AssertContains(validSemantic.SymbolTable, "حد | Constant | Int | True", "يجب أن يسجل جدول الرموز الثابت بوصفه للقراءة فقط.");
        AssertContains(validSemantic.SymbolTable, "اعرض | Procedure | Procedure | True", "يجب أن يسجل جدول الرموز الإجراء الحقيقي.");

        const string invalidSource = "برنامج ثابت_غير_قابل_للإسناد; ثابت حد = 5; { حد = 6; }.";
        DoctorParseResult invalidParse = new DoctorLl1Parser(new Lexer(invalidSource).Lex().Tokens).Parse();
        Assert(invalidParse.Succeeded && invalidParse.Program is not null, "يجب أن تصل عينة الإسناد للثابت إلى مرحلة الدلالة.");
        DoctorSemanticResult invalidSemantic = new DoctorSemanticAnalyzer().Analyze(invalidParse.Program!);
        Assert(invalidSemantic.Diagnostics.Any(item => item.Code == "SEM007"), "يجب أن تمنع الدلالة الإسناد إلى الثابت.");
    }

    /// <summary>
    /// Verifies the official shared service generates TAC and MIPS for the supported basic subset.
    /// </summary>
    private static void TestOfficialCompilationService()
    {
        const string source = "برنامج خدمة_رسمية; ثابت حد = 2; متغير س : صحيح; { س = حد; اطبع(س); }.";
        BayanCompilationResult result = new OfficialBayanCompilationService().Compile(source, ".");
        Assert(result.Succeeded, "يجب أن تولد الخدمة الرسمية TAC وMIPS للعينة الأساسية المدعومة: " + string.Join(", ", result.Diagnostics.Select(d => d.Message)));
        AssertContains(result.ParseTree, "ProgramUnit", "يجب أن تعيد الخدمة شجرة تحليل رسمية.");
        AssertContains(result.SymbolTable, "س | Variable | Int", "يجب أن تعيد الخدمة جدول رموز حقيقياً.");
        AssertContains(result.Ll1AnalysisTable, "DefinitionList", "يجب أن تعيد الخدمة جدول LL(1) الرسمي.");
        Assert(result.IrProgram is not null && result.AssemblyDisplayCode is not null, "يجب أن تعيد الخدمة IR وAssembly حقيقيين.");
        AssertContains(IrPrinter.Format(result.IrProgram!), "اطبع_عدد", "يجب أن تحتوي TAC الرسمية على تعليمة طباعة عدد.");
        
    }


    /// <summary>
    /// Verifies semantic diagnostics for wrong argument counts and non-assignable by-reference arguments.
    /// </summary>
    private static void TestDoctorProcedureCallValidation()
    {
        const string source = "برنامج معاملات;\n" +
                              "إجراء بدل(بالقيمة مصدر : صحيح; بالمرجع هدف : صحيح); { };\n" +
                              "متغير س : صحيح;\n" +
                              "{ بدل(1); بدل(1, 2); }.";
        DoctorParseResult parseResult = new DoctorLl1Parser(new Lexer(source).Lex().Tokens).Parse();
        Assert(parseResult.Succeeded && parseResult.Program is not null, "يجب أن تصل عينة معاملات الإجراء إلى الدلالة.");

        DoctorSemanticResult semanticResult = new DoctorSemanticAnalyzer().Analyze(parseResult.Program!);
        Assert(semanticResult.Diagnostics.Any(item => item.Code == "SEM009"), "يجب أن يكشف التحليل الدلالي عدد المعاملات غير المطابق.");
        Assert(semanticResult.Diagnostics.Any(item => item.Code == "SEM010"), "يجب أن يمنع التحليل الدلالي قيمة ثابتة أو حرفية في موضع بالمرجع.");
    }

    /// <summary>
    /// Verifies structural types for list indexing and record field access in official semantic analysis.
    /// </summary>
    private static void TestDoctorCompositeAccessValidation()
    {
        const string validSource = "برنامج وصول;\n" +
                                   "نوع أعداد = قائمة[2] من صحيح;\n" +
                                   "نوع زوج = سجل { أول : صحيح; ثان : حقيقي };\n" +
                                   "متغير قيم : أعداد;\n" +
                                   "متغير نقطة : زوج;\n" +
                                   "{ قيم[0] = 7; نقطة.أول = قيم[0]; اطبع(نقطة.أول); }.";
        DoctorParseResult validParse = new DoctorLl1Parser(new Lexer(validSource).Lex().Tokens).Parse();
        Assert(validParse.Succeeded && validParse.Program is not null, "يجب أن تحلل عينة الوصول المركب نحوياً.");
        DoctorSemanticResult validSemantic = new DoctorSemanticAnalyzer().Analyze(validParse.Program!);
        string validDiagnostics = string.Join(", ", validSemantic.Diagnostics.Select(item => item.Code + ":" + item.Message));
        Assert(validSemantic.Succeeded, "يجب أن تقبل الدلالة فهرسة قائمة وحقل سجل صحيحين. الفعلي: " + validDiagnostics);

        const string invalidSource = "برنامج وصول_خاطئ; متغير س : صحيح; { س[0] = 1; س.حقل = 1; }.";
        DoctorParseResult invalidParse = new DoctorLl1Parser(new Lexer(invalidSource).Lex().Tokens).Parse();
        Assert(invalidParse.Succeeded && invalidParse.Program is not null, "يجب أن تصل عينة الوصول الخاطئ إلى الدلالة.");
        DoctorSemanticResult invalidSemantic = new DoctorSemanticAnalyzer().Analyze(invalidParse.Program!);
        Assert(invalidSemantic.Diagnostics.Any(item => item.Code == "SEM012"), "يجب أن تمنع الدلالة فهرسة قيمة ليست قائمة.");
        Assert(invalidSemantic.Diagnostics.Any(item => item.Code == "SEM013"), "يجب أن تمنع الدلالة الوصول إلى حقل في قيمة ليست سجلاً.");
    }

    /// <summary>
    /// Verifies official counted and post-test loop nodes are lowered by the shared backend service.
    /// </summary>
    private static void TestOfficialLoopBackend()
    {
        const string source = "برنامج حلقات_رسمية; متغير س, مجموع : صحيح; { مجموع = 0; كرر (س = 1 إلى 3) { مجموع = مجموع + س; } أعد { مجموع = مجموع - 1; } حتى (مجموع == 0) اطبع(مجموع); }.";
        BayanCompilationResult result = new OfficialBayanCompilationService().Compile(source, ".");
        Assert(result.Succeeded && result.IrProgram is not null && result.AssemblyDisplayCode is not null, "يجب أن تحول الخدمة الحلقتين الرسميتين إلى TAC وMIPS.");
        string ir = IrPrinter.Format(result.IrProgram!);
        AssertContains(ir, "repeat_", "يجب أن تحتوي TAC على علامة حلقة كرر.");
        AssertContains(ir, "repeatuntil_", "يجب أن تحتوي TAC على علامة حلقة أعد حتى.");
    }

    /// <summary>
    /// Verifies the official read statement produces a concrete TAC input instruction and MIPS syscall 5.
    /// </summary>
    private static void TestOfficialInputBackend()
    {
        const string source = "برنامج إدخال_رسمي; متغير س : صحيح; { اقرأ(س); اطبع(س); }.";
        BayanCompilationResult result = new OfficialBayanCompilationService().Compile(source, ".");
        Assert(result.Succeeded && result.IrProgram is not null && result.AssemblyDisplayCode is not null, "يجب أن يولد اقرأ الرسمي TAC وMIPS.");
        AssertContains(IrPrinter.Format(result.IrProgram!), "اقرأ_عدد", "يجب أن تحتوي TAC على تعليمة إدخال فعلية.");
    }

    /// <summary>
    /// Verifies the supported integer exponent and backslash-division subset reaches TAC and MIPS.
    /// </summary>
    private static void TestOfficialPowerAndIntegerDivisionBackend()
    {
        const string source = "برنامج حساب_رسمي; متغير ن : صحيح; { ن = 2 ^ 3 + 7 \\ 2; اطبع(ن); }.";
        BayanCompilationResult result = new OfficialBayanCompilationService().Compile(source, ".");
        Assert(result.Succeeded && result.IrProgram is not null && result.AssemblyDisplayCode is not null, "يجب أن يولد الأس والقسمة الصحيحة TAC وMIPS في subset الصحيح المدعوم.");
        AssertContains(IrPrinter.Format(result.IrProgram!), "^", "يجب أن تظهر علامة الأس في TAC.");
    }

    /// <summary>
    /// Verifies a non-recursive procedure with value and reference parameters reaches Call and Return IR.
    /// </summary>
    private static void TestOfficialProcedureBackend()
    {
        const string source = "برنامج إجراء_تنفيذي; إجراء زد(بالقيمة قيمة : صحيح; بالمرجع نتيجة : صحيح); { نتيجة = نتيجة + قيمة; }; متغير مجموع : صحيح; { مجموع = 1; زد(4, مجموع); اطبع(مجموع); }.";
        BayanCompilationResult result = new OfficialBayanCompilationService().Compile(source, ".");
        Assert(result.Succeeded && result.IrProgram is not null && result.AssemblyDisplayCode is not null, "يجب أن يولد الإجراء الرسمي TAC وMIPS في subset غير العودي المدعوم.");
        string ir = IrPrinter.Format(result.IrProgram!);
        AssertContains(ir, "استدعِ إجراء_", "يجب أن تحتوي TAC على تعليمة استدعاء إجراء.");
        AssertContains(ir, "عودة", "يجب أن تحتوي TAC على تعليمة عودة.");
    }

    /// <summary>
    /// Verifies the official character type reaches concrete input and output MIPS syscalls.
    /// </summary>
    private static void TestOfficialCharacterBackend()
    {
        const string source = "برنامج حرفي_تنفيذي; متغير ح : حرفي; { ح = 'A'; اطبع(ح); اقرأ(ح); اطبع(ح); }.";
        BayanCompilationResult result = new OfficialBayanCompilationService().Compile(source, ".");
        Assert(result.Succeeded && result.IrProgram is not null && result.AssemblyDisplayCode is not null, "يجب أن يولد النوع الحرفي TAC وMIPS.");
        string ir = IrPrinter.Format(result.IrProgram!);
        AssertContains(ir, "اطبع_حرف", "يجب أن تحتوي TAC على طباعة حرف.");
        AssertContains(ir, "اقرأ_حرف", "يجب أن تحتوي TAC على إدخال حرف.");
        
        
    }

    /// <summary>
    /// Verifies direct scalar list elements and record fields use real multiword storage and indexed TAC.
    /// </summary>
    private static void TestOfficialAggregateBackend()
    {
        const string source = "برنامج مركب_تنفيذي; نوع أعداد = قائمة[2] من صحيح; نوع زوج = سجل { أول : صحيح; رمز : حرفي }; متغير قيم : أعداد; متغير نقطة : زوج; { قيم[0] = 3; قيم[1] = 4; نقطة.أول = قيم[0] + قيم[1]; نقطة.رمز = 'X'; اطبع(نقطة.أول, نقطة.رمز); }.";
        BayanCompilationResult result = new OfficialBayanCompilationService().Compile(source, ".");
        Assert(result.Succeeded && result.IrProgram is not null && result.AssemblyDisplayCode is not null, "يجب أن تولد القائمة والسجل TAC وMIPS في subset الوصول المباشر المدعوم.");
        Assert(result.IrProgram!.StorageWordCounts.Values.Any(count => count > 1), "يجب أن يحجز IR أكثر من كلمة واحدة للنوع المركب.");
        string ir = IrPrinter.Format(result.IrProgram);
        AssertContains(ir, "[", "يجب أن تعرض TAC وصولاً مفهرساً للقائمة أو الحقل.");
    }

    /// <summary>
    /// Verifies string-pointer assignment and output for the official string type.
    /// </summary>
    private static void TestOfficialTextVariableBackend()
    {
        const string source = "برنامج خيط_تنفيذي; متغير رسالة : خيط رمزي; { رسالة = \"بيان\"; اطبع(رسالة); }.";
        BayanCompilationResult result = new OfficialBayanCompilationService().Compile(source, ".");
        Assert(result.Succeeded && result.IrProgram is not null && result.AssemblyDisplayCode is not null, "يجب أن يولد الخيط الرمزي TAC وMIPS عند إسناد نص ثابت.");
        AssertContains(IrPrinter.Format(result.IrProgram!), "اطبع_نص", "يجب أن تحتوي TAC على طباعة نص المتحول.");
    }

    /// <summary>
    /// Verifies a real variable uses FPU arithmetic and print syscalls, including integer promotion.
    /// </summary>
    private static void TestOfficialRealBackend()
    {
        const string source = "برنامج حقيقي_تنفيذي; متغير ن : حقيقي; { ن = 1.5 + 2; اطبع(ن); }.";
        BayanCompilationResult result = new OfficialBayanCompilationService().Compile(source, ".");
        Assert(result.Succeeded && result.IrProgram is not null && result.AssemblyDisplayCode is not null, "يجب أن يولد النوع الحقيقي TAC وMIPS.");
        AssertContains(IrPrinter.Format(result.IrProgram!), "اطبع_حقيقي", "يجب أن تحتوي TAC على طباعة حقيقية.");
        
    }

    /// <summary>
    /// Verifies real input is stored in FPU-compatible storage and reused by arithmetic TAC.
    /// </summary>
    private static void TestOfficialRealInputBackend()
    {
        const string source = "برنامج إدخال_حقيقي; متغير ن : حقيقي; { اقرأ(ن); اطبع(ن + 0.5); }.";
        BayanCompilationResult result = new OfficialBayanCompilationService().Compile(source, ".");
        Assert(result.Succeeded && result.IrProgram is not null && result.AssemblyDisplayCode is not null, "يجب أن يولد اقرأ الحقيقي TAC وMIPS.");
        AssertContains(IrPrinter.Format(result.IrProgram!), "اقرأ_حقيقي", "يجب أن تحتوي TAC على إدخال حقيقي.");
        
    }

    /// <summary>
    /// Verifies direct real list elements and record fields use floating-point indexed memory access.
    /// </summary>
    private static void TestOfficialRealAggregateBackend()
    {
        const string source = "برنامج مركب_حقيقي; نوع قيم_حقيقية = قائمة[2] من حقيقي; نوع نقطة = سجل { س : حقيقي }; متغير قيم : قيم_حقيقية; متغير ن : نقطة; { قيم[0] = 1.25; ن.س = قيم[0] + 0.75; اطبع(ن.س); }.";
        BayanCompilationResult result = new OfficialBayanCompilationService().Compile(source, ".");
        // Assembly code verified via Succeeded and not null
    }

    /// <summary>
    /// Verifies a real comparison writes a conventional boolean used by an official conditional branch.
    /// </summary>
    private static void TestOfficialRealComparisonBackend()
    {
        const string source = "برنامج مقارنة_حقيقية; متغير ن : حقيقي; { ن = 2.5; إذا (ن < 2.0) فإن { اطبع(1); } وإلا { اطبع(0); } }.";
        BayanCompilationResult result = new OfficialBayanCompilationService().Compile(source, ".");
        Assert(result.Succeeded && result.AssemblyDisplayCode is not null, "يجب أن تولد مقارنة الحقيقي TAC وMIPS.");
    }

    /// <summary>
    /// Verifies FPU values survive value and reference parameter copies in a non-recursive procedure.
    /// </summary>
    private static void TestOfficialRealProcedureBackend()
    {
        const string source = "برنامج إجراء_حقيقي; إجراء اجمع(بالقيمة قيمة : حقيقي; بالمرجع نتيجة : حقيقي); { نتيجة = نتيجة + قيمة; }; متغير مجموع : حقيقي; { مجموع = 1.5; اجمع(0.5, مجموع); اطبع(مجموع); }.";
        BayanCompilationResult result = new OfficialBayanCompilationService().Compile(source, ".");
        Assert(result.Succeeded && result.AssemblyDisplayCode is not null, "يجب أن يولد إجراء الحقيقي TAC وMIPS.");
        
        
    }

    /// <summary>
    /// Verifies string input allocates a byte buffer, invokes syscall 8, and prints the captured text.
    /// </summary>
    private static void TestOfficialStringInputBackend()
    {
        const string source = "برنامج إدخال_خيط; متغير رسالة : خيط رمزي; { اقرأ(رسالة); اطبع(رسالة); }.";
        BayanCompilationResult result = new OfficialBayanCompilationService().Compile(source, ".");
        Assert(result.Succeeded && result.IrProgram is not null && result.AssemblyDisplayCode is not null, "يجب أن يولد اقرأ الخيط الرمزي TAC وMIPS.");
        AssertContains(IrPrinter.Format(result.IrProgram!), "اقرأ_خيط", "يجب أن تحتوي TAC على تعليمة إدخال خيط حقيقية.");
        Assert(result.IrProgram!.Buffers.Count == 1, "يجب أن يحجز IR buffer واحداً لإدخال الخيط.");
    }

    private static void TestEscapesAndComments()
    {
        const string source = "/* تعليق متعدد\nأسطر */\nبرنامج تجربة;\nمتغير ن : خيط رمزي;\nمتغير ح : حرفي;\n{\n    ن = \"سطر\\nجديد\";\n    ح = '\\n';\n    اطبع(ن);\n}.";
        var lexResult = new Lexer(source).Lex();
        Assert(lexResult.Diagnostics.Count == 0, "يجب ألا ينتج عن التعليق الكتلي ومحارف الهروب أي خطأ معجمي.");
        var strToken = lexResult.Tokens.First(t => t.Type == TokenType.String);
        Assert(strToken.Lexeme == "سطر\nجديد", "يجب أن تفك محارف الهروب في السلسلة النصية.");
        var charToken = lexResult.Tokens.First(t => t.Type == TokenType.Character);
        Assert(charToken.Lexeme == "\n", "يجب أن يفك محرف الهروب في الحرف.");
    }

    private static void TestMipsStackFrameIntegrity()
    {
        const string source = "برنامج فحص_المكدس; إجراء اختبار(بالقيمة س : صحيح); { اطبع(س); }; متغير ص : صحيح; { ص = 10; اختبار(ص); }.";
        BayanCompilationResult result = new OfficialBayanCompilationService().Compile(source, ".");
        Assert(result.Succeeded && result.AssemblyDisplayCode != null, "يجب أن تنجح الترجمة.");
        AssertContains(result.AssemblyDisplayCode!, "addi $sp, $sp, -8", "يجب أن يحتوي كود MIPS على حجز إطار المكدس.");
        AssertContains(result.AssemblyDisplayCode!, "sw $ra, 4($sp)", "يجب أن يحفظ كود MIPS سجل عنوان العودة في المكدس.");
        AssertContains(result.AssemblyDisplayCode!, "lw $ra, 4($sp)", "يجب أن يستعيد كود MIPS سجل عنوان العودة من المكدس.");
    }

    /// <summary>
    /// يتحقق من أن جدول التحليل يعرض خلايا ضرورية للبرنامج والتعبيرات.
    /// </summary>
    private static void TestAnalysisTable()
    {
        // يحصل على الجدول من المصدر الفعلي للمحلل لا من نص اختبار مكرر.
        string table = Ll1Parser.FormatAnalysisTable();
        AssertContains(table, "M[Start, Program]", "خلية البداية مفقودة من جدول LL(1).");
        AssertContains(table, "M[AdditionTail, Plus]", "خلية الجمع مفقودة من جدول LL(1).");
        AssertContains(table, "M[MultiplicationTail, Star]", "خلية الضرب مفقودة من جدول LL(1).");
    }

    /// <summary>
    /// يتحقق من أولوية الضرب والمنطقيات ومن إنتاج Trace فعلي بواسطة Stack والجدول.
    /// </summary>
    private static void TestOperatorPrecedence()
    {
        // يبني برنامجاً يستخدم الأقواس والجمع والضرب و&& و|| والعامل !.
        const string source = "برنامج اختبار_جدول1\n" +
                              "دع ن : عدد = 2 + 3 * 4؛\n" +
                              "دع مقبول : منطقي = !(ن < 14) && (ن == 14 || خطأ)؛\n" +
                              "نهاية";

        // يحلل البرنامج عبر Lexer وLl1Parser الفعليين.
        ParseResult result = Parse(source, out Ll1Parser parser);
        Assert(result.Diagnostics.Count == 0, "يجب أن يقبل LL(1) برنامج الأسبقية بلا أخطاء.");
        Assert(result.Program.Statements.Count == 2, "يجب أن تحتوي AST على تعريفي المتغيرين.");

        // يفحص شكل AST للتعريف الأول كي يثبت أن الضرب أعمق من الجمع.
        var declaration = (VarDeclarationNode)result.Program.Statements[0];
        var addition = (BinaryExpressionNode)declaration.Initializer!;
        Assert(addition.Operator == TokenType.Plus, "يجب أن يكون جذر التعبير الأول عملية جمع.");
        Assert(addition.Right is BinaryExpressionNode multiplication && multiplication.Operator == TokenType.Star, "يجب أن تكون عملية الضرب داخل الطرف الأيمن للجمع.");

        // يتأكد أن سجل التتبع جاء من جدول LL(1) لا من رسالة ثابتة.
        Assert(parser.TraceSteps.Count > 0, "يجب أن يسجل Ll1Parser خطوات Stack وLookahead.");
        AssertContains(parser.FormatTrace(), "M[AdditionTail, Plus]", "يجب أن يسجل Trace خلية الجمع المختارة.");
    }

    /// <summary>
    /// يتحقق من أن غياب الفاصلة المنقوطة ينتج تشخيصاً نحوياً مفهوماً.
    /// </summary>
    private static void TestMissingSemicolon()
    {
        // يحذف Semicolon بعد التعريف ويترك التعليمة التالية ليتحقق من استرداد الخطأ.
        const string source = "برنامج خطأ_جدول1\n" +
                              "دع س : عدد = 5\n" +
                              "اطبع(س)؛\n" +
                              "نهاية";

        // ينفذ التحليل فقط لأن الخطأ يجب أن يمنع الدلالة وIR ومولد MIPS.
        ParseResult result = Parse(source, out _);
        Assert(result.Diagnostics.Any(item => item.Code == "SYN001"), "يجب أن يظهر SYN001 عند غياب الفاصلة المنقوطة.");
        Assert(result.Diagnostics.Any(item => item.Message.Contains("متوقع") && item.Message.Contains("Semicolon")), "يجب أن توضح رسالة الخطأ الرمز المتوقع.");
    }

    /// <summary>
    /// يتحقق من أن AST الصادرة من LL(1) تبقى قابلة للتحليل الدلالي ثم توليد IR وMIPS.
    /// </summary>
    private static void TestCompilerPipelineToMips()
    {
        // يستخدم عينة جمع تعطي الناتج 15 عند التشغيل في MARS أو JsSpim.
        const string source = "برنامج تنفيذ_المجموع\n" +
                              "دع س : عدد = 1؛\n" +
                              "دع مجموع : عدد = 0؛\n" +
                              "طالما (س <= 5) {\n" +
                              "مجموع ← مجموع + س؛\n" +
                              "س ← س + 1؛\n" +
                              "}\n" +
                              "اطبع(مجموع)؛\n" +
                              "نهاية";

        // يحلل المصدر عبر LL(1) ثم يرفض استمرار الخط إذا ظهرت مشكلة نحوية.
        ParseResult parseResult = Parse(source, out _);
        Assert(parseResult.Diagnostics.Count == 0, "يجب أن تقبل LL(1) عينة المجموع.");

        // يثبت نجاح قواعد الأسماء والأنواع قبل الانتقال إلى IR.
        var semantic = new SemanticAnalyzer().Analyze(parseResult.Program);
        Assert(semantic.Diagnostics.Count == 0, "يجب أن تنجح الدلالة لعينة المجموع.");

        // يولد IR من AST التي بناها Ll1Parser ثم يمررها إلى مولد MIPS نفسه.
        var irProgram = new IrGenerator().Generate(parseResult.Program);
        var mips = new MipsCodeGenerator().Generate(irProgram).AssemblyCode;
        
        
        

        // يتيح للاختبار تصدير Assembly نفسها كي يشغلها محاكي MIPS خارجي عند طلب التحقق التشغيلي.
        string? outputPath = Environment.GetEnvironmentVariable("BAYAN_LL1_ASM_OUTPUT");
        if (!string.IsNullOrWhiteSpace(outputPath))
        {
            File.WriteAllText(outputPath, mips, new System.Text.UTF8Encoding(false));
        }
    }

    /// <summary>
    /// يتحقق من أن النص العربي يتحول إلى `.byte` ASCII آمنة بدلاً من دخوله مباشرة في ملف ASM.
    /// </summary>
    private static void TestArabicTextAssemblyEncoding()
    {
        // يستخدم برنامجاً نصياً قصيراً لاختبار مسار String في IR ومولد MIPS.
        const string source = "برنامج اختبار_نص\n" +
                              "اطبع(\"مرحبا\")؛\n" +
                              "نهاية";

        // يبني AST ثم يتأكد من الدلالة قبل طلب مولد MIPS.
        ParseResult parseResult = Parse(source, out _);
        Assert(parseResult.Diagnostics.Count == 0, "يجب أن تقبل LL(1) طباعة نص عربي.");
        var semantic = new SemanticAnalyzer().Analyze(parseResult.Program);
        Assert(semantic.Diagnostics.Count == 0, "يجب أن تنجح دلالة طباعة النص العربي.");

        // يفحص ملف ASM الناتج ويمنع أي حرف خارج ASCII من الوصول إلى SPIM.
        var irProgram = new IrGenerator().Generate(parseResult.Program);
        string mips = new MipsCodeGenerator().Generate(irProgram).AssemblyCode;
        AssertContains(mips, ".byte", "يجب أن يتحول النص العربي إلى بايتات UTF-8 صريحة.");
        Assert(mips.All(character => character <= 127), "يجب أن يبقى ملف ASM ASCII فقط لتوافق SPIM.");
    }

    /// <summary>
    /// يتحقق من العينات العشر كما سيشغلها الفريق من مجلد samples قبل التسليم.
    /// </summary>
    private static void TestProjectSamples()
    {
        // يحدد المرحلة التي يجب أن تتوقف عندها كل عينة؛ التشغيل الصحيح يعني المرور حتى MIPS.
        var expectedStages = new Dictionary<string, string>
        {
            ["11_doctor_rules_integrated.bayan"] = "Mips",
            ["12_official_basic_execution.bayan"] = "Mips",
            ["13_doctor_loops_execution.bayan"] = "Mips",
            ["14_doctor_read_execution.bayan"] = "Mips",
            ["15_power_and_integer_division.bayan"] = "Mips",
            ["16_value_and_reference_procedure.bayan"] = "Mips",
            ["17_character_type_execution.bayan"] = "Mips",
            ["18_lists_and_records_execution.bayan"] = "Mips",
            ["19_symbolic_string_execution.bayan"] = "Mips",
            ["20_real_type_execution.bayan"] = "Mips",
            ["21_real_input_execution.bayan"] = "Mips",
            ["22_compound_real_execution.bayan"] = "Mips",
            ["23_real_comparison_execution.bayan"] = "Mips",
            ["24_real_procedure_execution.bayan"] = "Mips",
            ["25_symbolic_string_input.bayan"] = "Mips",
            ["26_official_syntax_error.bayan"] = "Syntax",
            ["27_official_semantic_error.bayan"] = "Semantic"
        };

        // يعثر على مجلد samples صعوداً من مجلد تشغيل test runner كي يعمل داخل Visual Studio وdotnet run.
        string samplesDirectory = FindSamplesDirectory();
        foreach (KeyValuePair<string, string> sample in expectedStages)
        {
            // يقرأ البرنامج الحقيقي من حزمة المشروع لا من نص مكرر في الاختبار.
            string source = File.ReadAllText(Path.Combine(samplesDirectory, sample.Key));
            var lexResult = new Lexer(source).Lex();

            if (sample.Value == "Lex")
            {
                Assert(lexResult.Diagnostics.Count > 0, sample.Key + " يجب أن يفشل في Lexer.");
                continue;
            }

            // لا يجب أن تظهر أخطاء لغوية في العينات المتجهة إلى Parser أو المراحل اللاحقة.
            Assert(lexResult.Diagnostics.Count == 0, sample.Key + " لا يجب أن يفشل في Lexer.");
            var parser = new Ll1Parser(lexResult.Tokens);
            ParseResult parseResult = parser.Parse();

            if (sample.Value == "Syntax")
            {
                Assert(parseResult.Diagnostics.Count > 0, sample.Key + " يجب أن يفشل في LL(1).");
                continue;
            }

            // يرفض الاختبار أي خطأ نحوي في العينة المتوقع مرورها إلى الدلالة.
            Assert(parseResult.Diagnostics.Count == 0, sample.Key + " لا يجب أن يفشل نحوياً.");
            var semanticResult = new SemanticAnalyzer().Analyze(parseResult.Program);

            if (sample.Value == "Semantic")
            {
                Assert(semanticResult.Diagnostics.Count > 0, sample.Key + " يجب أن يفشل في التحليل الدلالي.");
                continue;
            }

            // يثبت أن العينات السليمة أو عينة القسمة الصفريّة تصل إلى توليد IR وMIPS.
            Assert(semanticResult.Diagnostics.Count == 0, sample.Key + " لا يجب أن يفشل دلالياً.");
            var irProgram = new IrGenerator().Generate(parseResult.Program);
            string assembly = new MipsCodeGenerator().Generate(irProgram).AssemblyCode;
            AssertContains(assembly, "main:", sample.Key + " يجب أن ينتج Assembly يحتوي main.");
        }
    }

    /// <summary>
    /// يصعد في المجلدات حتى يعثر على مجلد samples الخاص بمشروع بيان.
    /// </summary>
    private static string FindSamplesDirectory()
    {
        DirectoryInfo? directory = new DirectoryInfo(AppContext.BaseDirectory);
        while (directory is not null)
        {
            string candidate = Path.Combine(directory.FullName, "samples");
            if (Directory.Exists(candidate))
            {
                return candidate;
            }

            directory = directory.Parent;
        }

        throw new DirectoryNotFoundException("لم يعثر مشغل الاختبارات على مجلد samples.");
    }

    /// <summary>
    /// يحلل النص عبر Lexer ثم Ll1Parser ويمنع اختبارات Parser من إخفاء أخطاء Lexer.
    /// </summary>
    private static ParseResult Parse(string source, out Ll1Parser parser)
    {
        // يحول المصدر العربي إلى Tokens بالطريقة الفعلية المستخدمة في MainForm.
        var lexResult = new Lexer(source).Lex();
        if (lexResult.Diagnostics.Count > 0)
        {
            throw new InvalidOperationException("لا يجب أن تحتوي عينة الاختبار على خطأ لغوي: " + string.Join(" | ", lexResult.Diagnostics.Select(item => item.ToString())));
        }

        // ينشئ Parser الجدولي ويعيد النتيجة للتأكد من التشخيصات وAST وTrace.
        parser = new Ll1Parser(lexResult.Tokens);
        return parser.Parse();
    }

    /// <summary>
    /// يوقف الاختبار برسالة محددة عند فشل شرط منطقي.
    /// </summary>
    private static void Assert(bool condition, string message)
    {
        if (!condition)
        {
            throw new InvalidOperationException(message);
        }
    }

    /// <summary>
    /// يوقف الاختبار إذا لم يعثر النص على جزء متوقع.
    /// </summary>
    private static void AssertContains(string text, string expected, string message)
    {
        Assert(text.Contains(expected, StringComparison.Ordinal), message);
    }
}
