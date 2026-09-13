using System.Text;
using BayanCompiler.Frontend;
using BayanCompiler.Lexing;

namespace BayanCompiler.Parsing;

/// <summary>
/// Provides the first official, table-driven LL(1) parser for the doctor specification.
/// </summary>
public sealed class DoctorLl1Parser
{
    private const int MaxTraceSteps = 800;
    private static readonly Dictionary<(DoctorNonTerminal NonTerminal, TokenType Lookahead), DoctorGrammarSymbol[]> ParseTable = BuildParseTable();

    private readonly IReadOnlyList<Token> _tokens;
    private readonly DiagnosticBag _diagnostics = new();
    private readonly List<Ll1TraceStep> _traceSteps = new();
    private int _position;

    /// <summary>
    /// Exposes the table decisions made during the latest parsing run.
    /// </summary>
    public IReadOnlyList<Ll1TraceStep> TraceSteps => _traceSteps.AsReadOnly();

    /// <summary>
    /// Initializes an official LL(1) parser with lexer output ending in Eof.
    /// </summary>
    public DoctorLl1Parser(IReadOnlyList<Token> tokens)
    {
        _tokens = tokens ?? throw new ArgumentNullException(nameof(tokens));
        if (_tokens.Count == 0)
        {
            throw new ArgumentException("The token stream must contain an Eof token.", nameof(tokens));
        }
    }

    /// <summary>
    /// Runs the predictive parser and builds a concrete parse tree from actual stack expansions.
    /// </summary>
    public DoctorParseResult Parse()
    {
        var root = DoctorParseTreeNode.ForNonTerminal(DoctorNonTerminal.Start);
        var stack = new Stack<DoctorParseStackEntry>();
        stack.Push(new DoctorParseStackEntry(DoctorGrammarSymbol.ForNonTerminal(DoctorNonTerminal.Start), root));

        while (stack.Count > 0)
        {
            DoctorParseStackEntry entry = stack.Pop();
            string stackSnapshot = FormatStack(entry, stack);

            if (entry.Symbol.IsTerminal)
            {
                AddTrace(stackSnapshot, Current, "Match terminal " + entry.Symbol.Terminal);
                MatchTerminal(entry.Symbol.Terminal, entry.Node);
                continue;
            }

            if (!ParseTable.TryGetValue((entry.Symbol.NonTerminal, Current.Type), out DoctorGrammarSymbol[]? production))
            {
                string expected = GetExpectedLookaheads(entry.Symbol.NonTerminal);
                AddTrace(stackSnapshot, Current, "No table cell for " + entry.Symbol.NonTerminal);
                Report("SYN001", "أثناء تحليل " + entry.Symbol.NonTerminal + ": متوقع أحد الرموز { " + expected + " } لكن وُجد " + Describe(Current) + ".", Current.Span);

                if (!IsAtEnd)
                {
                    Advance();
                }

                continue;
            }

            AddTrace(stackSnapshot, Current, "M[" + entry.Symbol.NonTerminal + ", " + Current.Type + "] = " + FormatProduction(production));
            ExpandProduction(entry.Node, production, stack);
        }

        var result = new DoctorParseResult(root, _diagnostics.Items, _traceSteps);
        return result.Succeeded ? result with { Program = DoctorAstBuilder.Build(root) } : result;
    }

    /// <summary>
    /// Formats the non-empty LL(1) analysis table for the editor and compiler artifacts.
    /// </summary>
    public static string FormatAnalysisTable()
    {
        var builder = new StringBuilder();
        builder.AppendLine("جدول التحليل Table-Driven LL(1) — قواعد الدكتور خالد الكحسة");
        builder.AppendLine("الصيغة: M[NonTerminal, Lookahead] = Production");
        builder.AppendLine(new string('=', 92));

        foreach (var cell in ParseTable.OrderBy(item => item.Key.NonTerminal).ThenBy(item => item.Key.Lookahead))
        {
            builder.AppendLine("M[" + cell.Key.NonTerminal + ", " + cell.Key.Lookahead + "] = " + FormatProduction(cell.Value));
        }

        return builder.ToString();
    }

    /// <summary>
    /// Formats the parser trace using the same stack, lookahead, and action columns shown by the editor.
    /// </summary>
    public string FormatTrace()
    {
        var builder = new StringBuilder();
        builder.AppendLine("تتبع LL(1) الرسمي: Stack → Lookahead → Action");
        builder.AppendLine(new string('=', 100));

        if (_traceSteps.Count == 0)
        {
            builder.AppendLine("لا توجد خطوات بعد.");
            return builder.ToString();
        }

        foreach (Ll1TraceStep step in _traceSteps)
        {
            builder.AppendLine(step.StepNumber.ToString().PadLeft(3) + " | " + step.Stack + " | " + step.Lookahead + " | " + step.Action);
        }

        return builder.ToString();
    }

    /// <summary>
    /// Formats the concrete parse tree produced by the current parse operation.
    /// </summary>
    public static string FormatParseTree(DoctorParseTreeNode root)
    {
        var builder = new StringBuilder();
        AppendTree(root, builder, 0);
        return builder.ToString();
    }

    private static void AppendTree(DoctorParseTreeNode node, StringBuilder builder, int depth)
    {
        builder.Append(' ', depth * 2);
        builder.Append(node.Label);
        if (node.MatchedToken is not null)
        {
            builder.Append(" → ");
            builder.Append(node.MatchedToken.Lexeme);
        }

        builder.AppendLine();
        foreach (DoctorParseTreeNode child in node.Children)
        {
            AppendTree(child, builder, depth + 1);
        }
    }

    private void MatchTerminal(TokenType expected, DoctorParseTreeNode node)
    {
        if (Current.Type == expected)
        {
            node.MatchedToken = Current;
            if (!IsAtEnd)
            {
                Advance();
            }

            return;
        }

        Report("SYN001", "متوقع الرمز " + expected + " لكن وُجد " + Describe(Current) + ".", Current.Span);
        if (expected == TokenType.Semicolon && Current.Type == TokenType.RightBrace)
        {
            // Insert a missing statement terminator before a block boundary to prevent cascading diagnostics.
            node.MatchedToken = new Token(TokenType.Semicolon, "؛", Current.Span);
            return;
        }

        if (!IsAtEnd)
        {
            Advance();
        }
    }

    private static void ExpandProduction(DoctorParseTreeNode parent, IReadOnlyList<DoctorGrammarSymbol> production, Stack<DoctorParseStackEntry> stack)
    {
        var children = new List<DoctorParseTreeNode>();
        foreach (DoctorGrammarSymbol symbol in production)
        {
            DoctorParseTreeNode child = symbol.IsTerminal
                ? DoctorParseTreeNode.ForTerminal(symbol.Terminal)
                : DoctorParseTreeNode.ForNonTerminal(symbol.NonTerminal);
            parent.Children.Add(child);
            children.Add(child);
        }

        for (int index = production.Count - 1; index >= 0; index--)
        {
            stack.Push(new DoctorParseStackEntry(production[index], children[index]));
        }
    }

    private static Dictionary<(DoctorNonTerminal NonTerminal, TokenType Lookahead), DoctorGrammarSymbol[]> BuildParseTable()
    {
        var table = new Dictionary<(DoctorNonTerminal NonTerminal, TokenType Lookahead), DoctorGrammarSymbol[]>();

        Add(table, DoctorNonTerminal.Start, new[] { TokenType.Program }, N(DoctorNonTerminal.ProgramUnit), T(TokenType.Eof));
        Add(table, DoctorNonTerminal.ProgramUnit, new[] { TokenType.Program }, T(TokenType.Program), T(TokenType.Identifier), T(TokenType.Semicolon), N(DoctorNonTerminal.DefinitionList), N(DoctorNonTerminal.Block), T(TokenType.Dot));
        Add(table, DoctorNonTerminal.Block, new[] { TokenType.LeftBrace }, T(TokenType.LeftBrace), N(DoctorNonTerminal.StatementList), T(TokenType.RightBrace));

        Add(table, DoctorNonTerminal.DefinitionList, DefinitionStarts(), N(DoctorNonTerminal.Definition), N(DoctorNonTerminal.DefinitionList));
        Add(table, DoctorNonTerminal.DefinitionList, new[] { TokenType.LeftBrace }, Epsilon());
        Add(table, DoctorNonTerminal.Definition, new[] { TokenType.Const }, N(DoctorNonTerminal.ConstantDeclaration));
        Add(table, DoctorNonTerminal.Definition, new[] { TokenType.TypeDeclaration }, N(DoctorNonTerminal.TypeDeclaration));
        Add(table, DoctorNonTerminal.Definition, new[] { TokenType.Variable }, N(DoctorNonTerminal.VariableDeclaration));
        Add(table, DoctorNonTerminal.Definition, new[] { TokenType.Procedure }, N(DoctorNonTerminal.ProcedureDeclaration));
        Add(table, DoctorNonTerminal.ConstantDeclaration, new[] { TokenType.Const }, T(TokenType.Const), T(TokenType.Identifier), T(TokenType.Assign), N(DoctorNonTerminal.ConstantValue), T(TokenType.Semicolon));
        Add(table, DoctorNonTerminal.ConstantValue, new[] { TokenType.Integer }, T(TokenType.Integer));
        Add(table, DoctorNonTerminal.ConstantValue, new[] { TokenType.Real }, T(TokenType.Real));
        Add(table, DoctorNonTerminal.ConstantValue, new[] { TokenType.String }, T(TokenType.String));
        Add(table, DoctorNonTerminal.ConstantValue, new[] { TokenType.Character }, T(TokenType.Character));
        Add(table, DoctorNonTerminal.ConstantValue, new[] { TokenType.True }, T(TokenType.True));
        Add(table, DoctorNonTerminal.ConstantValue, new[] { TokenType.False }, T(TokenType.False));
        Add(table, DoctorNonTerminal.ConstantValue, new[] { TokenType.Identifier }, T(TokenType.Identifier));
        Add(table, DoctorNonTerminal.VariableDeclaration, new[] { TokenType.Variable }, T(TokenType.Variable), N(DoctorNonTerminal.NameList), T(TokenType.Colon), N(DoctorNonTerminal.DataType), T(TokenType.Semicolon));
        Add(table, DoctorNonTerminal.NameList, new[] { TokenType.Identifier }, T(TokenType.Identifier), N(DoctorNonTerminal.NameListTail));
        Add(table, DoctorNonTerminal.NameListTail, new[] { TokenType.Comma }, T(TokenType.Comma), T(TokenType.Identifier), N(DoctorNonTerminal.NameListTail));
        Add(table, DoctorNonTerminal.NameListTail, new[] { TokenType.Colon }, Epsilon());
        Add(table, DoctorNonTerminal.DataType, new[] { TokenType.TypeInt }, T(TokenType.TypeInt));
        Add(table, DoctorNonTerminal.DataType, new[] { TokenType.TypeReal }, T(TokenType.TypeReal));
        Add(table, DoctorNonTerminal.DataType, new[] { TokenType.TypeBool }, T(TokenType.TypeBool));
        Add(table, DoctorNonTerminal.DataType, new[] { TokenType.TypeChar }, T(TokenType.TypeChar));
        Add(table, DoctorNonTerminal.DataType, new[] { TokenType.TypeString }, T(TokenType.TypeString), T(TokenType.TypeStringQualifier));
        Add(table, DoctorNonTerminal.DataType, new[] { TokenType.Identifier }, T(TokenType.Identifier));
        Add(table, DoctorNonTerminal.TypeDeclaration, new[] { TokenType.TypeDeclaration }, T(TokenType.TypeDeclaration), T(TokenType.Identifier), T(TokenType.Assign), N(DoctorNonTerminal.CompositeType), T(TokenType.Semicolon));
        Add(table, DoctorNonTerminal.CompositeType, new[] { TokenType.List }, N(DoctorNonTerminal.ListType));
        Add(table, DoctorNonTerminal.CompositeType, new[] { TokenType.Record }, N(DoctorNonTerminal.RecordType));
        Add(table, DoctorNonTerminal.ListType, new[] { TokenType.List }, T(TokenType.List), T(TokenType.LeftBracket), T(TokenType.Integer), T(TokenType.RightBracket), T(TokenType.From), N(DoctorNonTerminal.DataType));
        Add(table, DoctorNonTerminal.RecordType, new[] { TokenType.Record }, T(TokenType.Record), T(TokenType.LeftBrace), N(DoctorNonTerminal.FieldList), T(TokenType.RightBrace));
        Add(table, DoctorNonTerminal.FieldList, new[] { TokenType.Identifier }, N(DoctorNonTerminal.FieldDeclaration), N(DoctorNonTerminal.FieldListTail));
        Add(table, DoctorNonTerminal.FieldListTail, new[] { TokenType.Semicolon }, T(TokenType.Semicolon), N(DoctorNonTerminal.FieldList));
        Add(table, DoctorNonTerminal.FieldListTail, new[] { TokenType.RightBrace }, Epsilon());
        Add(table, DoctorNonTerminal.FieldDeclaration, new[] { TokenType.Identifier }, T(TokenType.Identifier), T(TokenType.Colon), N(DoctorNonTerminal.DataType));
        Add(table, DoctorNonTerminal.ProcedureDeclaration, new[] { TokenType.Procedure }, T(TokenType.Procedure), T(TokenType.Identifier), T(TokenType.LeftParen), N(DoctorNonTerminal.FormalParametersOpt), T(TokenType.RightParen), T(TokenType.Semicolon), N(DoctorNonTerminal.DefinitionList), N(DoctorNonTerminal.Block), T(TokenType.Semicolon));
        Add(table, DoctorNonTerminal.FormalParametersOpt, new[] { TokenType.ByValue, TokenType.ByReference }, N(DoctorNonTerminal.FormalParameter), N(DoctorNonTerminal.FormalParametersTail));
        Add(table, DoctorNonTerminal.FormalParametersOpt, new[] { TokenType.RightParen }, Epsilon());
        Add(table, DoctorNonTerminal.FormalParametersTail, new[] { TokenType.Semicolon }, T(TokenType.Semicolon), N(DoctorNonTerminal.FormalParameter), N(DoctorNonTerminal.FormalParametersTail));
        Add(table, DoctorNonTerminal.FormalParametersTail, new[] { TokenType.RightParen }, Epsilon());
        Add(table, DoctorNonTerminal.FormalParameter, new[] { TokenType.ByValue, TokenType.ByReference }, N(DoctorNonTerminal.PassingMode), N(DoctorNonTerminal.NameList), T(TokenType.Colon), N(DoctorNonTerminal.DataType));
        AddTerminalLookaheads(table, DoctorNonTerminal.PassingMode, new[] { TokenType.ByValue, TokenType.ByReference });

        Add(table, DoctorNonTerminal.StatementList, StatementStarts(), N(DoctorNonTerminal.Statement), N(DoctorNonTerminal.StatementList));
        Add(table, DoctorNonTerminal.StatementList, new[] { TokenType.RightBrace }, Epsilon());
        Add(table, DoctorNonTerminal.Statement, new[] { TokenType.Print }, N(DoctorNonTerminal.PrintStatement));
        Add(table, DoctorNonTerminal.Statement, new[] { TokenType.Identifier }, N(DoctorNonTerminal.IdentifierStatement));
        Add(table, DoctorNonTerminal.Statement, new[] { TokenType.Read }, N(DoctorNonTerminal.ReadStatement));
        Add(table, DoctorNonTerminal.Statement, new[] { TokenType.If }, N(DoctorNonTerminal.IfStatement));
        Add(table, DoctorNonTerminal.Statement, new[] { TokenType.Repeat }, N(DoctorNonTerminal.RepeatToStatement));
        Add(table, DoctorNonTerminal.Statement, new[] { TokenType.While }, N(DoctorNonTerminal.WhileStatement));
        Add(table, DoctorNonTerminal.Statement, new[] { TokenType.RepeatUntil }, N(DoctorNonTerminal.RepeatUntilStatement));
        Add(table, DoctorNonTerminal.Statement, new[] { TokenType.LeftBrace }, N(DoctorNonTerminal.Block));

        Add(table, DoctorNonTerminal.PrintStatement, new[] { TokenType.Print }, T(TokenType.Print), T(TokenType.LeftParen), N(DoctorNonTerminal.PrintItems), T(TokenType.RightParen), T(TokenType.Semicolon));
        Add(table, DoctorNonTerminal.PrintItems, ExpressionStarts(), N(DoctorNonTerminal.Expression), N(DoctorNonTerminal.PrintItemsTail));
        Add(table, DoctorNonTerminal.PrintItemsTail, new[] { TokenType.Comma }, T(TokenType.Comma), N(DoctorNonTerminal.Expression), N(DoctorNonTerminal.PrintItemsTail));
        Add(table, DoctorNonTerminal.PrintItemsTail, new[] { TokenType.RightParen }, Epsilon());

        Add(table, DoctorNonTerminal.ReadStatement, new[] { TokenType.Read }, T(TokenType.Read), T(TokenType.LeftParen), N(DoctorNonTerminal.Access), T(TokenType.RightParen), T(TokenType.Semicolon));
        Add(table, DoctorNonTerminal.IfStatement, new[] { TokenType.If }, T(TokenType.If), T(TokenType.LeftParen), N(DoctorNonTerminal.Expression), T(TokenType.RightParen), T(TokenType.Then), N(DoctorNonTerminal.Statement), N(DoctorNonTerminal.ElsePart));
        Add(table, DoctorNonTerminal.ElsePart, new[] { TokenType.Else }, T(TokenType.Else), N(DoctorNonTerminal.Statement));
        Add(table, DoctorNonTerminal.ElsePart, StatementStarts().Concat(new[] { TokenType.RightBrace }), Epsilon());
        Add(table, DoctorNonTerminal.RepeatToStatement, new[] { TokenType.Repeat }, T(TokenType.Repeat), T(TokenType.LeftParen), T(TokenType.Identifier), T(TokenType.Assign), N(DoctorNonTerminal.Expression), T(TokenType.To), N(DoctorNonTerminal.Expression), N(DoctorNonTerminal.StepOpt), T(TokenType.RightParen), N(DoctorNonTerminal.Statement));
        Add(table, DoctorNonTerminal.StepOpt, new[] { TokenType.Step }, T(TokenType.Step), N(DoctorNonTerminal.Expression));
        Add(table, DoctorNonTerminal.StepOpt, new[] { TokenType.RightParen }, Epsilon());
        Add(table, DoctorNonTerminal.WhileStatement, new[] { TokenType.While }, T(TokenType.While), T(TokenType.LeftParen), N(DoctorNonTerminal.Expression), T(TokenType.RightParen), T(TokenType.Continue), N(DoctorNonTerminal.Statement));
        Add(table, DoctorNonTerminal.RepeatUntilStatement, new[] { TokenType.RepeatUntil }, T(TokenType.RepeatUntil), N(DoctorNonTerminal.Statement), T(TokenType.Until), T(TokenType.LeftParen), N(DoctorNonTerminal.Expression), T(TokenType.RightParen));

        Add(table, DoctorNonTerminal.IdentifierStatement, new[] { TokenType.Identifier }, T(TokenType.Identifier), N(DoctorNonTerminal.IdentifierStatementTail));
        Add(table, DoctorNonTerminal.IdentifierStatementTail, new[] { TokenType.LeftParen }, T(TokenType.LeftParen), N(DoctorNonTerminal.ActualArgumentsOpt), T(TokenType.RightParen), T(TokenType.Semicolon));
        Add(table, DoctorNonTerminal.IdentifierStatementTail, new[] { TokenType.LeftBracket, TokenType.Dot, TokenType.Assign }, N(DoctorNonTerminal.AccessTail), T(TokenType.Assign), N(DoctorNonTerminal.Expression), T(TokenType.Semicolon));
        Add(table, DoctorNonTerminal.ActualArgumentsOpt, ExpressionStarts(), N(DoctorNonTerminal.ActualArguments));
        Add(table, DoctorNonTerminal.ActualArgumentsOpt, new[] { TokenType.RightParen }, Epsilon());
        Add(table, DoctorNonTerminal.ActualArguments, ExpressionStarts(), N(DoctorNonTerminal.Expression), N(DoctorNonTerminal.ActualArgumentsTail));
        Add(table, DoctorNonTerminal.ActualArgumentsTail, new[] { TokenType.Comma }, T(TokenType.Comma), N(DoctorNonTerminal.Expression), N(DoctorNonTerminal.ActualArgumentsTail));
        Add(table, DoctorNonTerminal.ActualArgumentsTail, new[] { TokenType.RightParen }, Epsilon());
        Add(table, DoctorNonTerminal.Access, new[] { TokenType.Identifier }, T(TokenType.Identifier), N(DoctorNonTerminal.AccessTail));
        Add(table, DoctorNonTerminal.AccessTail, new[] { TokenType.LeftBracket }, T(TokenType.LeftBracket), N(DoctorNonTerminal.Expression), T(TokenType.RightBracket), N(DoctorNonTerminal.AccessTail));
        Add(table, DoctorNonTerminal.AccessTail, new[] { TokenType.Dot }, T(TokenType.Dot), T(TokenType.Identifier), N(DoctorNonTerminal.AccessTail));
        Add(table, DoctorNonTerminal.AccessTail, AccessFollowers(), Epsilon());

        Add(table, DoctorNonTerminal.Expression, ExpressionStarts(), N(DoctorNonTerminal.SimpleExpression), N(DoctorNonTerminal.RelationTail));
        Add(table, DoctorNonTerminal.RelationTail, ComparisonOperators(), N(DoctorNonTerminal.RelationOperator), N(DoctorNonTerminal.SimpleExpression));
        Add(table, DoctorNonTerminal.RelationTail, ExpressionFollowers(), Epsilon());
        AddTerminalLookaheads(table, DoctorNonTerminal.RelationOperator, ComparisonOperators());

        Add(table, DoctorNonTerminal.SimpleExpression, SimpleExpressionStarts(), N(DoctorNonTerminal.SignOpt), N(DoctorNonTerminal.Term), N(DoctorNonTerminal.AdditionTail));
        Add(table, DoctorNonTerminal.SignOpt, new[] { TokenType.Plus }, T(TokenType.Plus));
        Add(table, DoctorNonTerminal.SignOpt, new[] { TokenType.Minus }, T(TokenType.Minus));
        Add(table, DoctorNonTerminal.SignOpt, FactorStarts(), Epsilon());
        Add(table, DoctorNonTerminal.Term, FactorStarts(), N(DoctorNonTerminal.Factor), N(DoctorNonTerminal.MultiplicationTail));

        Add(table, DoctorNonTerminal.AdditionTail, new[] { TokenType.Plus, TokenType.Minus, TokenType.OrOr }, N(DoctorNonTerminal.AdditionOperator), N(DoctorNonTerminal.Term), N(DoctorNonTerminal.AdditionTail));
        Add(table, DoctorNonTerminal.AdditionTail, ComparisonOperators().Concat(ExpressionFollowers()), Epsilon());
        AddTerminalLookaheads(table, DoctorNonTerminal.AdditionOperator, new[] { TokenType.Plus, TokenType.Minus, TokenType.OrOr });

        Add(table, DoctorNonTerminal.MultiplicationTail, new[] { TokenType.Star, TokenType.Slash, TokenType.Backslash, TokenType.Percent, TokenType.Caret, TokenType.AndAnd }, N(DoctorNonTerminal.MultiplicationOperator), N(DoctorNonTerminal.Factor), N(DoctorNonTerminal.MultiplicationTail));
        Add(table, DoctorNonTerminal.MultiplicationTail, new[] { TokenType.Plus, TokenType.Minus, TokenType.OrOr }.Concat(ComparisonOperators()).Concat(ExpressionFollowers()), Epsilon());
        AddTerminalLookaheads(table, DoctorNonTerminal.MultiplicationOperator, new[] { TokenType.Star, TokenType.Slash, TokenType.Backslash, TokenType.Percent, TokenType.Caret, TokenType.AndAnd });

        Add(table, DoctorNonTerminal.Factor, new[] { TokenType.Integer }, T(TokenType.Integer));
        Add(table, DoctorNonTerminal.Factor, new[] { TokenType.Real }, T(TokenType.Real));
        Add(table, DoctorNonTerminal.Factor, new[] { TokenType.String }, T(TokenType.String));
        Add(table, DoctorNonTerminal.Factor, new[] { TokenType.Character }, T(TokenType.Character));
        Add(table, DoctorNonTerminal.Factor, new[] { TokenType.True }, T(TokenType.True));
        Add(table, DoctorNonTerminal.Factor, new[] { TokenType.False }, T(TokenType.False));
        Add(table, DoctorNonTerminal.Factor, new[] { TokenType.Identifier }, N(DoctorNonTerminal.Access));
        Add(table, DoctorNonTerminal.Factor, new[] { TokenType.LeftParen }, T(TokenType.LeftParen), N(DoctorNonTerminal.Expression), T(TokenType.RightParen));
        Add(table, DoctorNonTerminal.Factor, new[] { TokenType.Bang }, T(TokenType.Bang), N(DoctorNonTerminal.Factor));

        return table;
    }

    private static void Add(
        Dictionary<(DoctorNonTerminal NonTerminal, TokenType Lookahead), DoctorGrammarSymbol[]> table,
        DoctorNonTerminal nonTerminal,
        IEnumerable<TokenType> lookaheads,
        params DoctorGrammarSymbol[] production)
    {
        foreach (TokenType lookahead in lookaheads.Distinct())
        {
            if (!table.TryAdd((nonTerminal, lookahead), production))
            {
                throw new InvalidOperationException("LL(1) conflict at " + nonTerminal + " with lookahead " + lookahead + ".");
            }
        }
    }

    private static void AddTerminalLookaheads(
        Dictionary<(DoctorNonTerminal NonTerminal, TokenType Lookahead), DoctorGrammarSymbol[]> table,
        DoctorNonTerminal nonTerminal,
        IEnumerable<TokenType> lookaheads)
    {
        foreach (TokenType lookahead in lookaheads.Distinct())
        {
            Add(table, nonTerminal, new[] { lookahead }, T(lookahead));
        }
    }

    private static DoctorGrammarSymbol[] Epsilon() => Array.Empty<DoctorGrammarSymbol>();

    private static DoctorGrammarSymbol T(TokenType terminal) => DoctorGrammarSymbol.ForTerminal(terminal);

    private static DoctorGrammarSymbol N(DoctorNonTerminal nonTerminal) => DoctorGrammarSymbol.ForNonTerminal(nonTerminal);

    private static TokenType[] StatementStarts() => new[] { TokenType.Print, TokenType.Identifier, TokenType.Read, TokenType.If, TokenType.Repeat, TokenType.While, TokenType.RepeatUntil, TokenType.LeftBrace };

    private static TokenType[] DefinitionStarts() => new[] { TokenType.Const, TokenType.TypeDeclaration, TokenType.Variable, TokenType.Procedure };

    private static TokenType[] FactorStarts() => new[] { TokenType.Integer, TokenType.Real, TokenType.String, TokenType.Character, TokenType.True, TokenType.False, TokenType.Identifier, TokenType.LeftParen, TokenType.Bang };

    private static TokenType[] SimpleExpressionStarts() => new[] { TokenType.Plus, TokenType.Minus }.Concat(FactorStarts()).ToArray();

    private static TokenType[] ExpressionStarts() => SimpleExpressionStarts();

    private static TokenType[] ComparisonOperators() => new[] { TokenType.Less, TokenType.Greater, TokenType.LessEqual, TokenType.GreaterEqual, TokenType.EqualEqual, TokenType.BangEqual };

    private static TokenType[] ExpressionFollowers() => new[] { TokenType.RightParen, TokenType.RightBracket, TokenType.Comma, TokenType.Semicolon, TokenType.To, TokenType.Step };

    private static TokenType[] AccessFollowers()
    {
        return new[] { TokenType.Assign, TokenType.Star, TokenType.Slash, TokenType.Backslash, TokenType.Percent, TokenType.Caret, TokenType.AndAnd, TokenType.Plus, TokenType.Minus, TokenType.OrOr }
            .Concat(ComparisonOperators())
            .Concat(ExpressionFollowers())
            .ToArray();
    }

    private static string FormatProduction(IReadOnlyList<DoctorGrammarSymbol> production)
    {
        return production.Count == 0
            ? "ε"
            : string.Join(" ", production.Select(symbol => symbol.IsTerminal ? symbol.Terminal.ToString() : symbol.NonTerminal.ToString()));
    }

    private static string GetExpectedLookaheads(DoctorNonTerminal nonTerminal)
    {
        return string.Join(", ", ParseTable.Keys.Where(key => key.NonTerminal == nonTerminal).Select(key => key.Lookahead).Distinct().OrderBy(item => item));
    }

    private void AddTrace(string stack, Token lookahead, string action)
    {
        if (_traceSteps.Count >= MaxTraceSteps)
        {
            return;
        }

        _traceSteps.Add(new Ll1TraceStep(_traceSteps.Count + 1, stack, Describe(lookahead), action));
    }

    private static string FormatStack(DoctorParseStackEntry current, Stack<DoctorParseStackEntry> remaining)
    {
        IEnumerable<string> items = new[] { current.Symbol.DisplayName }.Concat(remaining.Select(item => item.Symbol.DisplayName));
        return string.Join(" ", items);
    }

    private void Report(string code, string message, SourceSpan span)
    {
        _diagnostics.Report(code, message, span);
    }

    private static string Describe(Token token)
    {
        return token.Type == TokenType.Eof ? "نهاية الملف" : "\"" + token.Lexeme + "\" (" + token.Type + ")";
    }

    private Token Advance()
    {
        Token current = Current;
        if (!IsAtEnd)
        {
            _position++;
        }

        return current;
    }

    private Token Current => _tokens[Math.Min(_position, _tokens.Count - 1)];

    private bool IsAtEnd => Current.Type == TokenType.Eof;

    private enum DoctorNonTerminal
    {
        Start,
        ProgramUnit,
        Block,
        DefinitionList,
        Definition,
        ConstantDeclaration,
        ConstantValue,
        TypeDeclaration,
        CompositeType,
        ListType,
        RecordType,
        FieldList,
        FieldListTail,
        FieldDeclaration,
        ProcedureDeclaration,
        FormalParametersOpt,
        FormalParametersTail,
        FormalParameter,
        PassingMode,
        VariableDeclaration,
        NameList,
        NameListTail,
        DataType,
        StatementList,
        Statement,
        PrintStatement,
        PrintItems,
        PrintItemsTail,
        ReadStatement,
        IfStatement,
        ElsePart,
        RepeatToStatement,
        StepOpt,
        WhileStatement,
        RepeatUntilStatement,
        IdentifierStatement,
        IdentifierStatementTail,
        ActualArgumentsOpt,
        ActualArguments,
        ActualArgumentsTail,
        Access,
        AccessTail,
        Expression,
        RelationTail,
        RelationOperator,
        SimpleExpression,
        SignOpt,
        Term,
        AdditionTail,
        AdditionOperator,
        MultiplicationTail,
        MultiplicationOperator,
        Factor
    }

    private sealed class DoctorGrammarSymbol
    {
        private DoctorGrammarSymbol(TokenType terminal)
        {
            IsTerminal = true;
            Terminal = terminal;
        }

        private DoctorGrammarSymbol(DoctorNonTerminal nonTerminal)
        {
            NonTerminal = nonTerminal;
        }

        public bool IsTerminal { get; }
        public TokenType Terminal { get; }
        public DoctorNonTerminal NonTerminal { get; }
        public string DisplayName => IsTerminal ? Terminal.ToString() : NonTerminal.ToString();

        public static DoctorGrammarSymbol ForTerminal(TokenType terminal) => new(terminal);

        public static DoctorGrammarSymbol ForNonTerminal(DoctorNonTerminal nonTerminal) => new(nonTerminal);
    }

    private sealed record DoctorParseStackEntry(DoctorGrammarSymbol Symbol, DoctorParseTreeNode Node);
}

/// <summary>
/// Represents one visible node in the concrete parse tree created by DoctorLl1Parser.
/// </summary>
public sealed class DoctorParseTreeNode
{
    private DoctorParseTreeNode(string label)
    {
        Label = label;
    }

    /// <summary>
    /// Gets the grammar label for this terminal or non-terminal node.
    /// </summary>
    public string Label { get; }

    /// <summary>
    /// Gets the source token when this node represents a matched terminal.
    /// </summary>
    public Token? MatchedToken { get; set; }

    /// <summary>
    /// Gets the nodes created by the production selected for this node.
    /// </summary>
    public List<DoctorParseTreeNode> Children { get; } = new();

    internal static DoctorParseTreeNode ForNonTerminal(object nonTerminal) => new(nonTerminal.ToString() ?? string.Empty);

    internal static DoctorParseTreeNode ForTerminal(TokenType terminal) => new(terminal.ToString());
}

/// <summary>
/// Combines the official concrete parse tree, diagnostics, and actual LL(1) trace.
/// </summary>
public sealed record DoctorParseResult(
    DoctorParseTreeNode ParseTree,
    IReadOnlyList<Diagnostic> Diagnostics,
    IReadOnlyList<Ll1TraceStep> TraceSteps)
{
    /// <summary>
    /// Gets the shared AST lowered from the official parse tree when parsing succeeds.
    /// </summary>
    public BayanCompiler.Syntax.ProgramNode? Program { get; init; }

    /// <summary>
    /// Indicates whether the parser reached an error-free complete derivation.
    /// </summary>
    public bool Succeeded => Diagnostics.Count == 0;
}
