using System.Text;
using BayanCompiler.Syntax;

namespace BayanCompiler.Diagnostics;

/// <summary>
/// يحول شجرة AST إلى تمثيل هرمي نقي ومجرد يعكس نموذج لغة بيان بدقة تامة.
/// </summary>
public static class AstPrinter
{
    /// <summary>
    /// ينسق عقدة البرنامج وكل التعليمات التابعة لها.
    /// </summary>
    public static string Format(ProgramNode program)
    {
        StringBuilder builder = new StringBuilder();

        // يعرض الجذر الذي يضم اسم البرنامج.
        builder.AppendLine("ProgramNode: " + program.Name);

        if (program.Declarations.Count > 0)
        {
            AppendLine(builder, 1, "Declarations");
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

        return builder.ToString();
    }

    /// <summary>
    /// يطبع التعريفات الرأسية مع تفكيك الأنواع والمعرفات كعقد مستقلة.
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
            AppendLine(builder, depth, "TypeDeclarationNode: " + typeDeclaration.Name);
            AppendType(builder, typeDeclaration.Definition, depth + 1);
            return;
        }

        if (declaration is VariableDeclarationGroupNode variables)
        {
            AppendLine(builder, depth, "VariableDeclarationGroupNode");
            foreach (string name in variables.Names)
            {
                AppendLine(builder, depth + 1, "IdentifierNode: " + name);
            }
            AppendType(builder, variables.Type, depth + 1);
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

    private static void AppendType(StringBuilder builder, TypeSyntaxNode type, int depth)
    {
        if (type is NamedTypeSyntaxNode named)
        {
            AppendLine(builder, depth, "NamedTypeSyntaxNode: " + named.Name);
            return;
        }

        if (type is ListTypeSyntaxNode list)
        {
            AppendLine(builder, depth, "ListTypeSyntaxNode");
            AppendExpression(builder, list.Size, depth + 1);
            AppendType(builder, list.ElementType, depth + 1);
            return;
        }

        if (type is RecordTypeSyntaxNode record)
        {
            AppendLine(builder, depth, "RecordTypeSyntaxNode");
            foreach (FieldDeclarationNode field in record.Fields)
            {
                AppendLine(builder, depth + 1, "FieldDeclarationNode: " + field.Name);
                AppendType(builder, field.Type, depth + 2);
            }
            return;
        }

        AppendLine(builder, depth, type.GetType().Name);
    }

    /// <summary>
    /// يطبع أي نوع من أنواع العبارات في الشجرة.
    /// </summary>
    private static void AppendStatement(StringBuilder builder, StatementNode statement, int depth)
    {
        if (statement is VarDeclarationNode declaration)
        {
            AppendLine(builder, depth, "VariableDeclarationGroupNode");
            AppendLine(builder, depth + 1, "IdentifierNode: " + declaration.Name);
            AppendLine(builder, depth + 1, "NamedTypeSyntaxNode: " + declaration.TypeName);
            if (declaration.Initializer is not null)
            {
                AppendLine(builder, depth + 1, "Initializer:");
                AppendExpression(builder, declaration.Initializer, depth + 2);
            }
            return;
        }

        if (statement is AssignmentNode assignment)
        {
            AppendLine(builder, depth, "AssignmentNode: " + assignment.Name);
            AppendExpression(builder, assignment.Expression, depth + 1);
            return;
        }

        if (statement is ComplexAssignmentNode complexAssign)
        {
            AppendLine(builder, depth, "AssignmentNode");
            AppendExpression(builder, complexAssign.Target, depth + 1);
            AppendExpression(builder, complexAssign.Expression, depth + 1);
            return;
        }

        if (statement is PrintNode print)
        {
            AppendLine(builder, depth, "PrintNode");
            AppendExpression(builder, print.Expression, depth + 1);
            return;
        }

        if (statement is PrintListNode printList)
        {
            AppendLine(builder, depth, "PrintNode");
            foreach (ExpressionNode expr in printList.Expressions)
            {
                AppendExpression(builder, expr, depth + 1);
            }
            return;
        }

        if (statement is ReadNode read)
        {
            AppendLine(builder, depth, "ReadNode");
            AppendExpression(builder, read.Target, depth + 1);
            return;
        }

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

        if (statement is WhileNode whileNode)
        {
            AppendLine(builder, depth, "WhileNode:");
            AppendLine(builder, depth + 1, "Condition:");
            AppendExpression(builder, whileNode.Condition, depth + 2);
            AppendLine(builder, depth + 1, "Body:");
            AppendBlock(builder, whileNode.Body, depth + 2);
            return;
        }

        if (statement is RepeatToNode repeatTo)
        {
            AppendLine(builder, depth, "RepeatToNode: " + repeatTo.IteratorName);
            AppendLine(builder, depth + 1, "Start:");
            AppendExpression(builder, repeatTo.Start, depth + 2);
            AppendLine(builder, depth + 1, "End:");
            AppendExpression(builder, repeatTo.End, depth + 2);
            if (repeatTo.Step is not null)
            {
                AppendLine(builder, depth + 1, "Step:");
                AppendExpression(builder, repeatTo.Step, depth + 2);
            }
            AppendLine(builder, depth + 1, "Body:");
            AppendBlock(builder, repeatTo.Body, depth + 2);
            return;
        }

        if (statement is RepeatUntilNode repeatUntil)
        {
            AppendLine(builder, depth, "RepeatUntilNode:");
            AppendLine(builder, depth + 1, "Body:");
            AppendBlock(builder, repeatUntil.Body, depth + 2);
            AppendLine(builder, depth + 1, "Condition:");
            AppendExpression(builder, repeatUntil.Condition, depth + 2);
            return;
        }

        if (statement is ExpressionStatementNode exprStmt)
        {
            AppendExpression(builder, exprStmt.Expression, depth);
            return;
        }

        if (statement is BlockNode block)
        {
            AppendBlock(builder, block, depth);
            return;
        }

        AppendLine(builder, depth, statement.GetType().Name);
    }

    private static void AppendBlock(StringBuilder builder, BlockNode block, int depth)
    {
        AppendLine(builder, depth, "BlockNode:");
        foreach (StatementNode statement in block.Statements)
        {
            AppendStatement(builder, statement, depth + 1);
        }
    }

    private static void AppendExpression(StringBuilder builder, ExpressionNode expression, int depth)
    {
        if (expression is LiteralNode literal)
        {
            AppendLine(builder, depth, "LiteralNode: " + literal.Value);
            return;
        }

        if (expression is IdentifierNode identifier)
        {
            AppendLine(builder, depth, "IdentifierNode: " + identifier.Name);
            return;
        }

        if (expression is BinaryExpressionNode binary)
        {
            AppendLine(builder, depth, "BinaryExpressionNode: " + binary.Operator);
            AppendExpression(builder, binary.Left, depth + 1);
            AppendExpression(builder, binary.Right, depth + 1);
            return;
        }

        if (expression is UnaryExpressionNode unary)
        {
            AppendLine(builder, depth, "UnaryExpressionNode: " + unary.Operator);
            AppendExpression(builder, unary.Operand, depth + 1);
            return;
        }

        if (expression is GroupingNode grouping)
        {
            AppendLine(builder, depth, "GroupingNode:");
            AppendExpression(builder, grouping.Expression, depth + 1);
            return;
        }

        if (expression is CallExpressionNode call)
        {
            AppendLine(builder, depth, "CallExpressionNode: " + call.ProcedureName);
            foreach (ExpressionNode arg in call.Arguments)
            {
                AppendExpression(builder, arg, depth + 1);
            }
            return;
        }

        if (expression is IndexedAccessNode indexAccess)
        {
            AppendLine(builder, depth, "IndexedAccessNode");
            AppendExpression(builder, indexAccess.Collection, depth + 1);
            AppendExpression(builder, indexAccess.Index, depth + 1);
            return;
        }

        if (expression is FieldAccessNode fieldAccess)
        {
            AppendLine(builder, depth, "FieldAccessNode: " + fieldAccess.FieldName);
            AppendExpression(builder, fieldAccess.Record, depth + 1);
            return;
        }

        AppendLine(builder, depth, expression.GetType().Name);
    }

    private static void AppendLine(StringBuilder builder, int depth, string text)
    {
        builder.Append(' ', depth * 4);
        builder.AppendLine(text);
    }
}
