using System.Globalization;
using System.Text;
using BayanCompiler.Frontend;
using BayanCompiler.Intermediate;
using BayanCompiler.Lexing;
using BayanCompiler.Semantics;

namespace BayanCompiler.CodeGeneration;

/// <summary>
/// Lowers the shared Three-Address Code program into Common Intermediate Language accepted by ILAsm.
/// </summary>
public sealed class CilExecutableGenerator
{
    /// <summary>
    /// Creates a standalone CIL console program from the supported official IR subset.
    /// </summary>
    public CilGenerationResult Generate(IrProgram program)
    {
        ValidateStorage(program);
        Dictionary<string, int> locals = CreateLocalMap(program);
        Dictionary<string, string> labels = CreateLabelMap(program);
        ValidateBranchTargets(program, labels);

        var builder = new StringBuilder();
        EmitPreamble(builder);
        EmitMainHeader(builder, program, locals);

        foreach (IrInstruction instruction in program.Instructions)
        {
            EmitInstruction(builder, instruction, program, locals, labels);
        }

        builder.AppendLine("        ret");
        builder.AppendLine("    }");
        builder.AppendLine("}");
        return new CilGenerationResult(builder.ToString());
    }

    /// <summary>
    /// Rejects storage models that require a CIL runtime representation not implemented in this backend version.
    /// </summary>
    private static void ValidateStorage(IrProgram program)
    {
        foreach ((string name, LanguageType type) in program.Storage)
        {
            int words = program.StorageWordCounts.TryGetValue(name, out int count) ? count : 1;
            if (words != 1 || type is not (LanguageType.Int or LanguageType.Bool or LanguageType.Char))
            {
                throw new CilGenerationException(
                    "CIL001",
                    "هدف Windows EXE يدعم حالياً التخزين المفرد من نوع صحيح أو منطقي أو حرفي فقط: " + name + ".",
                    new SourceSpan(0, 0, 1, 1));
            }
        }
    }

    /// <summary>
    /// Assigns stable CIL local slots to compiler-owned storage names.
    /// </summary>
    private static Dictionary<string, int> CreateLocalMap(IrProgram program)
    {
        var result = new Dictionary<string, int>(StringComparer.Ordinal);
        int index = 0;
        foreach (string name in program.Storage.Keys)
        {
            result.Add(name, index++);
        }

        return result;
    }

    /// <summary>
    /// Converts every TAC label to an IL-safe generated label rather than reusing source-adjacent names.
    /// </summary>
    private static Dictionary<string, string> CreateLabelMap(IrProgram program)
    {
        var result = new Dictionary<string, string>(StringComparer.Ordinal);
        int index = 0;
        foreach (IrInstruction instruction in program.Instructions)
        {
            if (string.IsNullOrWhiteSpace(instruction.TargetLabel) || result.ContainsKey(instruction.TargetLabel))
            {
                continue;
            }

            result.Add(instruction.TargetLabel, "IL_BAYAN_" + index++.ToString("D4", CultureInfo.InvariantCulture));
        }

        return result;
    }

    /// <summary>
    /// Ensures every branch target is backed by a real TAC label before CIL is written.
    /// </summary>
    private static void ValidateBranchTargets(IrProgram program, IReadOnlyDictionary<string, string> labels)
    {
        var declaredLabels = new HashSet<string>(
            program.Instructions
                .Where(item => item.Opcode == IrOpcode.Label && !string.IsNullOrWhiteSpace(item.TargetLabel))
                .Select(item => item.TargetLabel!),
            StringComparer.Ordinal);

        foreach (IrInstruction instruction in program.Instructions)
        {
            if (instruction.Opcode is not (IrOpcode.Goto or IrOpcode.IfZeroGoto))
            {
                continue;
            }

            if (string.IsNullOrWhiteSpace(instruction.TargetLabel) ||
                !labels.ContainsKey(instruction.TargetLabel) ||
                !declaredLabels.Contains(instruction.TargetLabel))
            {
                throw new CilGenerationException("CIL002", "تعليمة CIL تحتوي قفزاً إلى علامة غير معرفة.", instruction.Span);
            }
        }
    }

    /// <summary>
    /// Emits the assembly metadata required by ILAsm for a managed console executable.
    /// </summary>
    private static void EmitPreamble(StringBuilder builder)
    {
        builder.AppendLine(".assembly extern mscorlib {}");
        builder.AppendLine(".assembly Bayan.Generated {}");
        builder.AppendLine(".module Bayan.Generated.exe");
        builder.AppendLine();
        builder.AppendLine(".class public auto ansi sealed beforefieldinit BayanProgram");
        builder.AppendLine("       extends [mscorlib]System.Object");
        builder.AppendLine("{");
    }

    /// <summary>
    /// Emits Main and its local declarations from the compiler's actual TAC storage map.
    /// </summary>
    private static void EmitMainHeader(StringBuilder builder, IrProgram program, IReadOnlyDictionary<string, int> locals)
    {
        builder.AppendLine("    .method public static void Main() cil managed");
        builder.AppendLine("    {");
        builder.AppendLine("        .entrypoint");
        builder.AppendLine("        .maxstack 8");

        if (locals.Count > 0)
        {
            builder.AppendLine("        .locals init (");
            foreach ((string name, int index) in locals.OrderBy(item => item.Value))
            {
                string separator = index == locals.Count - 1 ? string.Empty : ",";
                string sourceName = program.GetStorageSourceName(name) ?? name;
                builder.Append("            [")
                    .Append(index)
                    .Append("] int32 local_")
                    .Append(index)
                    .Append(separator)
                    .Append(" // ")
                    .Append(EscapeComment(sourceName))
                    .AppendLine();
            }

            builder.AppendLine("        )");
        }
    }

    /// <summary>
    /// Emits a single TAC instruction into equivalent supported CIL instructions.
    /// </summary>
    private static void EmitInstruction(
        StringBuilder builder,
        IrInstruction instruction,
        IrProgram program,
        IReadOnlyDictionary<string, int> locals,
        IReadOnlyDictionary<string, string> labels)
    {
        switch (instruction.Opcode)
        {
            case IrOpcode.Assign:
                EmitLoadValue(builder, instruction.Left!, locals, instruction.Span);
                EmitStoreValue(builder, instruction.Result!, locals, instruction.Span);
                return;

            case IrOpcode.Binary:
                EmitBinary(builder, instruction, locals);
                return;

            case IrOpcode.Unary:
                EmitUnary(builder, instruction, locals);
                return;

            case IrOpcode.Label:
                builder.AppendLine(labels[instruction.TargetLabel!] + ":");
                return;

            case IrOpcode.Goto:
                builder.AppendLine("        br " + labels[instruction.TargetLabel!]);
                return;

            case IrOpcode.IfZeroGoto:
                EmitLoadValue(builder, instruction.Left!, locals, instruction.Span);
                builder.AppendLine("        brfalse " + labels[instruction.TargetLabel!]);
                return;

            case IrOpcode.PrintInt:
                EmitLoadValue(builder, instruction.Left!, locals, instruction.Span);
                builder.AppendLine("        call void [mscorlib]System.Console::WriteLine(int32)");
                return;

            case IrOpcode.PrintChar:
                EmitLoadValue(builder, instruction.Left!, locals, instruction.Span);
                builder.AppendLine("        conv.u2");
                builder.AppendLine("        call void [mscorlib]System.Console::WriteLine(char)");
                return;

            case IrOpcode.PrintString:
                EmitPrintString(builder, instruction.Left!, program, locals, instruction.Span);
                return;

            case IrOpcode.ReadInt:
                builder.AppendLine("        call string [mscorlib]System.Console::ReadLine()");
                builder.AppendLine("        call int32 [mscorlib]System.Int32::Parse(string)");
                EmitStoreValue(builder, instruction.Result!, locals, instruction.Span);
                return;

            default:
                throw new CilGenerationException(
                    "CIL001",
                    "تعليمة TAC غير مدعومة بعد في هدف Windows EXE: " + instruction.Opcode + ".",
                    instruction.Span);
        }
    }

    /// <summary>
    /// Emits an integer or Boolean binary operation and stores its calculated result.
    /// </summary>
    private static void EmitBinary(StringBuilder builder, IrInstruction instruction, IReadOnlyDictionary<string, int> locals)
    {
        if (instruction.Left!.Type == LanguageType.Real || instruction.Right!.Type == LanguageType.Real)
        {
            throw new CilGenerationException("CIL001", "العمليات الحقيقية غير مدعومة بعد في هدف Windows EXE.", instruction.Span);
        }

        EmitLoadValue(builder, instruction.Left, locals, instruction.Span);
        EmitLoadValue(builder, instruction.Right!, locals, instruction.Span);
        switch (instruction.Operator)
        {
            case TokenType.Plus:
                builder.AppendLine("        add");
                break;
            case TokenType.Minus:
                builder.AppendLine("        sub");
                break;
            case TokenType.Star:
                builder.AppendLine("        mul");
                break;
            case TokenType.Slash:
            case TokenType.Backslash:
                builder.AppendLine("        div");
                break;
            case TokenType.Percent:
                builder.AppendLine("        rem");
                break;
            case TokenType.Less:
                builder.AppendLine("        clt");
                break;
            case TokenType.Greater:
                builder.AppendLine("        cgt");
                break;
            case TokenType.LessEqual:
                builder.AppendLine("        cgt");
                EmitBooleanInvert(builder);
                break;
            case TokenType.GreaterEqual:
                builder.AppendLine("        clt");
                EmitBooleanInvert(builder);
                break;
            case TokenType.EqualEqual:
                builder.AppendLine("        ceq");
                break;
            case TokenType.BangEqual:
                builder.AppendLine("        ceq");
                EmitBooleanInvert(builder);
                break;
            case TokenType.AndAnd:
                builder.AppendLine("        and");
                break;
            case TokenType.OrOr:
                builder.AppendLine("        or");
                break;
            default:
                throw new CilGenerationException("CIL001", "عامل ثنائي غير مدعوم في هدف Windows EXE: " + instruction.Operator + ".", instruction.Span);
        }

        EmitStoreValue(builder, instruction.Result!, locals, instruction.Span);
    }

    /// <summary>
    /// Emits supported unary arithmetic and Boolean operations.
    /// </summary>
    private static void EmitUnary(StringBuilder builder, IrInstruction instruction, IReadOnlyDictionary<string, int> locals)
    {
        EmitLoadValue(builder, instruction.Left!, locals, instruction.Span);
        if (instruction.Operator == TokenType.Minus)
        {
            builder.AppendLine("        neg");
        }
        else if (instruction.Operator == TokenType.Bang)
        {
            builder.AppendLine("        ldc.i4.0");
            builder.AppendLine("        ceq");
        }
        else
        {
            throw new CilGenerationException("CIL001", "عامل أحادي غير مدعوم في هدف Windows EXE: " + instruction.Operator + ".", instruction.Span);
        }

        EmitStoreValue(builder, instruction.Result!, locals, instruction.Span);
    }

    /// <summary>
    /// Loads a literal string from the real IR string table and prints it through the console runtime.
    /// </summary>
    private static void EmitPrintString(
        StringBuilder builder,
        IrValue value,
        IrProgram program,
        IReadOnlyDictionary<string, int> locals,
        SourceSpan span)
    {
        if (value.Kind == IrValueKind.StringLabel && program.Strings.TryGetValue(value.Name, out string? text))
        {
            builder.AppendLine("        ldstr \"" + EscapeIlString(text) + "\"");
        }
        else if (value.Type == LanguageType.Text)
        {
            throw new CilGenerationException("CIL001", "طباعة متغير خيط رمزي غير مدعومة بعد في هدف Windows EXE.", span);
        }
        else
        {
            EmitLoadValue(builder, value, locals, span);
            builder.AppendLine("        box [mscorlib]System.Int32");
            builder.AppendLine("        call string [mscorlib]System.Convert::ToString(object)");
        }

        builder.AppendLine("        call void [mscorlib]System.Console::WriteLine(string)");
    }

    /// <summary>
    /// Loads an immediate scalar or a previously declared CIL local.
    /// </summary>
    private static void EmitLoadValue(StringBuilder builder, IrValue value, IReadOnlyDictionary<string, int> locals, SourceSpan span)
    {
        if (value.Kind == IrValueKind.Immediate)
        {
            if (!int.TryParse(value.Name, NumberStyles.Integer, CultureInfo.InvariantCulture, out int immediate))
            {
                throw new CilGenerationException("CIL003", "قيمة فورية غير صحيحة في TAC: " + value.Name + ".", span);
            }

            EmitLoadInt32(builder, immediate);
            return;
        }

        if (value.Kind is IrValueKind.Variable or IrValueKind.Temporary)
        {
            builder.AppendLine("        ldloc " + GetLocalSlot(value, locals, span).ToString(CultureInfo.InvariantCulture));
            return;
        }

        throw new CilGenerationException("CIL001", "نوع قيمة غير مدعوم في هدف Windows EXE: " + value.Kind + ".", span);
    }

    /// <summary>
    /// Stores the evaluation stack value in a declared CIL local.
    /// </summary>
    private static void EmitStoreValue(StringBuilder builder, IrValue value, IReadOnlyDictionary<string, int> locals, SourceSpan span)
    {
        if (value.Kind is not (IrValueKind.Variable or IrValueKind.Temporary))
        {
            throw new CilGenerationException("CIL003", "نتيجة TAC لا تشير إلى موقع تخزين قابل للكتابة.", span);
        }

        builder.AppendLine("        stloc " + GetLocalSlot(value, locals, span).ToString(CultureInfo.InvariantCulture));
    }

    /// <summary>
    /// Resolves one compiler storage value to its previously generated local slot.
    /// </summary>
    private static int GetLocalSlot(IrValue value, IReadOnlyDictionary<string, int> locals, SourceSpan span)
    {
        if (!locals.TryGetValue(value.Name, out int slot))
        {
            throw new CilGenerationException("CIL003", "تعليمة TAC تشير إلى تخزين غير معروف: " + value.Name + ".", span);
        }

        return slot;
    }

    /// <summary>
    /// Emits a CIL Boolean inversion for values represented as zero or one.
    /// </summary>
    private static void EmitBooleanInvert(StringBuilder builder)
    {
        builder.AppendLine("        ldc.i4.0");
        builder.AppendLine("        ceq");
    }

    /// <summary>
    /// Emits a compact integer literal instruction when possible.
    /// </summary>
    private static void EmitLoadInt32(StringBuilder builder, int value)
    {
        if (value == -1)
        {
            builder.AppendLine("        ldc.i4.m1");
        }
        else if (value is >= 0 and <= 8)
        {
            builder.AppendLine("        ldc.i4." + value.ToString(CultureInfo.InvariantCulture));
        }
        else if (value is >= sbyte.MinValue and <= sbyte.MaxValue)
        {
            builder.AppendLine("        ldc.i4.s " + value.ToString(CultureInfo.InvariantCulture));
        }
        else
        {
            builder.AppendLine("        ldc.i4 " + value.ToString(CultureInfo.InvariantCulture));
        }
    }

    /// <summary>
    /// Escapes source text for a quoted CIL string literal while retaining its Unicode content.
    /// </summary>
    private static string EscapeIlString(string value)
    {
        return value
            .Replace("\\", "\\\\", StringComparison.Ordinal)
            .Replace("\"", "\\\"", StringComparison.Ordinal)
            .Replace("\r", "\\r", StringComparison.Ordinal)
            .Replace("\n", "\\n", StringComparison.Ordinal)
            .Replace("\t", "\\t", StringComparison.Ordinal);
    }

    /// <summary>
    /// Prevents line breaks from corrupting an emitted comment in the local declaration list.
    /// </summary>
    private static string EscapeComment(string value)
    {
        return value.Replace("\r", " ", StringComparison.Ordinal).Replace("\n", " ", StringComparison.Ordinal);
    }
}

/// <summary>
/// Holds the CIL text emitted from one shared TAC program.
/// </summary>
public sealed record CilGenerationResult(string IlCode);

/// <summary>
/// Provides an actionable compiler diagnostic for unsupported CIL lowering.
/// </summary>
public sealed class CilGenerationException : Exception
{
    public CilGenerationException(string code, string message, SourceSpan span)
        : base(message)
    {
        Code = code;
        Span = span;
    }

    public string Code { get; }

    public SourceSpan Span { get; }
}
