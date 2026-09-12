using BayanCompiler.Frontend;
using BayanCompiler.Lexing;
using BayanCompiler.Semantics;
using BayanCompiler.Syntax;

namespace BayanCompiler.Intermediate;

/// <summary>
/// يحول AST السليمة دلالياً إلى تمثيل وسيط Three-Address Code مناسب لـMIPS.
/// </summary>
public sealed class IrGenerator
{
    // يحتفظ بالبرنامج الوسيط الجاري بناؤه.
    private IrProgram _program = new IrProgram();

    // يمثل نطاق الأسماء الحالي ويحول أسماء بيان العربية إلى تسميات MIPS آمنة.
    private IrScope _scope = new IrScope(null);

    // يولد أرقاماً فريدة للقيم المؤقتة والمتغيرات والعلامات والنصوص.
    private int _temporaryCounter;
    private int _variableCounter;
    private int _labelCounter;
    private int _stringCounter;
    private int _floatCounter;
    private int _bufferCounter;

    // Stores procedure labels and static parameter locations for the supported non-recursive backend subset.
    private readonly Dictionary<string, ProcedureBinding> _procedures = new(StringComparer.Ordinal);

    // Maps official named types and allocated storage labels to their word-level backend layouts.
    private readonly Dictionary<string, BackendTypeLayout> _namedTypeLayouts = new(StringComparer.Ordinal);
    private readonly Dictionary<string, BackendTypeLayout> _storageLayouts = new(StringComparer.Ordinal);

    /// <summary>
    /// يحول برنامج بيان كاملاً إلى IR.
    /// </summary>
    public IrProgram Generate(ProgramNode program)
    {
        // يعيد تهيئة الحالة حتى تكون كل عملية توليد مستقلة.
        _program = new IrProgram();
        _scope = new IrScope(null);
        _temporaryCounter = 0;
        _variableCounter = 0;
        _labelCounter = 0;
        _stringCounter = 0;
        _floatCounter = 0;
        _bufferCounter = 0;
        _procedures.Clear();
        _namedTypeLayouts.Clear();
        _storageLayouts.Clear();

        foreach (TypeDeclarationNode typeDeclaration in program.Declarations.OfType<TypeDeclarationNode>())
        {
            _namedTypeLayouts[typeDeclaration.Name] = ResolveBackendLayout(typeDeclaration.Definition);
        }

        IReadOnlyList<ProcedureDeclarationNode> procedures = program.Declarations.OfType<ProcedureDeclarationNode>().ToList();

        // Register all procedure signatures before their bodies or call sites are lowered.
        foreach (ProcedureDeclarationNode procedure in procedures)
        {
            RegisterProcedure(procedure);
        }

        // Generate non-procedure declarations before executable statements consume their names.
        foreach (DeclarationNode declaration in program.Declarations)
        {
            if (declaration is not ProcedureDeclarationNode)
            {
                GenerateOfficialDeclaration(declaration);
            }
        }

        if (procedures.Count > 0)
        {
            string mainLabel = CreateLabel("main");
            _program.Emit(new IrInstruction(IrOpcode.Goto, null, null, null, null, mainLabel, program.Span));
            foreach (ProcedureDeclarationNode procedure in procedures)
            {
                GenerateProcedure(procedure);
            }

            EmitLabel(mainLabel, program.Span);
        }

        // يولد التعليمات لكل عبارة في البرنامج بالترتيب.
        foreach (StatementNode statement in program.Statements)
        {
            GenerateStatement(statement);
        }

        // يعيد البرنامج الوسيط ليعرض أو يحول لاحقاً إلى MIPS.
        return _program;
    }

    /// <summary>
    /// يحول عبارة واحدة إلى تعليمات IR.
    /// </summary>
    private void GenerateStatement(StatementNode statement)
    {
        // يولد تعريف المتغير ومساحة التخزين والقيمة الابتدائية.
        if (statement is VarDeclarationNode declaration)
        {
            GenerateDeclaration(declaration);
            return;
        }

        // يولد إسناد قيمة جديدة إلى متغير موجود.
        if (statement is AssignmentNode assignment)
        {
            GenerateAssignment(assignment);
            return;
        }

        if (statement is ComplexAssignmentNode complexAssignment)
        {
            GenerateComplexAssignment(complexAssignment);
            return;
        }

        // يولد تعليمة طباعة قيمة عددية أو نصية.
        if (statement is PrintNode print)
        {
            GeneratePrint(print);
            return;
        }

        if (statement is PrintListNode printList)
        {
            foreach (ExpressionNode expression in printList.Expressions)
            {
                GeneratePrint(new PrintNode(expression, printList.Span));
            }

            return;
        }

        if (statement is ReadNode read)
        {
            GenerateRead(read);
            return;
        }

        if (statement is ExpressionStatementNode { Expression: CallExpressionNode call })
        {
            GenerateCall(call);
            return;
        }

        // يولد الفروع والعلامات اللازمة لتعليمة إذا وإلا.
        if (statement is IfNode ifNode)
        {
            GenerateIf(ifNode);
            return;
        }

        // يولد العلامات والقفز الشرطي لتعليمة طالما.
        if (statement is WhileNode whileNode)
        {
            GenerateWhile(whileNode);
            return;
        }

        if (statement is RepeatToNode repeatTo)
        {
            GenerateRepeatTo(repeatTo);
            return;
        }

        if (statement is RepeatUntilNode repeatUntil)
        {
            GenerateRepeatUntil(repeatUntil);
            return;
        }

        // يولد الكتلة في نطاق محلي عند وصولها مباشرة.
        if (statement is BlockNode block)
        {
            GenerateBlock(block);
            return;
        }

        // يمنع تجاهل عقدة جديدة إن وسعت اللغة مستقبلاً.
        throw new MipsGenerationException("MIPS001", "تعليمة غير مدعومة في مولد MIPS.", statement.Span);
    }

    /// <summary>
    /// يحول تعريف متغير بيان إلى مساحة .word وتعليمة إسناد أولية.
    /// </summary>
    private void GenerateDeclaration(VarDeclarationNode declaration)
    {
        // يحدد النوع الداخلي للاسم العربي المعلن.
        LanguageType type = MapTypeName(declaration.TypeName, declaration.Span);

        // يقيد الإصدار الأول إلى الأعداد الصحيحة والمنطقيات، وهي ممثلة بكلمة MIPS.
        EnsureMipsStorageType(type, declaration.Span);

        // ينشئ تسمية ASCII آمنة بدلاً من استعمال الاسم العربي مباشرة في MIPS.
        string storageLabel = "var_" + _variableCounter++;
        // Keeps the Arabic declaration name for IR display while MIPS uses storageLabel.
        var variable = new IrValue(IrValueKind.Variable, storageLabel, type, declaration.Name);

        // يحفظ الربط بين اسم بيان والتسمية الداخلية ضمن النطاق الحالي.
        _scope.Define(declaration.Name, variable);

        // يحجز كلمة في قسم البيانات للمتغير.
        // Stores the Arabic name as display metadata without changing the MIPS label.
        _program.AddStorage(storageLabel, type, declaration.Name);

        // يختار صفراً أو خطأ عند عدم وجود قيمة ابتدائية.
        IrValue initialValue = declaration.Initializer is null
            ? CreateDefaultValue(type, declaration.Span)
            : GenerateExpression(declaration.Initializer);

        // يخزن القيمة الابتدائية داخل المتغير.
        _program.Emit(new IrInstruction(
            IrOpcode.Assign,
            variable,
            initialValue,
            null,
            null,
            null,
            declaration.Span));
    }

    /// <summary>
    /// Registers a procedure label and allocates static parameter storage for the supported integer backend subset.
    /// </summary>
    private void RegisterProcedure(ProcedureDeclarationNode procedure)
    {
        if (_procedures.ContainsKey(procedure.Name))
        {
            throw new MipsGenerationException("MIPS004", "الإجراء مكرر أثناء توليد MIPS: " + procedure.Name, procedure.Span);
        }

        var parameters = new List<ProcedureParameterBinding>();
        foreach (ParameterNode parameter in procedure.Parameters)
        {
            LanguageType type = MapOfficialType(parameter.Type, parameter.Span);
            EnsureMipsStorageType(type, parameter.Span);
            string storageLabel = "param_" + _variableCounter++;
            var storage = new IrValue(IrValueKind.Variable, storageLabel, type, parameter.Name);
            _program.AddStorage(storageLabel, type, parameter.Name);
            parameters.Add(new ProcedureParameterBinding(parameter.Name, storage, parameter.PassingMode));
        }

        _procedures.Add(procedure.Name, new ProcedureBinding(CreateLabel("proc"), parameters));
    }

    /// <summary>
    /// Lowers a procedure body into a labeled TAC region that returns with the Return opcode.
    /// </summary>
    private void GenerateProcedure(ProcedureDeclarationNode procedure)
    {
        ProcedureBinding binding = _procedures[procedure.Name];
        EmitLabel(binding.Label, procedure.Span);

        IrScope previous = _scope;
        _scope = new IrScope(previous);
        try
        {
            foreach (ProcedureParameterBinding parameter in binding.Parameters)
            {
                _scope.Define(parameter.Name, parameter.Storage);
            }

            foreach (DeclarationNode declaration in procedure.Body.Declarations)
            {
                GenerateOfficialDeclaration(declaration);
            }

            foreach (StatementNode statement in procedure.Body.Statements)
            {
                GenerateStatement(statement);
            }
        }
        finally
        {
            _scope = previous;
        }

        _program.Emit(new IrInstruction(IrOpcode.Return, null, null, null, null, null, procedure.Span));
    }

    /// <summary>
    /// Generates the executable subset of official declarations and rejects backend features not yet lowered.
    /// </summary>
    private void GenerateOfficialDeclaration(DeclarationNode declaration)
    {
        if (declaration is ConstantDeclarationNode constant)
        {
            _scope.Define(constant.Name, GenerateExpression(constant.Value));
            return;
        }

        if (declaration is VariableDeclarationGroupNode variables)
        {
            BackendTypeLayout layout = ResolveBackendLayout(variables.Type);
            LanguageType type = layout.Kind;
            foreach (string name in variables.Names)
            {
                GenerateOfficialVariable(name, layout, variables.Initializer, variables.Span);
            }

            return;
        }

        if (declaration is TypeDeclarationNode)
        {
            return;
        }

        throw new MipsGenerationException("MIPS004", "الإجراءات لم تُحوّل إلى TAC/MIPS بعد.", declaration.Span);
    }

    /// <summary>
    /// Allocates one official variable and emits its initialization assignment.
    /// </summary>
    private void GenerateOfficialVariable(string sourceName, BackendTypeLayout layout, ExpressionNode? initializer, SourceSpan span)
    {
        string storageLabel = "var_" + _variableCounter++;
        var variable = new IrValue(IrValueKind.Variable, storageLabel, layout.Kind, sourceName);
        _scope.Define(sourceName, variable);
        _storageLayouts[storageLabel] = layout;
        _program.AddStorage(storageLabel, layout.Kind, sourceName, layout.WordCount);
        if (layout.Kind != LanguageType.List && layout.Kind != LanguageType.Record)
        {
            IrValue initialValue;
            if (initializer is not null)
            {
                initialValue = GenerateExpression(initializer);
            }
            else if (layout.Kind == LanguageType.Real)
            {
                string zeroLabel = "float_" + _floatCounter++;
                _program.AddFloat(zeroLabel, "0.0");
                initialValue = new IrValue(IrValueKind.FloatLabel, zeroLabel, LanguageType.Real);
            }
            else
            {
                initialValue = CreateDefaultValue(layout.Kind, span);
            }

            _program.Emit(new IrInstruction(IrOpcode.Assign, variable, initialValue, null, null, null, span));
        }
        else if (initializer is not null)
        {
            throw new MipsGenerationException("MIPS004", "تهيئة القوائم والسجلات بتعبير مباشر لم تُحوّل إلى MIPS بعد.", initializer.Span);
        }
    }

    /// <summary>
    /// يحول إسناد بيان إلى Assign في IR.
    /// </summary>
    private void GenerateAssignment(AssignmentNode assignment)
    {
        // يبحث عن التسمية الداخلية للمتغير الذي فحصه Semantic Analyzer مسبقاً.
        IrValue variable = _scope.Lookup(assignment.Name, assignment.Span);

        // يولد القيمة الجديدة من التعبير في الجهة اليمنى.
        IrValue value = GenerateExpression(assignment.Expression);

        // يخزن القيمة في الموقع المرتبط بالمتغير.
        _program.Emit(new IrInstruction(
            IrOpcode.Assign,
            variable,
            value,
            null,
            null,
            null,
            assignment.Span));
    }

    /// <summary>
    /// Generates a scalar store into a direct list element or record field.
    /// </summary>
    private void GenerateComplexAssignment(ComplexAssignmentNode assignment)
    {
        CompositeLocation location = ResolveCompositeLocation(assignment.Target);
        IrValue value = GenerateExpression(assignment.Expression);
        if (location.Layout.Kind != value.Type)
        {
            throw new MipsGenerationException("MIPS002", "نوع القيمة لا يطابق عنصر القائمة أو حقل السجل أثناء توليد MIPS.", assignment.Expression.Span);
        }

        _program.Emit(new IrInstruction(IrOpcode.StoreIndexed, location.Base, location.WordIndex, value, null, null, assignment.Span));
    }

    /// <summary>
    /// Resolves a direct aggregate access into its storage base, word index, and scalar element layout.
    /// </summary>
    private CompositeLocation ResolveCompositeLocation(ExpressionNode target)
    {
        if (target is IndexedAccessNode indexed)
        {
            IrValue baseValue = GenerateAggregateBase(indexed.Collection);
            BackendTypeLayout layout = GetStorageLayout(baseValue, indexed.Collection.Span);
            if (layout.Kind != LanguageType.List || layout.ElementType is null || layout.ElementType.WordCount != 1)
            {
                throw new MipsGenerationException("MIPS004", "الفهرسة في MIPS الحالي تدعم قائمة بعناصر مفردة الكلمة فقط.", indexed.Span);
            }

            return new CompositeLocation(baseValue, GenerateExpression(indexed.Index), layout.ElementType);
        }

        if (target is FieldAccessNode field)
        {
            IrValue baseValue = GenerateAggregateBase(field.Record);
            BackendTypeLayout layout = GetStorageLayout(baseValue, field.Record.Span);
            if (layout.Kind != LanguageType.Record || layout.Fields is null || !layout.Fields.TryGetValue(field.FieldName, out BackendFieldLayout? fieldLayout) || fieldLayout.Layout.WordCount != 1)
            {
                throw new MipsGenerationException("MIPS004", "الوصول في MIPS الحالي يدعم حقلاً مفرد الكلمة من سجل مباشر فقط.", field.Span);
            }

            return new CompositeLocation(baseValue, new IrValue(IrValueKind.Immediate, fieldLayout.WordOffset.ToString(), LanguageType.Int), fieldLayout.Layout);
        }

        throw new MipsGenerationException("MIPS004", "هدف الوصول المركب غير مدعوم في MIPS الحالي.", target.Span);
    }

    private IrValue GenerateAggregateBase(ExpressionNode expression)
    {
        if (expression is IdentifierNode identifier)
        {
            return _scope.Lookup(identifier.Name, identifier.Span);
        }

        throw new MipsGenerationException("MIPS004", "الوصول المركب المتداخل لم يُحوّل إلى MIPS بعد.", expression.Span);
    }

    private BackendTypeLayout GetStorageLayout(IrValue value, SourceSpan span)
    {
        if (_storageLayouts.TryGetValue(value.Name, out BackendTypeLayout? layout))
        {
            return layout;
        }

        throw new MipsGenerationException("MIPS004", "لم يتم العثور على تخطيط تخزين مركب للاسم: " + value.DisplayName, span);
    }

    /// <summary>
    /// يحول أمر اطبع إلى PrintInt أو PrintString.
    /// </summary>
    private void GeneratePrint(PrintNode print)
    {
        // يولد القيمة المراد عرضها أولاً.
        IrValue value = GenerateExpression(print.Expression);

        // يطبع النصوص عبر syscall 4، ويطبع الأعداد والمنطقيات عبر syscall 1.
        IrOpcode opcode = value.Type switch
        {
            LanguageType.Text => IrOpcode.PrintString,
            LanguageType.Char => IrOpcode.PrintChar,
            LanguageType.Real => IrOpcode.PrintReal,
            _ => IrOpcode.PrintInt
        };

        // يضيف تعليمة الطباعة إلى البرنامج الوسيط.
        _program.Emit(new IrInstruction(
            opcode,
            null,
            value,
            null,
            null,
            null,
            print.Span));
    }

    /// <summary>
    /// Generates an integer input instruction for an identifier target in the initial official backend subset.
    /// </summary>
    private void GenerateRead(ReadNode read)
    {
        if (read.Target is not IdentifierNode identifier)
        {
            throw new MipsGenerationException("MIPS004", "الإدخال إلى الفهرس أو الحقل لم يُحوّل إلى MIPS بعد.", read.Span);
        }

        IrValue target = _scope.Lookup(identifier.Name, identifier.Span);
        if (target.Type == LanguageType.Text)
        {
            string bufferLabel = "buffer_" + _bufferCounter++;
            _program.AddBuffer(bufferLabel, 256);
            _program.Emit(new IrInstruction(IrOpcode.ReadString, target, null, null, null, bufferLabel, read.Span));
            return;
        }

        if (target.Type != LanguageType.Int && target.Type != LanguageType.Bool && target.Type != LanguageType.Char && target.Type != LanguageType.Real)
        {
            throw new MipsGenerationException("MIPS002", "تعليمة اقرأ تدعم صحيح ومنطقي فقط في مولد MIPS الحالي.", read.Span);
        }

        IrOpcode opcode = target.Type switch
        {
            LanguageType.Char => IrOpcode.ReadChar,
            LanguageType.Real => IrOpcode.ReadReal,
            _ => IrOpcode.ReadInt
        };
        _program.Emit(new IrInstruction(opcode, target, null, null, null, null, read.Span));
    }

    /// <summary>
    /// Generates a non-recursive procedure call with explicit value and reference parameter copies.
    /// </summary>
    private void GenerateCall(CallExpressionNode call)
    {
        if (!_procedures.TryGetValue(call.ProcedureName, out ProcedureBinding? procedure))
        {
            throw new MipsGenerationException("MIPS001", "لم يتم العثور على الإجراء أثناء توليد MIPS: " + call.ProcedureName, call.Span);
        }

        if (procedure.Parameters.Count != call.Arguments.Count)
        {
            throw new MipsGenerationException("MIPS004", "عدد معاملات الاستدعاء لا يطابق الإجراء: " + call.ProcedureName, call.Span);
        }

        var referenceCopies = new List<(IrValue Parameter, IrValue Caller)>();
        for (int index = 0; index < call.Arguments.Count; index++)
        {
            ProcedureParameterBinding parameter = procedure.Parameters[index];
            ExpressionNode argument = call.Arguments[index];
            IrValue value = GenerateExpression(argument);
            if (parameter.Storage.Type != value.Type)
            {
                throw new MipsGenerationException("MIPS002", "نوع معامل الإجراء غير مدعوم أو غير متطابق أثناء توليد MIPS.", argument.Span);
            }

            _program.Emit(new IrInstruction(IrOpcode.Assign, parameter.Storage, value, null, null, null, argument.Span));
            if (parameter.PassingMode == ParameterPassingMode.ByReference)
            {
                if (argument is not IdentifierNode identifier)
                {
                    throw new MipsGenerationException("MIPS004", "المعامل بالمرجع يحتاج معرفاً بسيطاً في مولد MIPS الحالي.", argument.Span);
                }

                referenceCopies.Add((parameter.Storage, _scope.Lookup(identifier.Name, identifier.Span)));
            }
        }

        _program.Emit(new IrInstruction(IrOpcode.Call, null, null, null, null, procedure.Label, call.Span));
        foreach ((IrValue parameter, IrValue caller) in referenceCopies)
        {
            _program.Emit(new IrInstruction(IrOpcode.Assign, caller, parameter, null, null, null, call.Span));
        }
    }

    /// <summary>
    /// يحول إذا وإلا إلى قفز شرطي وعلامات.
    /// </summary>
    private void GenerateIf(IfNode ifNode)
    {
        // يولد قيمة الشرط المنطقي.
        IrValue condition = GenerateExpression(ifNode.Condition);

        // ينشئ علامة الفرع البديل أو النهاية عندما لا يوجد وإلا.
        string elseLabel = CreateLabel("else");
        string endLabel = CreateLabel("endif");

        // يقفز إلى الفرع البديل إذا كانت قيمة الشرط صفراً.
        _program.Emit(new IrInstruction(
            IrOpcode.IfZeroGoto,
            null,
            condition,
            null,
            null,
            elseLabel,
            ifNode.Condition.Span));

        // يولد تعليمات كتلة إذا.
        GenerateBlock(ifNode.ThenBlock);

        // يتجاوز كتلة وإلا بعد تنفيذ كتلة إذا.
        if (ifNode.ElseBlock is not null)
        {
            _program.Emit(new IrInstruction(
                IrOpcode.Goto,
                null,
                null,
                null,
                null,
                endLabel,
                ifNode.Span));
        }

        // يضع علامة بداية وإلا أو نهاية إذا عند عدم وجود الفرع البديل.
        EmitLabel(elseLabel, ifNode.Span);

        // يولد كتلة وإلا إن وجدت.
        if (ifNode.ElseBlock is not null)
        {
            GenerateBlock(ifNode.ElseBlock);
            EmitLabel(endLabel, ifNode.Span);
        }
    }

    /// <summary>
    /// يحول طالما إلى علامة بداية وشرط وقف وقفزة رجوع.
    /// </summary>
    private void GenerateWhile(WhileNode whileNode)
    {
        // ينشئ علامتي بداية الحلقة ونهايتها.
        string startLabel = CreateLabel("while");
        string endLabel = CreateLabel("endwhile");

        // يضع بداية الحلقة قبل حساب الشرط كي يعاد تقييمه في كل دورة.
        EmitLabel(startLabel, whileNode.Span);

        // يولد قيمة الشرط الحالية.
        IrValue condition = GenerateExpression(whileNode.Condition);

        // يخرج من الحلقة عندما تكون نتيجة الشرط صفراً.
        _program.Emit(new IrInstruction(
            IrOpcode.IfZeroGoto,
            null,
            condition,
            null,
            null,
            endLabel,
            whileNode.Condition.Span));

        // يولد جسم الحلقة في نطاق فرعي.
        GenerateBlock(whileNode.Body);

        // يعيد التنفيذ إلى بداية الحلقة.
        _program.Emit(new IrInstruction(
            IrOpcode.Goto,
            null,
            null,
            null,
            null,
            startLabel,
            whileNode.Span));

        // يضع علامة الخروج من الحلقة.
        EmitLabel(endLabel, whileNode.Span);
    }

    /// <summary>
    /// Lowers the official counted repeat loop into initialization, comparison, increment, and branch TAC.
    /// </summary>
    private void GenerateRepeatTo(RepeatToNode repeatTo)
    {
        IrValue iterator = _scope.Lookup(repeatTo.IteratorName, repeatTo.Span);
        IrValue startValue = GenerateExpression(repeatTo.Start);
        _program.Emit(new IrInstruction(IrOpcode.Assign, iterator, startValue, null, null, null, repeatTo.Start.Span));

        string startLabel = CreateLabel("repeat");
        string endLabel = CreateLabel("endrepeat");
        EmitLabel(startLabel, repeatTo.Span);

        IrValue endValue = GenerateExpression(repeatTo.End);
        IrValue condition = CreateTemporary(LanguageType.Bool);
        _program.Emit(new IrInstruction(IrOpcode.Binary, condition, iterator, endValue, TokenType.LessEqual, null, repeatTo.Span));
        _program.Emit(new IrInstruction(IrOpcode.IfZeroGoto, null, condition, null, null, endLabel, repeatTo.Span));

        GenerateBlock(repeatTo.Body);

        IrValue stepValue = repeatTo.Step is null
            ? new IrValue(IrValueKind.Immediate, "1", LanguageType.Int)
            : GenerateExpression(repeatTo.Step);
        IrValue nextValue = CreateTemporary(LanguageType.Int);
        _program.Emit(new IrInstruction(IrOpcode.Binary, nextValue, iterator, stepValue, TokenType.Plus, null, repeatTo.Span));
        _program.Emit(new IrInstruction(IrOpcode.Assign, iterator, nextValue, null, null, null, repeatTo.Span));
        _program.Emit(new IrInstruction(IrOpcode.Goto, null, null, null, null, startLabel, repeatTo.Span));
        EmitLabel(endLabel, repeatTo.Span);
    }

    /// <summary>
    /// Lowers the official post-test loop into a body followed by a conditional back edge.
    /// </summary>
    private void GenerateRepeatUntil(RepeatUntilNode repeatUntil)
    {
        string startLabel = CreateLabel("repeatuntil");
        EmitLabel(startLabel, repeatUntil.Span);
        GenerateBlock(repeatUntil.Body);
        IrValue condition = GenerateExpression(repeatUntil.Condition);
        _program.Emit(new IrInstruction(IrOpcode.IfZeroGoto, null, condition, null, null, startLabel, repeatUntil.Span));
    }

    /// <summary>
    /// يولد كتلة تعليمات ضمن نطاق أسماء فرعي.
    /// </summary>
    private void GenerateBlock(BlockNode block)
    {
        // يحتفظ بالنطاق الخارجي للعودة إليه عند انتهاء الكتلة.
        IrScope previous = _scope;
        _scope = new IrScope(previous);

        try
        {
            foreach (DeclarationNode declaration in block.Declarations)
            {
                GenerateOfficialDeclaration(declaration);
            }

            // يولد جميع تعليمات الكتلة بالترتيب.
            foreach (StatementNode statement in block.Statements)
            {
                GenerateStatement(statement);
            }
        }
        finally
        {
            // يعيد النطاق السابق حتى لا تتسرب تعريفات الكتلة.
            _scope = previous;
        }
    }

    /// <summary>
    /// يحول تعبير AST إلى قيمة IR مباشرة أو مؤقتة.
    /// </summary>
    private IrValue GenerateExpression(ExpressionNode expression)
    {
        // يحول الثوابت إلى أعداد مباشرة أو عناوين نصوص.
        if (expression is LiteralNode literal)
        {
            return GenerateLiteral(literal);
        }

        // يحول الاسم العربي إلى المتغير الداخلي المرتبط به.
        if (expression is IdentifierNode identifier)
        {
            return _scope.Lookup(identifier.Name, identifier.Span);
        }

        if (expression is IndexedAccessNode indexed)
        {
            CompositeLocation location = ResolveCompositeLocation(indexed);
            IrValue result = CreateTemporary(location.Layout.Kind);
            _program.Emit(new IrInstruction(IrOpcode.LoadIndexed, result, location.Base, location.WordIndex, null, null, indexed.Span));
            return result;
        }

        if (expression is FieldAccessNode field)
        {
            CompositeLocation location = ResolveCompositeLocation(field);
            IrValue result = CreateTemporary(location.Layout.Kind);
            _program.Emit(new IrInstruction(IrOpcode.LoadIndexed, result, location.Base, location.WordIndex, null, null, field.Span));
            return result;
        }

        // لا يحتاج القوس إلى تعليمة؛ يعيد التعبير الداخلي.
        if (expression is GroupingNode grouping)
        {
            return GenerateExpression(grouping.Expression);
        }

        // يولد تعليمة أحادية ونتيجة مؤقتة.
        if (expression is UnaryExpressionNode unary)
        {
            return GenerateUnary(unary);
        }

        // يولد تعليمة ثنائية ونتيجة مؤقتة.
        if (expression is BinaryExpressionNode binary)
        {
            return GenerateBinary(binary);
        }

        // يمنع تجاهل نوع تعبير غير مغطى.
        throw new MipsGenerationException("MIPS001", "تعبير غير مدعوم في مولد MIPS.", expression.Span);
    }

    /// <summary>
    /// يولد قيمة IR لعدد أو منطقي أو نص.
    /// </summary>
    private IrValue GenerateLiteral(LiteralNode literal)
    {
        // يحول العدد الصحيح إلى قيمة مباشرة.
        if (literal.LiteralType == TokenType.Integer)
        {
            return new IrValue(IrValueKind.Immediate, literal.Value, LanguageType.Int);
        }

        // يمثل صحيح وخطأ بالأعداد 1 و0 في MIPS.
        if (literal.LiteralType == TokenType.True)
        {
            return new IrValue(IrValueKind.Immediate, "1", LanguageType.Bool);
        }

        if (literal.LiteralType == TokenType.False)
        {
            return new IrValue(IrValueKind.Immediate, "0", LanguageType.Bool);
        }

        // يخزن النص في قسم البيانات تحت علامة ASCII فريدة.
        if (literal.LiteralType == TokenType.String)
        {
            string label = "str_" + _stringCounter++;
            _program.AddString(label, literal.Value);
            return new IrValue(IrValueKind.StringLabel, label, LanguageType.Text);
        }

        if (literal.LiteralType == TokenType.Character)
        {
            return new IrValue(IrValueKind.Immediate, ((int)literal.Value[0]).ToString(), LanguageType.Char);
        }

        if (literal.LiteralType == TokenType.Real)
        {
            string label = "float_" + _floatCounter++;
            _program.AddFloat(label, literal.Value);
            return new IrValue(IrValueKind.FloatLabel, label, LanguageType.Real);
        }

        // يرفع خطأ عند أي ثابت آخر غير متوقع.
        throw new MipsGenerationException("MIPS001", "ثابت غير مدعوم في مولد MIPS.", literal.Span);
    }

    /// <summary>
    /// يولد تعليمة أحادية إلى قيمة مؤقتة.
    /// </summary>
    private IrValue GenerateUnary(UnaryExpressionNode unary)
    {
        // يولد القيمة التي يعمل عليها العامل الأحادي.
        IrValue operand = GenerateExpression(unary.Operand);

        // يحدد نوع النتيجة حسب نوع العامل.
        LanguageType resultType = unary.Operator == TokenType.Bang ? LanguageType.Bool : operand.Type;

        // يحجز قيمة مؤقتة لحفظ النتيجة.
        IrValue result = CreateTemporary(resultType);

        // يضيف التعليمة الأحادية إلى IR.
        _program.Emit(new IrInstruction(
            IrOpcode.Unary,
            result,
            operand,
            null,
            unary.Operator,
            null,
            unary.Span));

        return result;
    }

    /// <summary>
    /// يولد تعليمة ثنائية إلى قيمة مؤقتة.
    /// </summary>
    private IrValue GenerateBinary(BinaryExpressionNode binary)
    {
        // يولد الطرفين أولاً احتراماً لترتيب التعبير.
        IrValue left = GenerateExpression(binary.Left);
        IrValue right = GenerateExpression(binary.Right);

        // يمنع دمج النصوص في إصدار MIPS الأول لأنه يحتاج إجراءات نسخ ذاكرة مستقلة.
        if (left.Type == LanguageType.Text || right.Type == LanguageType.Text)
        {
            throw new MipsGenerationException("MIPS003", "الإصدار الأول من مولد MIPS لا يدعم العمليات على النصوص؛ يسمح بطباعة النص الثابت فقط.", binary.Span);
        }

        if (binary.Operator == TokenType.Caret)
        {
            if (right.Kind != IrValueKind.Immediate || !int.TryParse(right.Name, out int exponent) || exponent < 0)
            {
                throw new MipsGenerationException("MIPS004", "عامل الأس ^ يدعم حالياً أساً صحيحاً ثابتاً غير سالب فقط.", binary.Span);
            }
        }

        // Keeps real arithmetic in FPU storage while comparisons still produce integer booleans.
        LanguageType resultType = IsBooleanOperator(binary.Operator)
            ? LanguageType.Bool
            : left.Type == LanguageType.Real || right.Type == LanguageType.Real
                ? LanguageType.Real
                : LanguageType.Int;

        // يحجز موقعاً مؤقتاً لتخزين نتيجة العملية.
        IrValue result = CreateTemporary(resultType);

        // يضيف عملية Three-Address Code المقابلة.
        _program.Emit(new IrInstruction(
            IrOpcode.Binary,
            result,
            left,
            right,
            binary.Operator,
            null,
            binary.Span));

        return result;
    }

    /// <summary>
    /// ينشئ قيمة مؤقتة ويضيف مساحة كلمة لها في قسم البيانات.
    /// </summary>
    private IrValue CreateTemporary(LanguageType type)
    {
        // يولد اسماً ASCII فريداً مثل temp_0.
        int temporaryIndex = _temporaryCounter++;
        string name = "temp_" + temporaryIndex;
        // Gives the IR viewer an Arabic temporary name while MIPS uses the ASCII name.
        var temporary = new IrValue(IrValueKind.Temporary, name, type, "مؤقت_" + temporaryIndex);

        // يحجز مساحة الذاكرة التي ستخزن فيها قيمة النتيجة.
        // Persists the Arabic display name for the IR storage section.
        _program.AddStorage(name, type, temporary.SourceName);
        return temporary;
    }

    /// <summary>
    /// ينشئ قيمة افتراضية لمتغير معلن من دون قيمة ابتدائية.
    /// </summary>
    private static IrValue CreateDefaultValue(LanguageType type, SourceSpan span)
    {
        // يعيد صفراً للأعداد وخطأ للمنطقيات.
        if (type == LanguageType.Int)
        {
            return new IrValue(IrValueKind.Immediate, "0", LanguageType.Int);
        }

        if (type == LanguageType.Bool)
        {
            return new IrValue(IrValueKind.Immediate, "0", LanguageType.Bool);
        }

        if (type == LanguageType.Char)
        {
            return new IrValue(IrValueKind.Immediate, "0", LanguageType.Char);
        }

        if (type == LanguageType.Text)
        {
            return new IrValue(IrValueKind.Immediate, "0", LanguageType.Text);
        }

        // يمنع الأنواع التي لم يصمم لها تمثيل ذاكرة في MIPS الأولي.
        throw new MipsGenerationException("MIPS002", "نوع غير مدعوم في مولد MIPS.", span);
    }

    /// <summary>
    /// يحول اسم نوع بيان إلى النوع الداخلي المقابل.
    /// </summary>
    private static LanguageType MapTypeName(string typeName, SourceSpan span)
    {
        // يطابق الأنواع العربية المعلنة في مواصفات اللغة.
        return typeName switch
        {
            "عدد" => LanguageType.Int,
            "منطقي" => LanguageType.Bool,
            "حقيقي" => LanguageType.Real,
            "نص" => LanguageType.Text,
            _ => throw new MipsGenerationException("MIPS001", "نوع غير معروف في مولد MIPS: " + typeName, span)
        };
    }

    /// <summary>
    /// Maps a supported official type node to the storage type used by the current MIPS backend.
    /// </summary>
    private LanguageType MapOfficialType(TypeSyntaxNode type, SourceSpan span)
    {
        return ResolveBackendLayout(type).Kind;
    }

    /// <summary>
    /// Resolves official type syntax into a word-level storage layout for the MIPS backend.
    /// </summary>
    private BackendTypeLayout ResolveBackendLayout(TypeSyntaxNode type)
    {
        if (type is NamedTypeSyntaxNode named)
        {
            return named.TypeToken switch
            {
                TokenType.TypeInt => new BackendTypeLayout(LanguageType.Int, 1),
                TokenType.TypeBool => new BackendTypeLayout(LanguageType.Bool, 1),
                TokenType.TypeChar => new BackendTypeLayout(LanguageType.Char, 1),
                TokenType.TypeString => new BackendTypeLayout(LanguageType.Text, 1),
                TokenType.TypeReal => new BackendTypeLayout(LanguageType.Real, 1),
                TokenType.Identifier when _namedTypeLayouts.TryGetValue(named.Name, out BackendTypeLayout? layout) => layout,
                _ => throw new MipsGenerationException("MIPS002", "نوع رسمي غير مدعوم بعد في مولد MIPS: " + named.Name, type.Span)
            };
        }

        if (type is ListTypeSyntaxNode list)
        {
            if (list.Size is not LiteralNode { LiteralType: TokenType.Integer } sizeLiteral || !int.TryParse(sizeLiteral.Value, out int length) || length <= 0)
            {
                throw new MipsGenerationException("MIPS004", "حجم القائمة في backend MIPS يجب أن يكون ثابتاً موجباً.", list.Size.Span);
            }

            BackendTypeLayout element = ResolveBackendLayout(list.ElementType);
            return new BackendTypeLayout(LanguageType.List, length * element.WordCount, element);
        }

        if (type is RecordTypeSyntaxNode record)
        {
            var fields = new Dictionary<string, BackendFieldLayout>(StringComparer.Ordinal);
            int offset = 0;
            foreach (FieldDeclarationNode field in record.Fields)
            {
                BackendTypeLayout fieldLayout = ResolveBackendLayout(field.Type);
                if (!fields.TryAdd(field.Name, new BackendFieldLayout(offset, fieldLayout)))
                {
                    throw new MipsGenerationException("MIPS004", "الحقل مكرر في سجل backend: " + field.Name, field.Span);
                }

                offset += fieldLayout.WordCount;
            }

            return new BackendTypeLayout(LanguageType.Record, offset, null, fields);
        }

        throw new MipsGenerationException("MIPS002", "نوع رسمي غير مدعوم بعد في مولد MIPS.", type.Span);
    }

    /// <summary>
    /// يضمن أن المتغير القابل للتخزين يستخدم نوعاً مدعوماً في الإصدار الأول.
    /// </summary>
    private static void EnsureMipsStorageType(LanguageType type, SourceSpan span)
    {
        // يسمح بكلمة 32-بت للعدد والمنطقي فقط.
        if (type != LanguageType.Int && type != LanguageType.Bool && type != LanguageType.Char && type != LanguageType.Text && type != LanguageType.Real)
        {
            throw new MipsGenerationException("MIPS002", "الإصدار الأول من مولد MIPS يدعم المتغيرات من النوع عدد أو منطقي فقط.", span);
        }
    }

    /// <summary>
    /// يحدد إن كان العامل يعيد قيمة منطقية.
    /// </summary>
    private static bool IsBooleanOperator(TokenType operation)
    {
        // تشمل المقارنات والعمليات المنطقية.
        return operation is TokenType.Less or TokenType.LessEqual or TokenType.Greater or TokenType.GreaterEqual
            or TokenType.EqualEqual or TokenType.BangEqual or TokenType.AndAnd or TokenType.OrOr;
    }

    /// <summary>
    /// ينشئ علامة MIPS ASCII فريدة ومقروءة.
    /// </summary>
    private string CreateLabel(string prefix)
    {
        // ينتج أسماء مثل else_0 وendwhile_4.
        return prefix + "_" + _labelCounter++;
    }

    /// <summary>
    /// يضيف علامة كتعليمة IR مستقلة.
    /// </summary>
    private void EmitLabel(string label, SourceSpan span)
    {
        // يحفظ العلامة كي يحولها مولد MIPS إلى label:.
        _program.Emit(new IrInstruction(
            IrOpcode.Label,
            null,
            null,
            null,
            null,
            label,
            span));
    }

    /// <summary>
    /// Holds one generated procedure label and its static parameter storage bindings.
    /// </summary>
    private sealed record ProcedureBinding(string Label, IReadOnlyList<ProcedureParameterBinding> Parameters);

    /// <summary>
    /// Holds the backend storage assigned to one formal procedure parameter.
    /// </summary>
    private sealed record ProcedureParameterBinding(string Name, IrValue Storage, ParameterPassingMode PassingMode);

    /// <summary>
    /// Represents one direct aggregate word location used by indexed load and store TAC instructions.
    /// </summary>
    private sealed record CompositeLocation(IrValue Base, IrValue WordIndex, BackendTypeLayout Layout);
}

/// <summary>
/// يمثل خطأ توليد خاصاً بالقيود أو العقد غير المدعومة في MIPS.
/// </summary>
public sealed class MipsGenerationException : Exception
{
    /// <summary>
    /// ينشئ خطأ توليد مرتبطاً بموقع في برنامج بيان.
    /// </summary>
    public MipsGenerationException(string code, string message, SourceSpan span)
        : base(message)
    {
        // يحفظ رمز الخطأ للواجهة.
        Code = code;

        // يحفظ موقع الخطأ لإيضاح مكانه للمستخدم.
        Span = span;
    }

    /// <summary>
    /// رمز خطأ التوليد.
    /// </summary>
    public string Code { get; }

    /// <summary>
    /// موقع العبارة أو التعبير المخالف.
    /// </summary>
    public SourceSpan Span { get; }

    /// <summary>
    /// يعرض التشخيص بصيغة مشابهة لتشخيصات مراحل التحليل الأخرى.
    /// </summary>
    public override string ToString()
    {
        // يربط رمز MIPS والموقع برسالة عربية واضحة.
        return "[" + Code + "] " + Span + ": " + Message;
    }
}

/// <summary>
/// يمثل نطاق أسماء داخلياً أثناء تحويل AST إلى تسميات IR.
/// </summary>
internal sealed class IrScope
{
    // يحتفظ بالأسماء المحلية والتسميات المولدة لها.
    private readonly Dictionary<string, IrValue> _values = new Dictionary<string, IrValue>(StringComparer.Ordinal);

    // يشير إلى النطاق الخارجي للكتل المتداخلة.
    private readonly IrScope? _parent;

    /// <summary>
    /// ينشئ نطاقاً يمكنه الرجوع إلى نطاق أب اختياري.
    /// </summary>
    public IrScope(IrScope? parent)
    {
        // يحفظ النطاق الأب لعمليات البحث المتداخلة.
        _parent = parent;
    }

    /// <summary>
    /// يسجل الاسم العربي وتسمية MIPS الداخلية في النطاق الحالي.
    /// </summary>
    public void Define(string sourceName, IrValue value)
    {
        // يضيف الربط؛ تكرار التعريف تم منعه سابقاً في Semantic Analyzer.
        _values[sourceName] = value;
    }

    /// <summary>
    /// يبحث عن التسمية الداخلية لاسم عربي عبر النطاقات المتداخلة.
    /// </summary>
    public IrValue Lookup(string sourceName, SourceSpan span)
    {
        // يعيد القيمة من النطاق الحالي فوراً عند وجودها.
        if (_values.TryGetValue(sourceName, out IrValue? value))
        {
            return value;
        }

        // يبحث في النطاق الخارجي إن وجدت كتلة حاوية.
        if (_parent is not null)
        {
            return _parent.Lookup(sourceName, span);
        }

        // لا يفترض الوصول إلى هذه الحالة بعد نجاح Semantic Analyzer.
        throw new MipsGenerationException("MIPS001", "لم يتم العثور على المتغير أثناء توليد MIPS: " + sourceName, span);
    }
}
