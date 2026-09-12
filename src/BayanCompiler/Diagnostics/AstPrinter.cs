using System.Text;
using BayanCompiler.Syntax;

namespace BayanCompiler.Diagnostics;

/// <summary>
/// يحول شجرة AST إلى نص هرمي لعرض شكل البرنامج بعد التحليل النحوي.
/// </summary>
public static class AstPrinter
{
    /// <summary>
    /// ينسق عقدة البرنامج وكل التعليمات التابعة لها.
    /// </summary>
    public static string Format(ProgramNode program)
    {
        // يجمع النص قبل عرضه في RichTextBox الخاص بالواجهة.
        StringBuilder builder = new StringBuilder();

        // يعرض الجذر الذي يضم اسم البرنامج.
        builder.AppendLine("ProgramNode: " + program.Name);

        if (program.Declarations.Count > 0)
        {
            AppendLine(builder, 1, "Declarations:");
            foreach (DeclarationNode declaration in program.Declarations)
            {
                AppendDeclaration(builder, declaration, 2);
            }
        }

        // يعرض التعليمات بترتيبها في البرنامج.
        foreach (StatementNode statement in program.Statements)
        {
            AppendStatement(builder, statement, 1);
        }

        // يعيد النص النهائي إلى Windows Forms.
        return builder.ToString();
    }

    /// <summary>
    /// Formats official top-level declarations before executable statements.
    /// </summary>
    private static void AppendDeclaration(StringBuilder builder, DeclarationNode declaration, int depth)
    {
        if (declaration is ConstantDeclarationNode constant)
        {
            AppendLine(builder, depth, "ConstantDeclarationNode: " + constant.Name);
            AppendExpression(builder, constant.Value, depth + 1);
            return;
        }

        if (declaration is TypeDeclarationNode typeDeclaration)
        {
            AppendLine(builder, depth, "TypeDeclarationNode: " + typeDeclaration.Name + " = " + typeDeclaration.Definition.GetType().Name);
            return;
        }

        if (declaration is VariableDeclarationGroupNode variables)
        {
            AppendLine(builder, depth, "VariableDeclarationGroupNode: " + string.Join(", ", variables.Names) + " : " + variables.Type.GetType().Name);
            return;
        }

        if (declaration is ProcedureDeclarationNode procedure)
        {
            AppendLine(builder, depth, "ProcedureDeclarationNode: " + procedure.Name);
            foreach (ParameterNode parameter in procedure.Parameters)
            {
                AppendLine(builder, depth + 1, "ParameterNode: " + parameter.PassingMode + " " + parameter.Name);
            }

            AppendBlock(builder, procedure.Body, depth + 1);
            return;
        }

        AppendLine(builder, depth, declaration.GetType().Name);
    }

    /// <summary>
    /// يطبع أي نوع من أنواع العبارات في الشجرة.
    /// </summary>
    private static void AppendStatement(StringBuilder builder, StatementNode statement, int depth)
    {
        // يطبع تعريف المتغير وقيمة التهيئة إن وجدت.
        if (statement is VarDeclarationNode declaration)
        {
            AppendLine(builder, depth, "VarDeclarationNode: " + declaration.Name + " : " + declaration.TypeName);
            if (declaration.Initializer is not null)
            {
                AppendLine(builder, depth + 1, "Initializer:");
                AppendExpression(builder, declaration.Initializer, depth + 2);
            }

            return;
        }

        // يطبع تعليمة الإسناد ثم التعبير الذي يمثل القيمة الجديدة.
        if (statement is AssignmentNode assignment)
        {
            AppendLine(builder, depth, "AssignmentNode: " + assignment.Name);
            AppendExpression(builder, assignment.Expression, depth + 1);
            return;
        }

        // يطبع تعليمة الطباعة ثم التعبير الذي ستعرضه.
        if (statement is PrintNode print)
        {
            AppendLine(builder, depth, "PrintNode:");
            AppendExpression(builder, print.Expression, depth + 1);
            return;
        }

        // يطبع الشرط وفرع إذا وفرع وإلا عند وجوده.
        if (statement is IfNode ifNode)
        {
            AppendLine(builder, depth, "IfNode:");
            AppendLine(builder, depth + 1, "Condition:");
            AppendExpression(builder, ifNode.Condition, depth + 2);
            AppendLine(builder, depth + 1, "Then:");
            AppendBlock(builder, ifNode.ThenBlock, depth + 2);

            if (ifNode.ElseBlock is not null)
            {
                AppendLine(builder, depth + 1, "Else:");
                AppendBlock(builder, ifNode.ElseBlock, depth + 2);
            }

            return;
        }

        // يطبع شرط الحلقة ثم جسمها.
        if (statement is WhileNode whileNode)
        {
            AppendLine(builder, depth, "WhileNode:");
            AppendLine(builder, depth + 1, "Condition:");
            AppendExpression(builder, whileNode.Condition, depth + 2);
            AppendLine(builder, depth + 1, "Body:");
            AppendBlock(builder, whileNode.Body, depth + 2);
            return;
        }

        // يطبع كتلة مستقلة إن أضيفت مباشرة إلى البرنامج.
        if (statement is BlockNode block)
        {
            AppendBlock(builder, block, depth);
            return;
        }

        // يعرض اسم النوع عند إضافة عقدة جديدة لم تحدث الطابعة بعد.
        AppendLine(builder, depth, statement.GetType().Name);
    }

    /// <summary>
    /// يطبع قائمة تعليمات داخل BlockNode.
    /// </summary>
    private static void AppendBlock(StringBuilder builder, BlockNode block, int depth)
    {
        // يوضح بداية الكتلة في النص الهرمي.
        AppendLine(builder, depth, "BlockNode:");

        // يطبع كل تعليمة داخل الكتلة بمستوى أعمق.
        foreach (StatementNode statement in block.Statements)
        {
            AppendStatement(builder, statement, depth + 1);
        }
    }

    /// <summary>
    /// يطبع تعبيراً وعقده الداخلية حسب نوعه.
    /// </summary>
    private static void AppendExpression(StringBuilder builder, ExpressionNode expression, int depth)
    {
        // يطبع الأعداد والنصوص والقيم المنطقية.
        if (expression is LiteralNode literal)
        {
            AppendLine(builder, depth, "LiteralNode: " + literal.Value);
            return;
        }

        // يطبع اسم المتغير المستخدم في التعبير.
        if (expression is IdentifierNode identifier)
        {
            AppendLine(builder, depth, "IdentifierNode: " + identifier.Name);
            return;
        }

        // يطبع العامل الثنائي وطرفيه بالتسلسل.
        if (expression is BinaryExpressionNode binary)
        {
            AppendLine(builder, depth, "BinaryExpressionNode: " + binary.Operator);
            AppendExpression(builder, binary.Left, depth + 1);
            AppendExpression(builder, binary.Right, depth + 1);
            return;
        }

        // يطبع العامل الأحادي مثل ! أو - ثم التعبير التابع له.
        if (expression is UnaryExpressionNode unary)
        {
            AppendLine(builder, depth, "UnaryExpressionNode: " + unary.Operator);
            AppendExpression(builder, unary.Operand, depth + 1);
            return;
        }

        // يطبع التعبير الموضوع بين قوسين.
        if (expression is GroupingNode grouping)
        {
            AppendLine(builder, depth, "GroupingNode:");
            AppendExpression(builder, grouping.Expression, depth + 1);
            return;
        }

        // يعرض اسم النوع عند إضافة تعبير جديد لاحقاً.
        AppendLine(builder, depth, expression.GetType().Name);
    }

    /// <summary>
    /// يضيف سطراً بمسافة بادئة تساوي عمق العقدة داخل الشجرة.
    /// </summary>
    private static void AppendLine(StringBuilder builder, int depth, string text)
    {
        // يضيف أربع مسافات لكل مستوى من مستويات AST.
        builder.Append(' ', depth * 4);

        // يضيف نص العقدة ثم ينتقل إلى السطر التالي.
        builder.AppendLine(text);
    }
}
