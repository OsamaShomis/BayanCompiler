using BayanCompiler.Frontend;
using BayanCompiler.Lexing;
using BayanCompiler.Syntax;

namespace BayanCompiler.Semantics;

/// <summary>
/// يفحص AST بعد نجاح Parser للتأكد من صحة الأسماء والأنواع قبل تنفيذ البرنامج.
/// </summary>
public sealed class SemanticAnalyzer
{
    // يجمع الأخطاء الدلالية لعرضها في تبويب الأخطاء بالواجهة.
    private readonly DiagnosticBag _diagnostics = new DiagnosticBag();

    // يشير إلى نطاق المتغيرات الحالي أثناء المرور في البرنامج.
    private SymbolTable _scope = new SymbolTable();

    /// <summary>
    /// ينفذ الفحص الدلالي للبرنامج كاملاً ويعيد جميع التشخيصات.
    /// </summary>
    public SemanticResult Analyze(ProgramNode program)
    {
        // يزور تعليمات البرنامج بالترتيب حتى تتاح التعريفات للتعليمات التالية.
        foreach (StatementNode statement in program.Statements)
        {
            VisitStatement(statement);
        }

        // يعيد قائمة الأخطاء إلى واجهة Windows Forms.
        return new SemanticResult(_diagnostics.Items);
    }

    /// <summary>
    /// يفحص عبارة واحدة وفق نوع عقدة AST.
    /// </summary>
    private void VisitStatement(StatementNode statement)
    {
        // يفحص تعريف المتغير ونوع قيمته الابتدائية.
        if (statement is VarDeclarationNode declaration)
        {
            VisitVarDeclaration(declaration);
            return;
        }

        // يفحص أن متغير الإسناد معرف وأن القيمة توافق نوعه.
        if (statement is AssignmentNode assignment)
        {
            VisitAssignment(assignment);
            return;
        }

        // يفحص التعبير الذي سيطبع.
        if (statement is PrintNode print)
        {
            AnalyzeExpression(print.Expression);
            return;
        }

        // يفحص أن شرط إذا منطقي ثم يدخل كل كتلة في نطاق فرعي.
        if (statement is IfNode ifNode)
        {
            VisitIf(ifNode);
            return;
        }

        // يفحص أن شرط طالما منطقي ثم يدخل جسم الحلقة في نطاق فرعي.
        if (statement is WhileNode whileNode)
        {
            VisitWhile(whileNode);
            return;
        }

        // يسمح بتحليل BlockNode إن استعملت مباشرة في توسعة مستقبلية.
        if (statement is BlockNode block)
        {
            VisitBlock(block);
        }
    }

    /// <summary>
    /// يضيف المتغير إلى النطاق الحالي ويتحقق من نوع القيمة الابتدائية.
    /// </summary>
    private void VisitVarDeclaration(VarDeclarationNode declaration)
    {
        // يحول الاسم العربي للنوع إلى النوع الداخلي للمترجم.
        LanguageType declaredType = ParseDeclaredType(declaration.TypeName);

        // ينشئ الرمز الذي سيستعمله البحث عن الأسماء لاحقاً.
        Symbol symbol = new Symbol(declaration.Name, declaredType, declaration.Span);

        // يمنع تكرار تعريف الاسم داخل النطاق نفسه.
        if (!_scope.TryDeclare(symbol))
        {
            Report("SEM002", "المتغير \"" + declaration.Name + "\" معرف مسبقاً في هذا النطاق.", declaration.Span);
        }

        // ينهي الفحص إن لم يكن هناك تعبير ابتدائي.
        if (declaration.Initializer is null)
        {
            return;
        }

        // يستنتج نوع التعبير الابتدائي.
        LanguageType initializerType = AnalyzeExpression(declaration.Initializer);

        // يتحقق من إمكان تخزين القيمة في نوع المتغير المعلن.
        if (!CanAssign(declaredType, initializerType))
        {
            Report("SEM003", "لا يمكن إسناد قيمة من النوع " + DisplayType(initializerType) + " إلى المتغير \"" + declaration.Name + "\" من النوع " + DisplayType(declaredType) + ".", declaration.Initializer.Span);
        }
    }

    /// <summary>
    /// يفحص إسناد قيمة إلى متغير معرّف مسبقاً.
    /// </summary>
    private void VisitAssignment(AssignmentNode assignment)
    {
        // يبحث عن المتغير في النطاق الحالي ثم النطاقات الخارجية.
        if (!_scope.TryLookup(assignment.Name, out Symbol symbol))
        {
            // يسجل استخدام اسم غير معرف في جهة الإسناد.
            Report("SEM001", "المتغير \"" + assignment.Name + "\" غير معرّف قبل استخدامه.", assignment.Span);

            // يحلل التعبير أيضاً لاكتشاف أخطاء مستقلة داخله.
            AnalyzeExpression(assignment.Expression);
            return;
        }

        // يستنتج نوع القيمة الجديدة المراد إسنادها.
        LanguageType expressionType = AnalyzeExpression(assignment.Expression);

        // يتحقق من توافق القيمة مع نوع المتغير الموجود في جدول الرموز.
        if (!CanAssign(symbol.Type, expressionType))
        {
            Report("SEM003", "لا يمكن إسناد قيمة من النوع " + DisplayType(expressionType) + " إلى المتغير \"" + assignment.Name + "\" من النوع " + DisplayType(symbol.Type) + ".", assignment.Expression.Span);
        }
    }

    /// <summary>
    /// يفحص شرط إذا ثم يحلل كل فرع في نطاق مستقل.
    /// </summary>
    private void VisitIf(IfNode ifNode)
    {
        // يستنتج نوع الشرط قبل الدخول إلى أي فرع.
        LanguageType conditionType = AnalyzeExpression(ifNode.Condition);

        // يشترط أن يكون الشرط منطقياً.
        RequireBool(conditionType, ifNode.Condition.Span, "شرط إذا");

        // يحلل كتلة إذا في نطاق فرعي.
        VisitBlock(ifNode.ThenBlock);

        // يحلل كتلة وإلا في نطاق مستقل عند وجودها.
        if (ifNode.ElseBlock is not null)
        {
            VisitBlock(ifNode.ElseBlock);
        }
    }

    /// <summary>
    /// يفحص شرط طالما ثم يحلل جسم الحلقة في نطاق فرعي.
    /// </summary>
    private void VisitWhile(WhileNode whileNode)
    {
        // يستنتج نوع شرط الحلقة.
        LanguageType conditionType = AnalyzeExpression(whileNode.Condition);

        // يشترط أن يكون شرط الحلقة منطقياً.
        RequireBool(conditionType, whileNode.Condition.Span, "شرط طالما");

        // يحلل جسم الحلقة دون تسريب تعريفات الكتلة إلى الخارج.
        VisitBlock(whileNode.Body);
    }

    /// <summary>
    /// يحلل تعليمات كتلة في نطاق فرعي ثم يعيد النطاق السابق.
    /// </summary>
    private void VisitBlock(BlockNode block)
    {
        // يحفظ النطاق السابق للعودة إليه بعد نهاية الكتلة.
        SymbolTable parentScope = _scope;

        // ينشئ نطاقاً جديداً يرى المتغيرات الخارجية.
        _scope = parentScope.CreateChildScope();

        // يحلل تعليمات الكتلة بالترتيب.
        foreach (StatementNode statement in block.Statements)
        {
            VisitStatement(statement);
        }

        // يعيد النطاق السابق عند الخروج من القوس المعقوف.
        _scope = parentScope;
    }

    /// <summary>
    /// يستنتج نوع تعبير ويبلغ عن العمليات ذات الأنواع غير المتوافقة.
    /// </summary>
    private LanguageType AnalyzeExpression(ExpressionNode expression)
    {
        // يعيد نوع القيمة الثابتة مباشرة.
        if (expression is LiteralNode literal)
        {
            return TypeFromLiteral(literal.LiteralType);
        }

        // يبحث عن نوع المتغير المستخدم في التعبير.
        if (expression is IdentifierNode identifier)
        {
            return AnalyzeIdentifier(identifier);
        }

        // يعيد نوع التعبير الداخلي بين القوسين.
        if (expression is GroupingNode grouping)
        {
            return AnalyzeExpression(grouping.Expression);
        }

        // يفحص العامل الأحادي مثل ! أو -.
        if (expression is UnaryExpressionNode unary)
        {
            return AnalyzeUnary(unary);
        }

        // يفحص العامل الثنائي مثل + أو <= أو &&.
        if (expression is BinaryExpressionNode binary)
        {
            return AnalyzeBinary(binary);
        }

        // يمنع تراكم أخطاء إضافية عند عقدة غير معروفة.
        return LanguageType.Unknown;
    }

    /// <summary>
    /// يستنتج نوع اسم متغير أو يسجل خطأ إذا لم يكن معرفاً.
    /// </summary>
    private LanguageType AnalyzeIdentifier(IdentifierNode identifier)
    {
        // يبحث عن الاسم في النطاق الحالي ثم الأب.
        if (_scope.TryLookup(identifier.Name, out Symbol symbol))
        {
            return symbol.Type;
        }

        // يسجل الخطأ المطلوب لملف 05_semantic_error.bayan.
        Report("SEM001", "المتغير \"" + identifier.Name + "\" غير معرّف قبل استخدامه.", identifier.Span);

        // يعيد Unknown كي لا تتولد رسائل أنواع مكررة لنفس السبب.
        return LanguageType.Unknown;
    }

    /// <summary>
    /// يفحص العامل الأحادي ويعيد النوع الناتج.
    /// </summary>
    private LanguageType AnalyzeUnary(UnaryExpressionNode unary)
    {
        // يستنتج نوع القيمة الواقعة بعد العامل.
        LanguageType operandType = AnalyzeExpression(unary.Operand);

        // يعالج النفي المنطقي !.
        if (unary.Operator == TokenType.Bang)
        {
            RequireBool(operandType, unary.Operand.Span, "العامل !");
            return LanguageType.Bool;
        }

        // يعالج السالب الأحادي -.
        if (unary.Operator == TokenType.Minus)
        {
            if (!IsNumeric(operandType) && operandType != LanguageType.Unknown)
            {
                Report("SEM005", "العامل - يحتاج قيمة عددية أو حقيقية.", unary.Span);
            }

            return operandType;
        }

        // يعيد Unknown لأي عامل أحادي غير متوقع.
        return LanguageType.Unknown;
    }

    /// <summary>
    /// يفحص العامل الثنائي ويعيد نوع التعبير الناتج.
    /// </summary>
    private LanguageType AnalyzeBinary(BinaryExpressionNode binary)
    {
        // يستنتج نوع الطرف الأيسر.
        LanguageType leftType = AnalyzeExpression(binary.Left);

        // يستنتج نوع الطرف الأيمن.
        LanguageType rightType = AnalyzeExpression(binary.Right);

        // يعالج الجمع والطرح والضرب والقسمة والباقي.
        if (binary.Operator == TokenType.Plus ||
            binary.Operator == TokenType.Minus ||
            binary.Operator == TokenType.Star ||
            binary.Operator == TokenType.Slash ||
            binary.Operator == TokenType.Percent)
        {
            return AnalyzeArithmetic(binary, leftType, rightType);
        }

        // يعالج المقارنات العددية.
        if (binary.Operator == TokenType.Less ||
            binary.Operator == TokenType.LessEqual ||
            binary.Operator == TokenType.Greater ||
            binary.Operator == TokenType.GreaterEqual)
        {
            if (!AreNumeric(leftType, rightType) && !HasUnknown(leftType, rightType))
            {
                Report("SEM005", "عامل المقارنة يحتاج قيمتين عدديتين أو حقيقيتين.", binary.Span);
            }

            return LanguageType.Bool;
        }

        // يعالج المساواة وعدم المساواة.
        if (binary.Operator == TokenType.EqualEqual || binary.Operator == TokenType.BangEqual)
        {
            if (!AreComparable(leftType, rightType) && !HasUnknown(leftType, rightType))
            {
                Report("SEM005", "لا يمكن مقارنة النوع " + DisplayType(leftType) + " بالنوع " + DisplayType(rightType) + ".", binary.Span);
            }

            return LanguageType.Bool;
        }

        // يعالج && و|| ويشترط قيمتين منطقيتين.
        if (binary.Operator == TokenType.AndAnd || binary.Operator == TokenType.OrOr)
        {
            if ((leftType != LanguageType.Bool || rightType != LanguageType.Bool) && !HasUnknown(leftType, rightType))
            {
                Report("SEM005", "العامل المنطقي يحتاج قيمتين من النوع منطقي.", binary.Span);
            }

            return LanguageType.Bool;
        }

        // يعيد Unknown لأي عامل غير مدعوم بعد.
        return LanguageType.Unknown;
    }

    /// <summary>
    /// يفحص العمليات الحسابية ويحدد نوع النتيجة.
    /// </summary>
    private LanguageType AnalyzeArithmetic(BinaryExpressionNode binary, LanguageType leftType, LanguageType rightType)
    {
        // يسمح بجمع نصين فقط عند استخدام +.
        if (binary.Operator == TokenType.Plus && leftType == LanguageType.Text && rightType == LanguageType.Text)
        {
            return LanguageType.Text;
        }

        // يمنع العمليات الحسابية عند وجود نوع غير عددي.
        if (!AreNumeric(leftType, rightType))
        {
            if (!HasUnknown(leftType, rightType))
            {
                Report("SEM005", "العامل الحسابي يحتاج قيمتين من النوع عدد أو حقيقي.", binary.Span);
            }

            return LanguageType.Unknown;
        }

        // تعطي القسمة دائماً نتيجة حقيقية لتجنب فقدان الكسور.
        if (binary.Operator == TokenType.Slash)
        {
            return LanguageType.Real;
        }

        // يحافظ الباقي على النوع عدد فقط.
        if (binary.Operator == TokenType.Percent)
        {
            if (leftType != LanguageType.Int || rightType != LanguageType.Int)
            {
                Report("SEM005", "عامل الباقي % يحتاج قيمتين من النوع عدد.", binary.Span);
                return LanguageType.Unknown;
            }

            return LanguageType.Int;
        }

        // ينتج حقيقي إذا كان أحد الطرفين حقيقياً، وإلا ينتج عدد.
        return leftType == LanguageType.Real || rightType == LanguageType.Real
            ? LanguageType.Real
            : LanguageType.Int;
    }

    /// <summary>
    /// يتأكد من أن الشرط منطقي، مع تجنب رسالة إضافية بعد خطأ سابق.
    /// </summary>
    private void RequireBool(LanguageType type, SourceSpan span, string context)
    {
        // لا يضيف خطأ أنواع جديداً إذا كان التعبير مجهولاً بسبب خطأ اسم سابق.
        if (type == LanguageType.Unknown)
        {
            return;
        }

        // يسجل الخطأ عندما لا يكون الشرط منطقياً.
        if (type != LanguageType.Bool)
        {
            Report("SEM004", context + " يجب أن يكون من النوع منطقي، لكنه من النوع " + DisplayType(type) + ".", span);
        }
    }

    /// <summary>
    /// يحول اسم النوع في المصدر إلى النوع الداخلي.
    /// </summary>
    private static LanguageType ParseDeclaredType(string typeName)
    {
        // يطابق الأسماء العربية المعرفة في language-spec.md.
        return typeName switch
        {
            "عدد" => LanguageType.Int,
            "حقيقي" => LanguageType.Real,
            "منطقي" => LanguageType.Bool,
            "نص" => LanguageType.Text,
            _ => LanguageType.Unknown
        };
    }

    /// <summary>
    /// يحول نوع Token الثابت إلى نوع داخلي.
    /// </summary>
    private static LanguageType TypeFromLiteral(TokenType type)
    {
        // يطابق Tokens القيم التي أنشأها Lexer.
        return type switch
        {
            TokenType.Integer => LanguageType.Int,
            TokenType.Real => LanguageType.Real,
            TokenType.String => LanguageType.Text,
            TokenType.True => LanguageType.Bool,
            TokenType.False => LanguageType.Bool,
            _ => LanguageType.Unknown
        };
    }

    /// <summary>
    /// يحدد إن كان نوعان رقميين، مع السماح بعدد وحقيقي معاً.
    /// </summary>
    private static bool AreNumeric(LanguageType left, LanguageType right)
    {
        // يتطلب أن يكون الطرفان عدداً أو حقيقياً.
        return IsNumeric(left) && IsNumeric(right);
    }

    /// <summary>
    /// يحدد إن كان نوع واحد رقمياً.
    /// </summary>
    private static bool IsNumeric(LanguageType type)
    {
        // يقبل العدد الصحيح والحقيقي فقط.
        return type == LanguageType.Int || type == LanguageType.Real;
    }

    /// <summary>
    /// يحدد إن كان نوعان قابلين للمقارنة بالمساواة.
    /// </summary>
    private static bool AreComparable(LanguageType left, LanguageType right)
    {
        // يسمح بمقارنة نفس النوع، ويسمح بمقارنة عدد وحقيقي معاً.
        return left == right || AreNumeric(left, right);
    }

    /// <summary>
    /// يحدد ما إذا كان أحد النوعين مجهولاً بسبب خطأ سابق.
    /// </summary>
    private static bool HasUnknown(LanguageType left, LanguageType right)
    {
        // يمنع رسائل مشتقة غير مفيدة من خطأ أصلي.
        return left == LanguageType.Unknown || right == LanguageType.Unknown;
    }

    /// <summary>
    /// يحدد إن كانت قيمة المصدر يمكن إسنادها إلى نوع الهدف.
    /// </summary>
    private static bool CanAssign(LanguageType target, LanguageType source)
    {
        // لا يضيف خطأ توافق ثانياً عند وجود خطأ سابق في المصدر.
        if (source == LanguageType.Unknown || target == LanguageType.Unknown)
        {
            return true;
        }

        // يسمح بإسناد نفس النوع.
        if (target == source)
        {
            return true;
        }

        // يسمح بترقية عدد إلى حقيقي.
        return target == LanguageType.Real && source == LanguageType.Int;
    }

    /// <summary>
    /// يحول النوع الداخلي إلى اسم عربي مناسب لرسائل الأخطاء.
    /// </summary>
    private static string DisplayType(LanguageType type)
    {
        // يعيد الاسم العربي المختصر لكل نوع.
        return type switch
        {
            LanguageType.Int => "عدد",
            LanguageType.Real => "حقيقي",
            LanguageType.Bool => "منطقي",
            LanguageType.Text => "نص",
            LanguageType.Void => "فارغ",
            _ => "غير معروف"
        };
    }

    /// <summary>
    /// يضيف تشخيصاً دلالياً مرتبطاً بموقع في المصدر.
    /// </summary>
    private void Report(string code, string message, SourceSpan span)
    {
        // يمرر التشخيص إلى الحاوية المشتركة مع Lexer وParser.
        _diagnostics.Report(code, message, span);
    }
}

/// <summary>
/// يجمع تشخيصات التحليل الدلالي في نتيجة واحدة للواجهة.
/// </summary>
public sealed record SemanticResult(IReadOnlyList<Diagnostic> Diagnostics);
