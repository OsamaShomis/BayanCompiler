using BayanCompiler.Frontend;
using BayanCompiler.Lexing;
using BayanCompiler.Syntax;

namespace BayanCompiler.Semantics;

/// <summary>
/// Performs semantic analysis for the official doctor grammar and produces a real root symbol table.
/// </summary>
public sealed class DoctorSemanticAnalyzer
{
    private readonly DiagnosticBag _diagnostics = new();
    private SymbolTable _rootScope = new();
    private SymbolTable _scope = new();

    /// <summary>
    /// Analyzes an official program AST after successful DoctorLl1Parser lowering.
    /// </summary>
    public DoctorSemanticResult Analyze(ProgramNode program)
    {
        _rootScope = new SymbolTable();
        _scope = _rootScope;

        foreach (DeclarationNode declaration in program.Declarations)
        {
            VisitDeclaration(declaration);
        }

        foreach (StatementNode statement in program.Statements)
        {
            VisitStatement(statement);
        }

        return new DoctorSemanticResult(_diagnostics.Items, _rootScope.FormatCurrentScope());
    }

    private void VisitDeclaration(DeclarationNode declaration)
    {
        switch (declaration)
        {
            case ConstantDeclarationNode constant:
                Declare(new Symbol(constant.Name, AnalyzeExpression(constant.Value), constant.Span) { Kind = SymbolKind.Constant, IsReadOnly = true });
                break;

            case TypeDeclarationNode typeDeclaration:
                TypeDescriptor typeDescriptor = ResolveTypeDescriptor(typeDeclaration.Definition);
                Declare(new Symbol(typeDeclaration.Name, typeDescriptor.Kind, typeDeclaration.Span)
                {
                    Kind = SymbolKind.Type,
                    IsReadOnly = true,
                    DetailedType = typeDescriptor
                });
                break;

            case VariableDeclarationGroupNode variables:
                TypeDescriptor variableDescriptor = ResolveTypeDescriptor(variables.Type);
                LanguageType declaredType = variableDescriptor.Kind;
                foreach (string name in variables.Names)
                {
                    Declare(new Symbol(name, declaredType, variables.Span) { Kind = SymbolKind.Variable, DetailedType = variableDescriptor });
                }

                if (variables.Initializer is not null)
                {
                    RequireAssignable(declaredType, AnalyzeExpression(variables.Initializer), variables.Initializer.Span, "مجموعة المتغيرات");
                }

                break;

            case ProcedureDeclarationNode procedure:
                IReadOnlyList<ProcedureParameterSignature> signatures = procedure.Parameters
                    .Select(parameter =>
                    {
                        TypeDescriptor descriptor = ResolveTypeDescriptor(parameter.Type);
                        return new ProcedureParameterSignature(parameter.Name, descriptor.Kind, parameter.PassingMode) { DetailedType = descriptor };
                    })
                    .ToList();
                Declare(new Symbol(procedure.Name, LanguageType.Procedure, procedure.Span)
                {
                    Kind = SymbolKind.Procedure,
                    IsReadOnly = true,
                    Parameters = signatures
                });
                VisitProcedure(procedure);
                break;
        }
    }

    private void VisitProcedure(ProcedureDeclarationNode procedure)
    {
        SymbolTable parent = _scope;
        _scope = parent.CreateChildScope();

        foreach (ParameterNode parameter in procedure.Parameters)
        {
            TypeDescriptor descriptor = ResolveTypeDescriptor(parameter.Type);
            Declare(new Symbol(parameter.Name, descriptor.Kind, parameter.Span) { Kind = SymbolKind.Parameter, DetailedType = descriptor });
        }

        foreach (DeclarationNode declaration in procedure.Body.Declarations)
        {
            VisitDeclaration(declaration);
        }

        foreach (StatementNode statement in procedure.Body.Statements)
        {
            VisitStatement(statement);
        }

        _scope = parent;
    }

    private void VisitStatement(StatementNode statement)
    {
        switch (statement)
        {
            case AssignmentNode assignment:
                VisitAssignment(assignment);
                break;

            case ComplexAssignmentNode assignment:
                RequireAssignable(AnalyzeAssignableTarget(assignment.Target, "الإسناد"), AnalyzeExpression(assignment.Expression), assignment.Expression.Span, "هدف الإسناد");
                break;

            case PrintNode print:
                AnalyzeExpression(print.Expression);
                break;

            case PrintListNode printList:
                foreach (ExpressionNode expression in printList.Expressions)
                {
                    AnalyzeExpression(expression);
                }

                break;

            case ReadNode read:
                AnalyzeAssignableTarget(read.Target, "الإدخال");
                break;

            case IfNode ifNode:
                RequireBool(AnalyzeExpression(ifNode.Condition), ifNode.Condition.Span, "شرط إذا");
                VisitBlock(ifNode.ThenBlock);
                if (ifNode.ElseBlock is not null)
                {
                    VisitBlock(ifNode.ElseBlock);
                }

                break;

            case WhileNode whileNode:
                RequireBool(AnalyzeExpression(whileNode.Condition), whileNode.Condition.Span, "شرط طالما");
                VisitBlock(whileNode.Body);
                break;

            case RepeatToNode repeatTo:
                VisitRepeatTo(repeatTo);
                break;

            case RepeatUntilNode repeatUntil:
                VisitBlock(repeatUntil.Body);
                RequireBool(AnalyzeExpression(repeatUntil.Condition), repeatUntil.Condition.Span, "شرط أعد حتى");
                break;

            case ExpressionStatementNode expressionStatement:
                AnalyzeExpression(expressionStatement.Expression);
                break;

            case BlockNode block:
                VisitBlock(block);
                break;
        }
    }

    private void VisitAssignment(AssignmentNode assignment)
    {
        if (!_scope.TryLookup(assignment.Name, out Symbol symbol))
        {
            Report("SEM001", "الاسم \"" + assignment.Name + "\" غير معرّف قبل استخدامه.", assignment.Span);
            AnalyzeExpression(assignment.Expression);
            return;
        }

        if (symbol.IsReadOnly)
        {
            Report("SEM007", "لا يمكن الإسناد إلى الاسم الثابت \"" + assignment.Name + "\".", assignment.Span);
        }

        RequireAssignable(symbol.Type, AnalyzeExpression(assignment.Expression), assignment.Expression.Span, assignment.Name);
    }

    private void VisitRepeatTo(RepeatToNode repeatTo)
    {
        if (!_scope.TryLookup(repeatTo.IteratorName, out Symbol iterator))
        {
            Report("SEM001", "متغير التكرار \"" + repeatTo.IteratorName + "\" غير معرّف قبل استخدامه.", repeatTo.Span);
        }
        else if (iterator.IsReadOnly)
        {
            Report("SEM007", "لا يمكن استخدام الثابت \"" + repeatTo.IteratorName + "\" كمتغير تكرار.", repeatTo.Span);
        }

        RequireNumeric(AnalyzeExpression(repeatTo.Start), repeatTo.Start.Span, "قيمة بداية كرر");
        RequireNumeric(AnalyzeExpression(repeatTo.End), repeatTo.End.Span, "قيمة نهاية كرر");
        if (repeatTo.Step is not null)
        {
            RequireNumeric(AnalyzeExpression(repeatTo.Step), repeatTo.Step.Span, "قيمة أضف في كرر");
        }

        VisitBlock(repeatTo.Body);
    }

    private void VisitBlock(BlockNode block)
    {
        SymbolTable parent = _scope;
        _scope = parent.CreateChildScope();

        foreach (DeclarationNode declaration in block.Declarations)
        {
            VisitDeclaration(declaration);
        }

        foreach (StatementNode statement in block.Statements)
        {
            VisitStatement(statement);
        }

        _scope = parent;
    }

    private LanguageType AnalyzeExpression(ExpressionNode expression)
    {
        switch (expression)
        {
            case LiteralNode literal:
                return TypeFromLiteral(literal.LiteralType);

            case IdentifierNode identifier:
                return LookupIdentifier(identifier);

            case IndexedAccessNode indexed:
                return AnalyzeIndexedAccess(indexed);

            case FieldAccessNode field:
                return AnalyzeFieldAccess(field);

            case GroupingNode grouping:
                return AnalyzeExpression(grouping.Expression);

            case UnaryExpressionNode unary:
                return AnalyzeUnary(unary);

            case BinaryExpressionNode binary:
                return AnalyzeBinary(binary);

            case CallExpressionNode call:
                return AnalyzeCall(call);

            default:
                return LanguageType.Unknown;
        }
    }

    private LanguageType AnalyzeCall(CallExpressionNode call)
    {
        if (!_scope.TryLookup(call.ProcedureName, out Symbol symbol) || symbol.Kind != SymbolKind.Procedure)
        {
            Report("SEM001", "الإجراء \"" + call.ProcedureName + "\" غير معرّف قبل استدعائه.", call.Span);
            foreach (ExpressionNode argument in call.Arguments)
            {
                AnalyzeExpression(argument);
            }

            return LanguageType.Void;
        }

        if (symbol.Parameters.Count != call.Arguments.Count)
        {
            Report("SEM009", "الإجراء \"" + call.ProcedureName + "\" يتوقع " + symbol.Parameters.Count + " معامل/معاملات لكنه استلم " + call.Arguments.Count + ".", call.Span);
        }

        int sharedCount = Math.Min(symbol.Parameters.Count, call.Arguments.Count);
        for (int index = 0; index < call.Arguments.Count; index++)
        {
            ExpressionNode argument = call.Arguments[index];
            LanguageType argumentType = AnalyzeExpression(argument);
            if (index >= sharedCount)
            {
                continue;
            }

            ProcedureParameterSignature parameter = symbol.Parameters[index];
            RequireAssignable(parameter.Type, argumentType, argument.Span, "المعامل " + parameter.Name);
            if (parameter.PassingMode == ParameterPassingMode.ByReference && !IsWritableReferenceArgument(argument))
            {
                Report("SEM010", "المعامل \"" + parameter.Name + "\" يمرر بالمرجع ويحتاج متغير وصول قابل للإسناد.", argument.Span);
            }
        }

        return LanguageType.Void;
    }

    private bool IsWritableReferenceArgument(ExpressionNode argument)
    {
        if (argument is IdentifierNode identifier)
        {
            return _scope.TryLookup(identifier.Name, out Symbol symbol) && !symbol.IsReadOnly;
        }

        return argument is IndexedAccessNode or FieldAccessNode;
    }

    private LanguageType AnalyzeUnary(UnaryExpressionNode unary)
    {
        LanguageType operand = AnalyzeExpression(unary.Operand);
        if (unary.Operator == TokenType.Bang)
        {
            RequireBool(operand, unary.Operand.Span, "العامل !");
            return LanguageType.Bool;
        }

        if (unary.Operator == TokenType.Minus)
        {
            RequireNumeric(operand, unary.Operand.Span, "العامل -");
            return operand;
        }

        return LanguageType.Unknown;
    }

    private LanguageType AnalyzeBinary(BinaryExpressionNode binary)
    {
        LanguageType left = AnalyzeExpression(binary.Left);
        LanguageType right = AnalyzeExpression(binary.Right);
        if (binary.Operator is TokenType.Plus or TokenType.Minus or TokenType.Star or TokenType.Slash or TokenType.Backslash or TokenType.Percent or TokenType.Caret)
        {
            if (binary.Operator == TokenType.Plus && left == LanguageType.Text && right == LanguageType.Text)
            {
                return LanguageType.Text;
            }

            RequireNumeric(left, binary.Left.Span, "الطرف الأيسر للعامل الحسابي");
            RequireNumeric(right, binary.Right.Span, "الطرف الأيمن للعامل الحسابي");
            if (binary.Operator == TokenType.Slash)
            {
                return LanguageType.Real;
            }

            if (binary.Operator is TokenType.Backslash or TokenType.Percent)
            {
                return left == LanguageType.Int && right == LanguageType.Int ? LanguageType.Int : LanguageType.Unknown;
            }

            return left == LanguageType.Real || right == LanguageType.Real ? LanguageType.Real : LanguageType.Int;
        }

        if (binary.Operator is TokenType.Less or TokenType.LessEqual or TokenType.Greater or TokenType.GreaterEqual)
        {
            RequireNumeric(left, binary.Left.Span, "الطرف الأيسر للمقارنة");
            RequireNumeric(right, binary.Right.Span, "الطرف الأيمن للمقارنة");
            return LanguageType.Bool;
        }

        if (binary.Operator is TokenType.EqualEqual or TokenType.BangEqual)
        {
            if (!AreComparable(left, right) && !HasUnknown(left, right))
            {
                Report("SEM005", "لا يمكن مقارنة النوع " + DisplayType(left) + " بالنوع " + DisplayType(right) + ".", binary.Span);
            }

            return LanguageType.Bool;
        }

        if (binary.Operator is TokenType.AndAnd or TokenType.OrOr)
        {
            RequireBool(left, binary.Left.Span, "الطرف الأيسر للعامل المنطقي");
            RequireBool(right, binary.Right.Span, "الطرف الأيمن للعامل المنطقي");
            return LanguageType.Bool;
        }

        return LanguageType.Unknown;
    }

    private LanguageType AnalyzeAssignableTarget(ExpressionNode target, string context)
    {
        if (target is IdentifierNode identifier)
        {
            if (!_scope.TryLookup(identifier.Name, out Symbol symbol))
            {
                Report("SEM001", "الاسم \"" + identifier.Name + "\" غير معرّف قبل استخدامه.", identifier.Span);
                return LanguageType.Unknown;
            }

            if (symbol.IsReadOnly)
            {
                Report("SEM007", "لا يمكن استخدام الثابت \"" + identifier.Name + "\" كهدف لـ" + context + ".", identifier.Span);
            }

            return symbol.Type;
        }

        if (target is IndexedAccessNode indexed)
        {
            return AnalyzeIndexedAccess(indexed);
        }

        if (target is FieldAccessNode field)
        {
            return AnalyzeFieldAccess(field);
        }

        Report("SEM008", "هدف " + context + " غير قابل للإسناد.", target.Span);
        return LanguageType.Unknown;
    }

    private LanguageType AnalyzeIndexedAccess(IndexedAccessNode indexed)
    {
        LanguageType collectionType = AnalyzeExpression(indexed.Collection);
        RequireNumeric(AnalyzeExpression(indexed.Index), indexed.Index.Span, "فهرس القائمة");
        TypeDescriptor? descriptor = ResolveAccessDescriptor(indexed.Collection);
        if (descriptor?.Kind != LanguageType.List)
        {
            if (collectionType != LanguageType.Unknown)
            {
                Report("SEM012", "الفهرسة بالمربعين تتطلب قيمة من النوع قائمة.", indexed.Collection.Span);
            }

            return LanguageType.Unknown;
        }

        return descriptor.ElementType?.Kind ?? LanguageType.Unknown;
    }

    private LanguageType AnalyzeFieldAccess(FieldAccessNode field)
    {
        LanguageType recordType = AnalyzeExpression(field.Record);
        TypeDescriptor? descriptor = ResolveAccessDescriptor(field.Record);
        if (descriptor?.Kind != LanguageType.Record)
        {
            if (recordType != LanguageType.Unknown)
            {
                Report("SEM013", "الوصول بالنقطة يتطلب قيمة من النوع سجل.", field.Record.Span);
            }

            return LanguageType.Unknown;
        }

        if (descriptor.Fields is null || !descriptor.Fields.TryGetValue(field.FieldName, out TypeDescriptor? fieldType))
        {
            Report("SEM014", "الحقل \"" + field.FieldName + "\" غير معرّف في السجل.", field.Span);
            return LanguageType.Unknown;
        }

        return fieldType.Kind;
    }

    private TypeDescriptor? ResolveAccessDescriptor(ExpressionNode expression)
    {
        if (expression is IdentifierNode identifier && _scope.TryLookup(identifier.Name, out Symbol symbol))
        {
            return symbol.DetailedType ?? TypeDescriptor.Primitive(symbol.Type);
        }

        if (expression is IndexedAccessNode indexed)
        {
            TypeDescriptor? collection = ResolveAccessDescriptor(indexed.Collection);
            return collection?.Kind == LanguageType.List ? collection.ElementType : null;
        }

        if (expression is FieldAccessNode field)
        {
            TypeDescriptor? record = ResolveAccessDescriptor(field.Record);
            return record?.Kind == LanguageType.Record && record.Fields is not null && record.Fields.TryGetValue(field.FieldName, out TypeDescriptor? fieldType)
                ? fieldType
                : null;
        }

        return null;
    }

    private TypeDescriptor ResolveTypeDescriptor(TypeSyntaxNode type)
    {
        if (type is NamedTypeSyntaxNode named)
        {
            switch (named.TypeToken)
            {
                case TokenType.TypeInt:
                    return TypeDescriptor.Primitive(LanguageType.Int);
                case TokenType.TypeReal:
                    return TypeDescriptor.Primitive(LanguageType.Real);
                case TokenType.TypeBool:
                    return TypeDescriptor.Primitive(LanguageType.Bool);
                case TokenType.TypeChar:
                    return TypeDescriptor.Primitive(LanguageType.Char);
                case TokenType.TypeString:
                    return TypeDescriptor.Primitive(LanguageType.Text);
                case TokenType.Identifier when _scope.TryLookup(named.Name, out Symbol known) && known.Kind == SymbolKind.Type:
                    return known.DetailedType ?? TypeDescriptor.Primitive(known.Type);
                default:
                    ReportUnknownType(named);
                    return TypeDescriptor.Primitive(LanguageType.Unknown);
            }
        }

        if (type is ListTypeSyntaxNode list)
        {
            RequireNumeric(AnalyzeExpression(list.Size), list.Size.Span, "طول القائمة");
            int length = 0;
            if (list.Size is LiteralNode { LiteralType: TokenType.Integer } literal)
            {
                int.TryParse(literal.Value, out length);
            }

            if (length <= 0)
            {
                Report("SEM011", "طول القائمة يجب أن يكون عدداً صحيحاً موجباً.", list.Size.Span);
            }

            return TypeDescriptor.List(ResolveTypeDescriptor(list.ElementType), length);
        }

        if (type is RecordTypeSyntaxNode record)
        {
            var fields = new Dictionary<string, TypeDescriptor>(StringComparer.Ordinal);
            foreach (FieldDeclarationNode field in record.Fields)
            {
                if (!fields.TryAdd(field.Name, ResolveTypeDescriptor(field.Type)))
                {
                    Report("SEM002", "الحقل \"" + field.Name + "\" معرف مسبقاً في السجل.", field.Span);
                }
            }

            return TypeDescriptor.Record(null, fields);
        }

        return TypeDescriptor.Primitive(LanguageType.Unknown);
    }

    private LanguageType ResolveType(TypeSyntaxNode type)
    {
        if (type is NamedTypeSyntaxNode named)
        {
            return named.TypeToken switch
            {
                TokenType.TypeInt => LanguageType.Int,
                TokenType.TypeReal => LanguageType.Real,
                TokenType.TypeBool => LanguageType.Bool,
                TokenType.TypeChar => LanguageType.Char,
                TokenType.TypeString => LanguageType.Text,
                TokenType.Identifier when _scope.TryLookup(named.Name, out Symbol known) && known.Kind == SymbolKind.Type => known.Type,
                _ => ReportUnknownType(named)
            };
        }

        if (type is ListTypeSyntaxNode list)
        {
            RequireNumeric(AnalyzeExpression(list.Size), list.Size.Span, "طول القائمة");
            ResolveType(list.ElementType);
            return LanguageType.List;
        }

        if (type is RecordTypeSyntaxNode record)
        {
            var fields = new HashSet<string>(StringComparer.Ordinal);
            foreach (FieldDeclarationNode field in record.Fields)
            {
                if (!fields.Add(field.Name))
                {
                    Report("SEM002", "الحقل \"" + field.Name + "\" معرف مسبقاً في السجل.", field.Span);
                }

                ResolveType(field.Type);
            }

            return LanguageType.Record;
        }

        return LanguageType.Unknown;
    }

    private LanguageType LookupIdentifier(IdentifierNode identifier)
    {
        if (_scope.TryLookup(identifier.Name, out Symbol symbol))
        {
            return symbol.Type;
        }

        Report("SEM001", "الاسم \"" + identifier.Name + "\" غير معرّف قبل استخدامه.", identifier.Span);
        return LanguageType.Unknown;
    }

    private void Declare(Symbol symbol)
    {
        if (!_scope.TryDeclare(symbol))
        {
            Report("SEM002", "الاسم \"" + symbol.Name + "\" معرف مسبقاً في هذا النطاق.", symbol.DeclarationSpan);
        }
    }

    private LanguageType ReportUnknownType(NamedTypeSyntaxNode type)
    {
        Report("SEM006", "النوع \"" + type.Name + "\" غير معرّف.", type.Span);
        return LanguageType.Unknown;
    }

    private void RequireAssignable(LanguageType target, LanguageType source, SourceSpan span, string targetName)
    {
        if (!CanAssign(target, source))
        {
            Report("SEM003", "لا يمكن إسناد قيمة من النوع " + DisplayType(source) + " إلى \"" + targetName + "\" من النوع " + DisplayType(target) + ".", span);
        }
    }

    private void RequireBool(LanguageType type, SourceSpan span, string context)
    {
        if (type != LanguageType.Unknown && type != LanguageType.Bool)
        {
            Report("SEM004", context + " يجب أن يكون من النوع منطقي، لكنه من النوع " + DisplayType(type) + ".", span);
        }
    }

    private void RequireNumeric(LanguageType type, SourceSpan span, string context)
    {
        if (type != LanguageType.Unknown && !IsNumeric(type))
        {
            Report("SEM005", context + " يجب أن يكون من النوع صحيح أو حقيقي، لكنه من النوع " + DisplayType(type) + ".", span);
        }
    }

    private static LanguageType TypeFromLiteral(TokenType type) => type switch
    {
        TokenType.Integer => LanguageType.Int,
        TokenType.Real => LanguageType.Real,
        TokenType.String => LanguageType.Text,
        TokenType.Character => LanguageType.Char,
        TokenType.True or TokenType.False => LanguageType.Bool,
        _ => LanguageType.Unknown
    };

    private static bool IsNumeric(LanguageType type) => type is LanguageType.Int or LanguageType.Real;

    private static bool AreComparable(LanguageType left, LanguageType right) => left == right || (IsNumeric(left) && IsNumeric(right));

    private static bool HasUnknown(LanguageType left, LanguageType right) => left == LanguageType.Unknown || right == LanguageType.Unknown;

    private static bool CanAssign(LanguageType target, LanguageType source)
    {
        return target == LanguageType.Unknown || source == LanguageType.Unknown || target == source || (target == LanguageType.Real && source == LanguageType.Int);
    }

    private static string DisplayType(LanguageType type) => type switch
    {
        LanguageType.Int => "صحيح",
        LanguageType.Real => "حقيقي",
        LanguageType.Bool => "منطقي",
        LanguageType.Char => "حرفي",
        LanguageType.Text => "خيط رمزي",
        LanguageType.List => "قائمة",
        LanguageType.Record => "سجل",
        LanguageType.Procedure => "إجراء",
        LanguageType.Void => "فارغ",
        _ => "غير معروف"
    };

    private void Report(string code, string message, SourceSpan span)
    {
        _diagnostics.Report(code, message, span);
    }
}

/// <summary>
/// Combines official semantic diagnostics with the root-scope symbol-table artifact.
/// </summary>
public sealed record DoctorSemanticResult(IReadOnlyList<Diagnostic> Diagnostics, string SymbolTable)
{
    /// <summary>
    /// Indicates that semantic analysis completed without errors.
    /// </summary>
    public bool Succeeded => Diagnostics.Count == 0;
}
