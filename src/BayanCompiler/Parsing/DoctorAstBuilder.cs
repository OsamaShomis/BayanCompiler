using BayanCompiler.Frontend;
using BayanCompiler.Lexing;
using BayanCompiler.Syntax;

namespace BayanCompiler.Parsing;

/// <summary>
/// Lowers the error-free concrete parse tree of the official basic grammar into the shared AST.
/// </summary>
internal static class DoctorAstBuilder
{
    /// <summary>
    /// Builds a complete program AST from the Start node of DoctorLl1Parser.
    /// </summary>
    public static ProgramNode Build(DoctorParseTreeNode root)
    {
        DoctorParseTreeNode programUnit = Child(root, 0);
        Token programKeyword = TokenOf(Child(programUnit, 0));
        Token name = TokenOf(Child(programUnit, 1));
        List<DeclarationNode> declarations = BuildDefinitionList(Child(programUnit, 3));
        BlockNode block = BuildBlock(Child(programUnit, 4));
        Token end = TokenOf(Child(programUnit, 5));
        return new ProgramNode(name.Lexeme, declarations, block.Statements, Combine(programKeyword.Span, end.Span));
    }

    private static List<DeclarationNode> BuildDefinitionList(DoctorParseTreeNode node)
    {
        if (node.Children.Count == 0)
        {
            return new List<DeclarationNode>();
        }

        var declarations = new List<DeclarationNode> { BuildDefinition(Child(node, 0)) };
        declarations.AddRange(BuildDefinitionList(Child(node, 1)));
        return declarations;
    }

    private static DeclarationNode BuildDefinition(DoctorParseTreeNode node)
    {
        DoctorParseTreeNode form = Child(node, 0);
        return form.Label switch
        {
            "ConstantDeclaration" => BuildConstantDeclaration(form),
            "TypeDeclaration" => BuildTypeDeclaration(form),
            "VariableDeclaration" => BuildVariableDeclaration(form),
            "ProcedureDeclaration" => BuildProcedureDeclaration(form),
            _ => throw new InvalidOperationException("Unsupported official declaration node: " + form.Label)
        };
    }

    private static DeclarationNode BuildConstantDeclaration(DoctorParseTreeNode node)
    {
        Token constant = TokenOf(Child(node, 0));
        Token name = TokenOf(Child(node, 1));
        ExpressionNode value = BuildConstantValue(Child(node, 3));
        Token semicolon = TokenOf(Child(node, 4));
        return new ConstantDeclarationNode(name.Lexeme, null, value, Combine(constant.Span, semicolon.Span));
    }

    private static ExpressionNode BuildConstantValue(DoctorParseTreeNode node)
    {
        Token value = TokenOf(Child(node, 0));
        return value.Type == TokenType.Identifier
            ? new IdentifierNode(value.Lexeme, value.Span)
            : new LiteralNode(value.Type, value.Lexeme, value.Span);
    }

    private static DeclarationNode BuildTypeDeclaration(DoctorParseTreeNode node)
    {
        Token typeKeyword = TokenOf(Child(node, 0));
        Token name = TokenOf(Child(node, 1));
        TypeSyntaxNode definition = BuildCompositeType(Child(node, 3));
        Token semicolon = TokenOf(Child(node, 4));
        return new TypeDeclarationNode(name.Lexeme, definition, Combine(typeKeyword.Span, semicolon.Span));
    }

    private static TypeSyntaxNode BuildCompositeType(DoctorParseTreeNode node)
    {
        DoctorParseTreeNode form = Child(node, 0);
        return form.Label switch
        {
            "ListType" => BuildListType(form),
            "RecordType" => BuildRecordType(form),
            _ => throw new InvalidOperationException("Unsupported official composite type node: " + form.Label)
        };
    }

    private static TypeSyntaxNode BuildListType(DoctorParseTreeNode node)
    {
        Token list = TokenOf(Child(node, 0));
        Token size = TokenOf(Child(node, 2));
        TypeSyntaxNode elementType = BuildDataType(Child(node, 5));
        return new ListTypeSyntaxNode(new LiteralNode(size.Type, size.Lexeme, size.Span), elementType, Combine(list.Span, elementType.Span));
    }

    private static TypeSyntaxNode BuildRecordType(DoctorParseTreeNode node)
    {
        Token record = TokenOf(Child(node, 0));
        List<FieldDeclarationNode> fields = BuildFieldList(Child(node, 2));
        Token rightBrace = TokenOf(Child(node, 3));
        return new RecordTypeSyntaxNode(fields, Combine(record.Span, rightBrace.Span));
    }

    private static List<FieldDeclarationNode> BuildFieldList(DoctorParseTreeNode node)
    {
        var fields = new List<FieldDeclarationNode> { BuildFieldDeclaration(Child(node, 0)) };
        DoctorParseTreeNode tail = Child(node, 1);
        if (tail.Children.Count > 0)
        {
            fields.AddRange(BuildFieldList(Child(tail, 1)));
        }

        return fields;
    }

    private static FieldDeclarationNode BuildFieldDeclaration(DoctorParseTreeNode node)
    {
        Token name = TokenOf(Child(node, 0));
        TypeSyntaxNode type = BuildDataType(Child(node, 2));
        return new FieldDeclarationNode(name.Lexeme, type, Combine(name.Span, type.Span));
    }

    private static DeclarationNode BuildVariableDeclaration(DoctorParseTreeNode node)
    {
        Token variable = TokenOf(Child(node, 0));
        List<string> names = BuildNameList(Child(node, 1));
        TypeSyntaxNode type = BuildDataType(Child(node, 3));
        Token semicolon = TokenOf(Child(node, 4));
        return new VariableDeclarationGroupNode(names, type, null, Combine(variable.Span, semicolon.Span));
    }

    private static DeclarationNode BuildProcedureDeclaration(DoctorParseTreeNode node)
    {
        Token procedure = TokenOf(Child(node, 0));
        Token name = TokenOf(Child(node, 1));
        List<ParameterNode> parameters = BuildFormalParameters(Child(node, 3));
        List<DeclarationNode> declarations = BuildDefinitionList(Child(node, 6));
        BlockNode parsedBody = BuildBlock(Child(node, 7));
        BlockNode body = new BlockNode(declarations, parsedBody.Statements, parsedBody.Span);
        Token semicolon = TokenOf(Child(node, 8));
        return new ProcedureDeclarationNode(name.Lexeme, parameters, body, Combine(procedure.Span, semicolon.Span));
    }

    private static List<ParameterNode> BuildFormalParameters(DoctorParseTreeNode node)
    {
        if (node.Children.Count == 0)
        {
            return new List<ParameterNode>();
        }

        var parameters = BuildFormalParameter(Child(node, 0));
        DoctorParseTreeNode tail = Child(node, 1);
        while (tail.Children.Count > 0)
        {
            parameters.AddRange(BuildFormalParameter(Child(tail, 1)));
            tail = Child(tail, 2);
        }

        return parameters;
    }

    private static List<ParameterNode> BuildFormalParameter(DoctorParseTreeNode node)
    {
        Token passing = TokenOf(Child(Child(node, 0), 0));
        ParameterPassingMode mode = passing.Type == TokenType.ByReference ? ParameterPassingMode.ByReference : ParameterPassingMode.ByValue;
        List<string> names = BuildNameList(Child(node, 1));
        TypeSyntaxNode type = BuildDataType(Child(node, 3));
        return names.Select(name => new ParameterNode(name, type, mode, Combine(passing.Span, type.Span))).ToList();
    }

    private static List<string> BuildNameList(DoctorParseTreeNode node)
    {
        var names = new List<string> { TokenOf(Child(node, 0)).Lexeme };
        DoctorParseTreeNode tail = Child(node, 1);
        while (tail.Children.Count > 0)
        {
            names.Add(TokenOf(Child(tail, 1)).Lexeme);
            tail = Child(tail, 2);
        }

        return names;
    }

    private static TypeSyntaxNode BuildDataType(DoctorParseTreeNode node)
    {
        Token first = TokenOf(Child(node, 0));
        if (first.Type == TokenType.TypeString)
        {
            Token qualifier = TokenOf(Child(node, 1));
            return new NamedTypeSyntaxNode(first.Lexeme + " " + qualifier.Lexeme, first.Type, Combine(first.Span, qualifier.Span));
        }

        return new NamedTypeSyntaxNode(first.Lexeme, first.Type, first.Span);
    }

    private static BlockNode BuildBlock(DoctorParseTreeNode node)
    {
        Token leftBrace = TokenOf(Child(node, 0));
        List<StatementNode> statements = BuildStatementList(Child(node, 1));
        Token rightBrace = TokenOf(Child(node, 2));
        return new BlockNode(statements, Combine(leftBrace.Span, rightBrace.Span));
    }

    private static List<StatementNode> BuildStatementList(DoctorParseTreeNode node)
    {
        if (node.Children.Count == 0)
        {
            return new List<StatementNode>();
        }

        var statements = new List<StatementNode> { BuildStatement(Child(node, 0)) };
        statements.AddRange(BuildStatementList(Child(node, 1)));
        return statements;
    }

    private static StatementNode BuildStatement(DoctorParseTreeNode node)
    {
        DoctorParseTreeNode form = Child(node, 0);
        return form.Label switch
        {
            "PrintStatement" => BuildPrint(form),
            "IdentifierStatement" => BuildIdentifierStatement(form),
            "ReadStatement" => BuildRead(form),
            "IfStatement" => BuildIf(form),
            "RepeatToStatement" => BuildRepeatTo(form),
            "WhileStatement" => BuildWhile(form),
            "RepeatUntilStatement" => BuildRepeatUntil(form),
            "Block" => BuildBlock(form),
            _ => throw new InvalidOperationException("Unsupported official statement node: " + form.Label)
        };
    }

    private static StatementNode BuildRead(DoctorParseTreeNode node)
    {
        Token read = TokenOf(Child(node, 0));
        ExpressionNode target = BuildAccess(Child(node, 2));
        Token semicolon = TokenOf(Child(node, 4));
        return new ReadNode(target, Combine(read.Span, semicolon.Span));
    }

    private static StatementNode BuildIf(DoctorParseTreeNode node)
    {
        Token ifKeyword = TokenOf(Child(node, 0));
        ExpressionNode condition = BuildExpression(Child(node, 2));
        StatementNode thenStatement = BuildStatement(Child(node, 5));
        DoctorParseTreeNode elsePart = Child(node, 6);
        BlockNode? elseBlock = elsePart.Children.Count == 0 ? null : ToBlock(BuildStatement(Child(elsePart, 1)));
        SourceSpan end = elseBlock?.Span ?? thenStatement.Span;
        return new IfNode(condition, ToBlock(thenStatement), elseBlock, Combine(ifKeyword.Span, end));
    }

    private static StatementNode BuildRepeatTo(DoctorParseTreeNode node)
    {
        Token repeat = TokenOf(Child(node, 0));
        Token iterator = TokenOf(Child(node, 2));
        ExpressionNode start = BuildExpression(Child(node, 4));
        ExpressionNode end = BuildExpression(Child(node, 6));
        DoctorParseTreeNode stepOpt = Child(node, 7);
        ExpressionNode? step = stepOpt.Children.Count == 0 ? null : BuildExpression(Child(stepOpt, 1));
        StatementNode body = BuildStatement(Child(node, 9));
        return new RepeatToNode(iterator.Lexeme, start, end, step, ToBlock(body), Combine(repeat.Span, body.Span));
    }

    private static StatementNode BuildWhile(DoctorParseTreeNode node)
    {
        Token whileKeyword = TokenOf(Child(node, 0));
        ExpressionNode condition = BuildExpression(Child(node, 2));
        StatementNode body = BuildStatement(Child(node, 5));
        return new WhileNode(condition, ToBlock(body), Combine(whileKeyword.Span, body.Span));
    }

    private static StatementNode BuildRepeatUntil(DoctorParseTreeNode node)
    {
        Token repeat = TokenOf(Child(node, 0));
        StatementNode body = BuildStatement(Child(node, 1));
        ExpressionNode condition = BuildExpression(Child(node, 4));
        Token rightParen = TokenOf(Child(node, 5));
        return new RepeatUntilNode(ToBlock(body), condition, Combine(repeat.Span, rightParen.Span));
    }

    private static BlockNode ToBlock(StatementNode statement)
    {
        return statement as BlockNode ?? new BlockNode(new List<StatementNode> { statement }, statement.Span);
    }

    private static StatementNode BuildPrint(DoctorParseTreeNode node)
    {
        Token print = TokenOf(Child(node, 0));
        List<ExpressionNode> expressions = BuildPrintItems(Child(node, 2));
        Token semicolon = TokenOf(Child(node, 4));

        return expressions.Count == 1
            ? new PrintNode(expressions[0], Combine(print.Span, semicolon.Span))
            : new PrintListNode(expressions, Combine(print.Span, semicolon.Span));
    }

    private static List<ExpressionNode> BuildPrintItems(DoctorParseTreeNode node)
    {
        var expressions = new List<ExpressionNode> { BuildExpression(Child(node, 0)) };
        DoctorParseTreeNode tail = Child(node, 1);
        while (tail.Children.Count > 0)
        {
            expressions.Add(BuildExpression(Child(tail, 1)));
            tail = Child(tail, 2);
        }

        return expressions;
    }

    private static StatementNode BuildIdentifierStatement(DoctorParseTreeNode node)
    {
        Token name = TokenOf(Child(node, 0));
        DoctorParseTreeNode tail = Child(node, 1);
        if (Child(tail, 0).Label == TokenType.LeftParen.ToString())
        {
            List<ExpressionNode> arguments = BuildActualArgumentsOpt(Child(tail, 1));
            Token semicolon = TokenOf(Child(tail, 3));
            var call = new CallExpressionNode(name.Lexeme, arguments, Combine(name.Span, semicolon.Span));
            return new ExpressionStatementNode(call, call.Span);
        }

        ExpressionNode target = BuildAccessFromHead(name, Child(tail, 0));
        ExpressionNode expression = BuildExpression(Child(tail, 2));
        Token assignmentSemicolon = TokenOf(Child(tail, 3));

        return target is IdentifierNode identifier
            ? new AssignmentNode(identifier.Name, expression, Combine(identifier.Span, assignmentSemicolon.Span))
            : new ComplexAssignmentNode(target, expression, Combine(target.Span, assignmentSemicolon.Span));
    }

    private static List<ExpressionNode> BuildActualArguments(DoctorParseTreeNode node)
    {
        if (node.Children.Count == 0)
        {
            return new List<ExpressionNode>();
        }

        var arguments = new List<ExpressionNode> { BuildExpression(Child(node, 0)) };
        DoctorParseTreeNode tail = Child(node, 1);
        while (tail.Children.Count > 0)
        {
            arguments.Add(BuildExpression(Child(tail, 1)));
            tail = Child(tail, 2);
        }

        return arguments;
    }

    private static List<ExpressionNode> BuildActualArgumentsOpt(DoctorParseTreeNode node)
    {
        return node.Children.Count == 0 ? new List<ExpressionNode>() : BuildActualArguments(Child(node, 0));
    }

    private static ExpressionNode BuildAccess(DoctorParseTreeNode node)
    {
        Token identifier = TokenOf(Child(node, 0));
        return BuildAccessFromHead(identifier, Child(node, 1));
    }

    private static ExpressionNode BuildAccessFromHead(Token identifier, DoctorParseTreeNode tail)
    {
        ExpressionNode access = new IdentifierNode(identifier.Lexeme, identifier.Span);

        while (tail.Children.Count > 0)
        {
            if (Child(tail, 0).Label == TokenType.LeftBracket.ToString())
            {
                ExpressionNode index = BuildExpression(Child(tail, 1));
                Token rightBracket = TokenOf(Child(tail, 2));
                access = new IndexedAccessNode(access, index, Combine(access.Span, rightBracket.Span));
                tail = Child(tail, 3);
                continue;
            }

            Token field = TokenOf(Child(tail, 1));
            access = new FieldAccessNode(access, field.Lexeme, Combine(access.Span, field.Span));
            tail = Child(tail, 2);
        }

        return access;
    }

    private static ExpressionNode BuildExpression(DoctorParseTreeNode node)
    {
        ExpressionNode expression = BuildSimpleExpression(Child(node, 0));
        DoctorParseTreeNode relationTail = Child(node, 1);
        if (relationTail.Children.Count == 0)
        {
            return expression;
        }

        Token operation = TokenOf(Child(Child(relationTail, 0), 0));
        ExpressionNode right = BuildSimpleExpression(Child(relationTail, 1));
        return new BinaryExpressionNode(expression, operation.Type, right, Combine(expression.Span, right.Span));
    }

    private static ExpressionNode BuildSimpleExpression(DoctorParseTreeNode node)
    {
        DoctorParseTreeNode sign = Child(node, 0);
        ExpressionNode expression = BuildTerm(Child(node, 1));
        if (sign.Children.Count > 0 && TokenOf(Child(sign, 0)).Type == TokenType.Minus)
        {
            Token minus = TokenOf(Child(sign, 0));
            expression = new UnaryExpressionNode(minus.Type, expression, Combine(minus.Span, expression.Span));
        }

        DoctorParseTreeNode tail = Child(node, 2);
        while (tail.Children.Count > 0)
        {
            Token operation = TokenOf(Child(Child(tail, 0), 0));
            ExpressionNode right = BuildTerm(Child(tail, 1));
            expression = new BinaryExpressionNode(expression, operation.Type, right, Combine(expression.Span, right.Span));
            tail = Child(tail, 2);
        }

        return expression;
    }

    private static ExpressionNode BuildTerm(DoctorParseTreeNode node)
    {
        ExpressionNode expression = BuildFactor(Child(node, 0));
        DoctorParseTreeNode tail = Child(node, 1);
        while (tail.Children.Count > 0)
        {
            Token operation = TokenOf(Child(Child(tail, 0), 0));
            ExpressionNode right = BuildFactor(Child(tail, 1));
            expression = new BinaryExpressionNode(expression, operation.Type, right, Combine(expression.Span, right.Span));
            tail = Child(tail, 2);
        }

        return expression;
    }

    private static ExpressionNode BuildFactor(DoctorParseTreeNode node)
    {
        DoctorParseTreeNode first = Child(node, 0);
        if (first.Label == "Access")
        {
            return BuildAccess(first);
        }

        if (first.Label == TokenType.LeftParen.ToString())
        {
            Token leftParen = TokenOf(first);
            ExpressionNode expression = BuildExpression(Child(node, 1));
            Token rightParen = TokenOf(Child(node, 2));
            return new GroupingNode(expression, Combine(leftParen.Span, rightParen.Span));
        }

        if (first.Label == TokenType.Bang.ToString())
        {
            Token bang = TokenOf(first);
            ExpressionNode operand = BuildFactor(Child(node, 1));
            return new UnaryExpressionNode(bang.Type, operand, Combine(bang.Span, operand.Span));
        }

        Token literal = TokenOf(first);
        return new LiteralNode(literal.Type, literal.Lexeme, literal.Span);
    }

    private static DoctorParseTreeNode Child(DoctorParseTreeNode node, int index)
    {
        return node.Children[index];
    }

    private static Token TokenOf(DoctorParseTreeNode node)
    {
        return node.MatchedToken ?? throw new InvalidOperationException("A terminal node was not matched during AST lowering.");
    }

    private static SourceSpan Combine(SourceSpan first, SourceSpan last)
    {
        int end = last.Start + last.Length;
        return new SourceSpan(first.Start, Math.Max(0, end - first.Start), first.Line, first.Column);
    }
}
