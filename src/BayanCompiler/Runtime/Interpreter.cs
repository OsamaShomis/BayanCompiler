using System.Globalization;
using System.Text;
using BayanCompiler.Frontend;
using BayanCompiler.Lexing;
using BayanCompiler.Semantics;
using BayanCompiler.Syntax;

namespace BayanCompiler.Runtime;

/// <summary>
/// ينفذ شجرة AST بعد نجاح التحليل اللغوي والنحوي والدلالي.
/// </summary>
public sealed class Interpreter
{
    // يجمع ناتج أوامر اطبع لعرضه لاحقاً في تبويب الناتج.
    private readonly StringBuilder _output = new StringBuilder();

    // يمثل نطاق المتغيرات الحالي أثناء التنفيذ.
    private Environment _environment = new Environment();

    /// <summary>
    /// ينفذ تعليمات البرنامج كاملاً ويعيد النص الناتج من أوامر اطبع.
    /// </summary>
    public ExecutionResult Execute(ProgramNode program)
    {
        // يمر على تعليمات البرنامج بالترتيب المصدر.
        foreach (StatementNode statement in program.Statements)
        {
            ExecuteStatement(statement);
        }

        // يعيد النص النهائي إلى واجهة Windows Forms.
        return new ExecutionResult(_output.ToString());
    }

    /// <summary>
    /// ينفذ تعليمة واحدة حسب نوع عقدة AST.
    /// </summary>
    private void ExecuteStatement(StatementNode statement)
    {
        // ينفذ تعريف المتغير ويخزن قيمته في البيئة الحالية.
        if (statement is VarDeclarationNode declaration)
        {
            ExecuteVarDeclaration(declaration);
            return;
        }

        // ينفذ تحديث قيمة متغير معرف مسبقاً.
        if (statement is AssignmentNode assignment)
        {
            RuntimeValue value = Evaluate(assignment.Expression);
            _environment.Assign(assignment.Name, value);
            return;
        }

        // ينفذ الطباعة ويضيف القيمة كسطر إلى الناتج.
        if (statement is PrintNode print)
        {
            RuntimeValue value = Evaluate(print.Expression);
            _output.AppendLine(value.ToString());
            return;
        }

        // ينفذ فرع إذا أو وإلا وفق قيمة الشرط.
        if (statement is IfNode ifNode)
        {
            ExecuteIf(ifNode);
            return;
        }

        // يكرر جسم طالما ما دام الشرط صحيحاً.
        if (statement is WhileNode whileNode)
        {
            ExecuteWhile(whileNode);
            return;
        }

        // يسمح بتنفيذ كتلة مستقلة في أي توسعة مستقبلية.
        if (statement is BlockNode block)
        {
            ExecuteBlock(block);
        }
    }

    /// <summary>
    /// ينفذ تعريف المتغير بقيمته الابتدائية أو بقيمة افتراضية عند غيابها.
    /// </summary>
    private void ExecuteVarDeclaration(VarDeclarationNode declaration)
    {
        // يختار القيمة الابتدائية من التعبير أو القيمة الافتراضية للنوع.
        RuntimeValue value = declaration.Initializer is null
            ? CreateDefaultValue(declaration.TypeName, declaration.Span)
            : Evaluate(declaration.Initializer);

        // يحفظ المتغير في البيئة الحالية ليستخدم في التعليمات اللاحقة.
        _environment.Define(declaration.Name, value);
    }

    /// <summary>
    /// ينفذ شرط إذا داخل نطاقات مستقلة للكتل.
    /// </summary>
    private void ExecuteIf(IfNode ifNode)
    {
        // يحسب الشرط ثم يقرأ قيمته المنطقية.
        bool condition = Evaluate(ifNode.Condition).AsBool();

        // ينفذ كتلة إذا عندما يتحقق الشرط.
        if (condition)
        {
            ExecuteBlock(ifNode.ThenBlock);
            return;
        }

        // ينفذ كتلة وإلا عند عدم تحقق الشرط ووجود الفرع البديل.
        if (ifNode.ElseBlock is not null)
        {
            ExecuteBlock(ifNode.ElseBlock);
        }
    }

    /// <summary>
    /// ينفذ حلقة طالما مع حماية من الحلقة اللانهائية غير المقصودة.
    /// </summary>
    private void ExecuteWhile(WhileNode whileNode)
    {
        // يعد التكرارات لحماية البرنامج من استهلاك الواجهة بلا حدود.
        int iterations = 0;

        // يعيد تقييم الشرط قبل كل دورة من دورات الحلقة.
        while (Evaluate(whileNode.Condition).AsBool())
        {
            // يوقف التنفيذ عند حد معقول ويعطي رسالة مرتبطة بشرط الحلقة.
            if (iterations++ >= 1_000_000)
            {
                throw new RuntimeException("RUN002", "تجاوزت الحلقة طالما الحد الأقصى الآمن للتكرار.", whileNode.Condition.Span);
            }

            // ينفذ جسم الحلقة في نطاق فرعي جديد لكل دورة.
            ExecuteBlock(whileNode.Body);
        }
    }

    /// <summary>
    /// ينفذ كتلة تعليمات داخل بيئة فرعية ثم يعيد البيئة السابقة.
    /// </summary>
    private void ExecuteBlock(BlockNode block)
    {
        // يحتفظ ببيئة النطاق الخارجي للعودة إليها بعد القوس المعقوف.
        Environment previous = _environment;

        // ينشئ نطاقاً يرى المتغيرات الخارجية ويحمي تعريفات الكتلة المحلية.
        _environment = previous.CreateChild();

        try
        {
            // ينفذ جميع تعليمات الكتلة بالترتيب.
            foreach (StatementNode statement in block.Statements)
            {
                ExecuteStatement(statement);
            }
        }
        finally
        {
            // يضمن العودة للنطاق السابق حتى عند حصول خطأ وقت التشغيل.
            _environment = previous;
        }
    }

    /// <summary>
    /// يحسب قيمة تعبير واحد داخل بيئة التنفيذ الحالية.
    /// </summary>
    private RuntimeValue Evaluate(ExpressionNode expression)
    {
        // يحول الثابت إلى قيمة وقت تشغيل.
        if (expression is LiteralNode literal)
        {
            return EvaluateLiteral(literal);
        }

        // يجلب قيمة المتغير من البيئة الحالية أو من نطاق خارجي.
        if (expression is IdentifierNode identifier)
        {
            return _environment.Get(identifier.Name);
        }

        // يعيد نتيجة التعبير الداخلي بين القوسين.
        if (expression is GroupingNode grouping)
        {
            return Evaluate(grouping.Expression);
        }

        // ينفذ العامل الأحادي مثل ! أو -.
        if (expression is UnaryExpressionNode unary)
        {
            return EvaluateUnary(unary);
        }

        // ينفذ العامل الثنائي مثل + أو <= أو &&.
        if (expression is BinaryExpressionNode binary)
        {
            return EvaluateBinary(binary);
        }

        // يرفع خطأ واضحاً عند وصول عقدة غير معروفة إلى Runtime.
        throw new RuntimeException("RUN001", "تعذر تنفيذ نوع تعبير غير مدعوم.", expression.Span);
    }

    /// <summary>
    /// يحول LiteralNode إلى RuntimeValue حسب نوع Token الأصلي.
    /// </summary>
    private static RuntimeValue EvaluateLiteral(LiteralNode literal)
    {
        // يحول العدد الصحيح من النص إلى int.
        if (literal.LiteralType == TokenType.Integer)
        {
            return RuntimeValue.FromInt(int.Parse(literal.Value, CultureInfo.InvariantCulture));
        }

        // يحول العدد الحقيقي من النص إلى double.
        if (literal.LiteralType == TokenType.Real)
        {
            return RuntimeValue.FromReal(double.Parse(literal.Value, CultureInfo.InvariantCulture));
        }

        // يعيد النص كما استخرجه Lexer من دون علامتي الاقتباس.
        if (literal.LiteralType == TokenType.String)
        {
            return RuntimeValue.FromText(literal.Value);
        }

        // يحول الكلمات المنطقية إلى قيم bool.
        if (literal.LiteralType == TokenType.True)
        {
            return RuntimeValue.FromBool(true);
        }

        if (literal.LiteralType == TokenType.False)
        {
            return RuntimeValue.FromBool(false);
        }

        // يرفع خطأ عند ثابت لا يستطيع Runtime تفسيره.
        throw new RuntimeException("RUN001", "ثابت غير مدعوم أثناء التنفيذ.", literal.Span);
    }

    /// <summary>
    /// ينفذ العامل الأحادي ! أو -.
    /// </summary>
    private RuntimeValue EvaluateUnary(UnaryExpressionNode unary)
    {
        // يحسب القيمة التي يتبعها العامل الأحادي.
        RuntimeValue operand = Evaluate(unary.Operand);

        // ينفذ النفي المنطقي.
        if (unary.Operator == TokenType.Bang)
        {
            return RuntimeValue.FromBool(!operand.AsBool());
        }

        // ينفذ السالب الأحادي مع الحفاظ على نوع عدد أو حقيقي.
        if (unary.Operator == TokenType.Minus)
        {
            return operand.Type == LanguageType.Real
                ? RuntimeValue.FromReal(-operand.AsNumber())
                : RuntimeValue.FromInt(-operand.AsInt());
        }

        // يرفع خطأ عند عامل أحادي غير مدعوم.
        throw new RuntimeException("RUN001", "عامل أحادي غير مدعوم أثناء التنفيذ.", unary.Span);
    }

    /// <summary>
    /// ينفذ عاملًا ثنائياً بين قيمتين.
    /// </summary>
    private RuntimeValue EvaluateBinary(BinaryExpressionNode binary)
    {
        // يحسب الطرف الأيسر من التعبير.
        RuntimeValue left = Evaluate(binary.Left);

        // ينفذ && باختصار منطقي لتجنب حساب الطرف الأيمن عند عدم الحاجة.
        if (binary.Operator == TokenType.AndAnd)
        {
            return left.AsBool()
                ? RuntimeValue.FromBool(Evaluate(binary.Right).AsBool())
                : RuntimeValue.FromBool(false);
        }

        // ينفذ || باختصار منطقي لتجنب حساب الطرف الأيمن عند عدم الحاجة.
        if (binary.Operator == TokenType.OrOr)
        {
            return left.AsBool()
                ? RuntimeValue.FromBool(true)
                : RuntimeValue.FromBool(Evaluate(binary.Right).AsBool());
        }

        // يحسب الطرف الأيمن لباقي العمليات الثنائية.
        RuntimeValue right = Evaluate(binary.Right);

        // ينفذ الجمع أو دمج نصين.
        if (binary.Operator == TokenType.Plus)
        {
            if (left.Type == LanguageType.Text && right.Type == LanguageType.Text)
            {
                return RuntimeValue.FromText(left.AsText() + right.AsText());
            }

            return CreateNumericValue(left, right, left.AsNumber() + right.AsNumber());
        }

        // ينفذ الطرح.
        if (binary.Operator == TokenType.Minus)
        {
            return CreateNumericValue(left, right, left.AsNumber() - right.AsNumber());
        }

        // ينفذ الضرب.
        if (binary.Operator == TokenType.Star)
        {
            return CreateNumericValue(left, right, left.AsNumber() * right.AsNumber());
        }

        // ينفذ القسمة مع منع القسمة على صفر.
        if (binary.Operator == TokenType.Slash)
        {
            if (right.AsNumber() == 0)
            {
                throw new RuntimeException("RUN001", "لا يمكن القسمة على صفر.", binary.Right.Span);
            }

            return RuntimeValue.FromReal(left.AsNumber() / right.AsNumber());
        }

        // ينفذ باقي القسمة للأعداد الصحيحة فقط.
        if (binary.Operator == TokenType.Percent)
        {
            if (right.AsInt() == 0)
            {
                throw new RuntimeException("RUN001", "لا يمكن حساب الباقي عند القسمة على صفر.", binary.Right.Span);
            }

            return RuntimeValue.FromInt(left.AsInt() % right.AsInt());
        }

        // ينفذ المقارنات العددية.
        if (binary.Operator == TokenType.Less)
        {
            return RuntimeValue.FromBool(left.AsNumber() < right.AsNumber());
        }

        if (binary.Operator == TokenType.LessEqual)
        {
            return RuntimeValue.FromBool(left.AsNumber() <= right.AsNumber());
        }

        if (binary.Operator == TokenType.Greater)
        {
            return RuntimeValue.FromBool(left.AsNumber() > right.AsNumber());
        }

        if (binary.Operator == TokenType.GreaterEqual)
        {
            return RuntimeValue.FromBool(left.AsNumber() >= right.AsNumber());
        }

        // ينفذ المقارنة بالمساواة وعدم المساواة.
        if (binary.Operator == TokenType.EqualEqual)
        {
            return RuntimeValue.FromBool(AreEqual(left, right));
        }

        if (binary.Operator == TokenType.BangEqual)
        {
            return RuntimeValue.FromBool(!AreEqual(left, right));
        }

        // يرفع خطأ عند عامل ثنائي غير مدعوم.
        throw new RuntimeException("RUN001", "عامل ثنائي غير مدعوم أثناء التنفيذ.", binary.Span);
    }

    /// <summary>
    /// ينشئ نتيجة عددية ويحافظ على حقيقي عند وجود قيمة حقيقية في أحد الطرفين.
    /// </summary>
    private static RuntimeValue CreateNumericValue(RuntimeValue left, RuntimeValue right, double value)
    {
        // يعيد حقيقي عند وجود قيمة حقيقية في أي طرف.
        if (left.Type == LanguageType.Real || right.Type == LanguageType.Real)
        {
            return RuntimeValue.FromReal(value);
        }

        // يعيد عددًا صحيحًا لبقية العمليات العددية الصحيحة.
        return RuntimeValue.FromInt(Convert.ToInt32(value, CultureInfo.InvariantCulture));
    }

    /// <summary>
    /// يقارن قيمتين مع السماح بمقارنة عدد وحقيقي بالقيمة العددية.
    /// </summary>
    private static bool AreEqual(RuntimeValue left, RuntimeValue right)
    {
        // يقارن القيم العددية بعد تحويلها إلى صيغة مشتركة.
        if (IsNumeric(left) && IsNumeric(right))
        {
            return left.AsNumber() == right.AsNumber();
        }

        // يقارن النصوص أو المنطقيات مباشرة.
        return Equals(left.Value, right.Value);
    }

    /// <summary>
    /// يحدد إن كانت القيمة من النوع عدد أو حقيقي.
    /// </summary>
    private static bool IsNumeric(RuntimeValue value)
    {
        // يقبل النوعين العدديين فقط.
        return value.Type == LanguageType.Int || value.Type == LanguageType.Real;
    }

    /// <summary>
    /// ينشئ قيمة افتراضية عند تعريف متغير من دون قيمة ابتدائية.
    /// </summary>
    private static RuntimeValue CreateDefaultValue(string typeName, SourceSpan span)
    {
        // يطابق أسماء الأنواع العربية المعتمدة في لغة بيان.
        return typeName switch
        {
            "عدد" => RuntimeValue.FromInt(0),
            "حقيقي" => RuntimeValue.FromReal(0),
            "منطقي" => RuntimeValue.FromBool(false),
            "نص" => RuntimeValue.FromText(string.Empty),
            _ => throw new RuntimeException("RUN001", "نوع متغير غير مدعوم أثناء التنفيذ.", span)
        };
    }
}

/// <summary>
/// يمثل الناتج النصي لتنفيذ برنامج بيان.
/// </summary>
public sealed record ExecutionResult(string Output);

/// <summary>
/// يمثل خطأً يحدث وقت التشغيل مع رمز وموقع في المصدر.
/// </summary>
public sealed class RuntimeException : Exception
{
    /// <summary>
    /// ينشئ خطأ تنفيذ مرتبطاً بموقع عقدة AST.
    /// </summary>
    public RuntimeException(string code, string message, SourceSpan span)
        : base(message)
    {
        // يحفظ رمز الخطأ لعرضه في تبويب الأخطاء.
        Code = code;

        // يحفظ موقع الخطأ في النص المصدر.
        Span = span;
    }

    /// <summary>
    /// رمز الخطأ وقت التشغيل.
    /// </summary>
    public string Code { get; }

    /// <summary>
    /// موقع العقدة التي تسبب فيها الخطأ.
    /// </summary>
    public SourceSpan Span { get; }

    /// <summary>
    /// يعيد نصاً مناسباً لعرض الخطأ في الواجهة.
    /// </summary>
    public override string ToString()
    {
        // يربط الرمز والموقع والرسالة في سطر تشخيص واحد.
        return "[" + Code + "] " + Span + ": " + Message;
    }
}
