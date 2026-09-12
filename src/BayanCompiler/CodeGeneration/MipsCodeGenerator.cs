using System.Text;
using BayanCompiler.Intermediate;
using BayanCompiler.Lexing;
using BayanCompiler.Semantics;

namespace BayanCompiler.CodeGeneration;

/// <summary>
/// يحول Three-Address IR إلى MIPS Assembly متوافق مع محاكي MARS.
/// </summary>
public sealed class MipsCodeGenerator
{
    /// <summary>
    /// يولد النص الكامل لملف ASM، شاملاً أقسام البيانات والنص والتعليمات.
    /// </summary>
    public MipsGenerationResult Generate(IrProgram program)
    {
        // يجمع أسطر ملف MIPS بالترتيب النهائي.
        var builder = new StringBuilder();

        // يكتب قسم البيانات أولاً لأن المتغيرات والنصوص تحتاج عناوين ثابتة.
        EmitDataSection(builder, program);

        // يكتب قسم النص وتعليمة main ثم باقي تعليمات البرنامج.
        EmitTextSection(builder, program);

        // يعيد النص الناتج ومعه البرنامج الوسيط للعرض في الواجهة.
        return new MipsGenerationResult(builder.ToString(), program);
    }

    /// <summary>
    /// يكتب تعريفات المتغيرات والقيم المؤقتة والنصوص في قسم .data.
    /// </summary>
    private static void EmitDataSection(StringBuilder builder, IrProgram program)
    {
        // يبدأ قسم البيانات في صيغة MARS.
        builder.AppendLine(".data");

        // يحجز كلمة 32-بت لكل متغير أو قيمة مؤقتة.
        foreach (KeyValuePair<string, LanguageType> item in program.Storage)
        {
            // يفصل أسماء العرض العربية عن ملف ASM كي يبقى صالحاً لمحاكيات لا تقبل UTF-8 في التعليقات.
            string? sourceName = program.GetStorageSourceName(item.Key);
            if (!string.IsNullOrWhiteSpace(sourceName))
            {
                // يكتب التسمية الداخلية ASCII بدلاً من الاسم العربي؛ خريطة الأسماء تبقى متاحة في IR والواجهة.
                builder.AppendLine("# source-storage: " + item.Key);
            }

            int wordCount = program.StorageWordCounts.TryGetValue(item.Key, out int storedWordCount) ? storedWordCount : 1;
            builder.AppendLine(item.Value == LanguageType.Real ? item.Key + ": .float 0.0" : wordCount == 1 ? item.Key + ": .word 0" : item.Key + ": .space " + (wordCount * 4));
        }

        // يضيف تعريفات النصوص اللازمة لأوامر اطبع.
        foreach (KeyValuePair<string, string> item in program.Strings)
        {
            // يكتب UTF-8 كبايتات ASCII صريحة حتى تعمل النصوص العربية في SPIM وMARS وJsSpim.
            builder.AppendLine(item.Key + ": .byte " + EncodeUtf8Bytes(item.Value));
        }

        foreach (KeyValuePair<string, string> item in program.Floats)
        {
            builder.AppendLine(item.Key + ": .float " + item.Value);
        }

        foreach (KeyValuePair<string, int> item in program.Buffers)
        {
            builder.AppendLine(item.Key + ": .space " + item.Value);
        }

        // يضيف بايت نهاية السطر ثم بايت النهاية بصيغة تقبلها محاكيات SPIM online.
        builder.AppendLine("newline: .byte 10, 0");
        builder.AppendLine();
    }

    /// <summary>
    /// يكتب تعليمات البرنامج في قسم .text وينهيه بـsyscall 10.
    /// </summary>
    private static void EmitTextSection(StringBuilder builder, IrProgram program)
    {
        // يحدد بداية قسم التعليمات ونقطة الدخول التي يتعرف عليها MARS.
        builder.AppendLine(".text");
        builder.AppendLine(".globl main");
        builder.AppendLine("main:");

        // يحول تعليمات IR واحدة واحدة إلى MIPS.
        foreach (IrInstruction instruction in program.Instructions)
        {
            EmitInstruction(builder, instruction);
        }

        // ينهي البرنامج بواسطة خدمة الخروج في MARS.
        builder.AppendLine("    li $v0, 10");
        builder.AppendLine("    syscall");
    }

    /// <summary>
    /// يحول تعليمة IR واحدة إلى تعليمات MIPS المقابلة.
    /// </summary>
    private static void EmitInstruction(StringBuilder builder, IrInstruction instruction)
    {
        // يحول إسناد قيمة إلى تحميل ثم تخزين في الذاكرة.
        if (instruction.Opcode == IrOpcode.Assign)
        {
            if (instruction.Result!.Type == LanguageType.Real)
            {
                LoadFloatValue(builder, instruction.Left!, "$f0");
                builder.AppendLine("    s.s $f0, " + instruction.Result.Name);
                return;
            }

            if (instruction.Left!.Kind == IrValueKind.StringLabel)
            {
                builder.AppendLine("    la $t0, " + instruction.Left.Name);
            }
            else
            {
                LoadValue(builder, instruction.Left, "$t0");
            }

            builder.AppendLine("    sw $t0, " + instruction.Result!.Name);
            return;
        }

        // يحول العملية الثنائية إلى تحميل الطرفين ثم عملية وتخزين النتيجة.
        if (instruction.Opcode == IrOpcode.Binary)
        {
            if (instruction.Left!.Type == LanguageType.Real || instruction.Right!.Type == LanguageType.Real)
            {
                EmitRealBinary(builder, instruction);
                return;
            }

            EmitBinary(builder, instruction);
            return;
        }

        // يحول العملية الأحادية إلى تحميل قيمة ثم تنفيذ العملية.
        if (instruction.Opcode == IrOpcode.Unary)
        {
            if (instruction.Result!.Type == LanguageType.Real)
            {
                LoadFloatValue(builder, instruction.Left!, "$f0");
                if (instruction.Operator != TokenType.Minus)
                {
                    throw new InvalidOperationException("عامل حقيقي أحادي غير مدعوم في مولد MIPS: " + instruction.Operator);
                }

                builder.AppendLine("    neg.s $f2, $f0");
                builder.AppendLine("    s.s $f2, " + instruction.Result.Name);
                return;
            }

            EmitUnary(builder, instruction);
            return;
        }

        // يحول العلامة إلى تسمية MIPS منتهية بنقطتين.
        if (instruction.Opcode == IrOpcode.Label)
        {
            builder.AppendLine(instruction.TargetLabel + ":");
            return;
        }

        // يحول القفز غير المشروط إلى j.
        if (instruction.Opcode == IrOpcode.Goto)
        {
            builder.AppendLine("    j " + instruction.TargetLabel);
            return;
        }

        // يحول القفز عند صفر الشرط إلى beq مع $zero.
        if (instruction.Opcode == IrOpcode.IfZeroGoto)
        {
            LoadValue(builder, instruction.Left!, "$t0");
            builder.AppendLine("    beq $t0, $zero, " + instruction.TargetLabel);
            return;
        }

        // يحول طباعة العدد أو المنطقي إلى syscall 1 ثم نهاية السطر.
        if (instruction.Opcode == IrOpcode.PrintInt)
        {
            LoadValue(builder, instruction.Left!, "$a0");
            builder.AppendLine("    li $v0, 1");
            builder.AppendLine("    syscall");
            EmitNewLine(builder);
            return;
        }

        // يحول طباعة النص إلى syscall 4 ثم نهاية السطر.
        if (instruction.Opcode == IrOpcode.PrintString)
        {
            if (instruction.Left!.Kind == IrValueKind.StringLabel)
            {
                builder.AppendLine("    la $a0, " + instruction.Left.Name);
            }
            else
            {
                LoadValue(builder, instruction.Left, "$a0");
            }

            builder.AppendLine("    li $v0, 4");
            builder.AppendLine("    syscall");
            EmitNewLine(builder);
            return;
        }

        if (instruction.Opcode == IrOpcode.PrintReal)
        {
            LoadFloatValue(builder, instruction.Left!, "$f12");
            builder.AppendLine("    li $v0, 2");
            builder.AppendLine("    syscall");
            EmitNewLine(builder);
            return;
        }

        if (instruction.Opcode == IrOpcode.ReadInt)
        {
            builder.AppendLine("    li $v0, 5");
            builder.AppendLine("    syscall");
            builder.AppendLine("    sw $v0, " + instruction.Result!.Name);
            return;
        }

        if (instruction.Opcode == IrOpcode.Call)
        {
            builder.AppendLine("    jal " + instruction.TargetLabel);
            return;
        }

        if (instruction.Opcode == IrOpcode.Return)
        {
            builder.AppendLine("    jr $ra");
            return;
        }

        if (instruction.Opcode == IrOpcode.PrintChar)
        {
            LoadValue(builder, instruction.Left!, "$a0");
            builder.AppendLine("    li $v0, 11");
            builder.AppendLine("    syscall");
            EmitNewLine(builder);
            return;
        }

        if (instruction.Opcode == IrOpcode.ReadChar)
        {
            builder.AppendLine("    li $v0, 12");
            builder.AppendLine("    syscall");
            builder.AppendLine("    sw $v0, " + instruction.Result!.Name);
            return;
        }

        if (instruction.Opcode == IrOpcode.ReadReal)
        {
            builder.AppendLine("    li $v0, 6");
            builder.AppendLine("    syscall");
            builder.AppendLine("    s.s $f0, " + instruction.Result!.Name);
            return;
        }

        if (instruction.Opcode == IrOpcode.ReadString)
        {
            builder.AppendLine("    la $a0, " + instruction.TargetLabel);
            builder.AppendLine("    li $a1, 256");
            builder.AppendLine("    li $v0, 8");
            builder.AppendLine("    syscall");
            builder.AppendLine("    la $t0, " + instruction.TargetLabel);
            builder.AppendLine("    sw $t0, " + instruction.Result!.Name);
            return;
        }

        if (instruction.Opcode == IrOpcode.LoadIndexed)
        {
            builder.AppendLine("    la $t0, " + instruction.Left!.Name);
            LoadValue(builder, instruction.Right!, "$t1");
            builder.AppendLine("    sll $t1, $t1, 2");
            builder.AppendLine("    addu $t0, $t0, $t1");
            if (instruction.Result!.Type == LanguageType.Real)
            {
                builder.AppendLine("    l.s $f0, 0($t0)");
                builder.AppendLine("    s.s $f0, " + instruction.Result.Name);
            }
            else
            {
                builder.AppendLine("    lw $t2, 0($t0)");
                builder.AppendLine("    sw $t2, " + instruction.Result.Name);
            }

            return;
        }

        if (instruction.Opcode == IrOpcode.StoreIndexed)
        {
            builder.AppendLine("    la $t0, " + instruction.Result!.Name);
            LoadValue(builder, instruction.Left!, "$t1");
            builder.AppendLine("    sll $t1, $t1, 2");
            builder.AppendLine("    addu $t0, $t0, $t1");
            if (instruction.Right!.Type == LanguageType.Real)
            {
                LoadFloatValue(builder, instruction.Right, "$f0");
                builder.AppendLine("    s.s $f0, 0($t0)");
            }
            else
            {
                LoadValue(builder, instruction.Right, "$t2");
                builder.AppendLine("    sw $t2, 0($t0)");
            }

            return;
        }

        // يمنع تجاهل Opcode جديد عند توسعة IR مستقبلاً.
        throw new InvalidOperationException("تعليمة IR غير مدعومة في مولد MIPS: " + instruction.Opcode);
    }

    /// <summary>
    /// يحول العمليات الحسابية والمقارنات والمنطق إلى تعليمات MIPS أو pseudo-instructions في MARS.
    /// </summary>
    private static void EmitBinary(StringBuilder builder, IrInstruction instruction)
    {
        // يحمل طرفي العملية في سجلي عمل مؤقتين.
        LoadValue(builder, instruction.Left!, "$t0");
        LoadValue(builder, instruction.Right!, "$t1");

        // يولد العملية الحسابية أو المقارنة في $t2.
        switch (instruction.Operator)
        {
            case TokenType.Plus:
                builder.AppendLine("    add $t2, $t0, $t1");
                break;

            case TokenType.Minus:
                builder.AppendLine("    sub $t2, $t0, $t1");
                break;

            case TokenType.Star:
                builder.AppendLine("    mult $t0, $t1");
                builder.AppendLine("    mflo $t2");
                break;

            case TokenType.Slash:
                builder.AppendLine("    div $t0, $t1");
                builder.AppendLine("    mflo $t2");
                break;

            case TokenType.Backslash:
                builder.AppendLine("    div $t0, $t1");
                builder.AppendLine("    mflo $t2");
                break;

            case TokenType.Percent:
                builder.AppendLine("    div $t0, $t1");
                builder.AppendLine("    mfhi $t2");
                break;

            case TokenType.Less:
                builder.AppendLine("    slt $t2, $t0, $t1");
                break;

            case TokenType.LessEqual:
                builder.AppendLine("    slt $t2, $t1, $t0");
                builder.AppendLine("    xori $t2, $t2, 1");
                break;

            case TokenType.Greater:
                builder.AppendLine("    slt $t2, $t1, $t0");
                break;

            case TokenType.GreaterEqual:
                builder.AppendLine("    slt $t2, $t0, $t1");
                builder.AppendLine("    xori $t2, $t2, 1");
                break;

            case TokenType.EqualEqual:
                builder.AppendLine("    sub $t2, $t0, $t1");
                builder.AppendLine("    sltiu $t2, $t2, 1");
                break;

            case TokenType.BangEqual:
                builder.AppendLine("    sub $t2, $t0, $t1");
                builder.AppendLine("    sltu $t2, $zero, $t2");
                break;

            case TokenType.AndAnd:
                builder.AppendLine("    and $t2, $t0, $t1");
                break;

            case TokenType.OrOr:
                builder.AppendLine("    or $t2, $t0, $t1");
                break;

            case TokenType.Caret:
                EmitPower(builder, instruction.Result!.Name);
                break;

            default:
                throw new InvalidOperationException("عامل ثنائي غير مدعوم في مولد MIPS: " + instruction.Operator);
        }

        // يخزن النتيجة في قيمة IR المؤقتة المحددة.
        builder.AppendLine("    sw $t2, " + instruction.Result!.Name);
    }

    /// <summary>
    /// Emits single-precision arithmetic and comparison instructions for real operands.
    /// </summary>
    private static void EmitRealBinary(StringBuilder builder, IrInstruction instruction)
    {
        LoadFloatValue(builder, instruction.Left!, "$f0");
        LoadFloatValue(builder, instruction.Right!, "$f2");

        switch (instruction.Operator)
        {
            case TokenType.Plus:
                builder.AppendLine("    add.s $f4, $f0, $f2");
                break;
            case TokenType.Minus:
                builder.AppendLine("    sub.s $f4, $f0, $f2");
                break;
            case TokenType.Star:
                builder.AppendLine("    mul.s $f4, $f0, $f2");
                break;
            case TokenType.Slash:
                builder.AppendLine("    div.s $f4, $f0, $f2");
                break;
            case TokenType.Less:
                EmitRealComparison(builder, instruction, "c.lt.s $f0, $f2", false);
                return;
            case TokenType.LessEqual:
                EmitRealComparison(builder, instruction, "c.le.s $f0, $f2", false);
                return;
            case TokenType.Greater:
                EmitRealComparison(builder, instruction, "c.lt.s $f2, $f0", false);
                return;
            case TokenType.GreaterEqual:
                EmitRealComparison(builder, instruction, "c.le.s $f2, $f0", false);
                return;
            case TokenType.EqualEqual:
                EmitRealComparison(builder, instruction, "c.eq.s $f0, $f2", false);
                return;
            case TokenType.BangEqual:
                EmitRealComparison(builder, instruction, "c.eq.s $f0, $f2", true);
                return;
            default:
                throw new InvalidOperationException("عامل حقيقي غير مدعوم في مولد MIPS: " + instruction.Operator);
        }

        builder.AppendLine("    s.s $f4, " + instruction.Result!.Name);
    }

    /// <summary>
    /// Converts an FPU condition flag into a conventional integer boolean storage value.
    /// </summary>
    private static void EmitRealComparison(StringBuilder builder, IrInstruction instruction, string comparison, bool invert)
    {
        string trueLabel = "float_true_" + instruction.Result!.Name;
        string endLabel = "float_end_" + instruction.Result.Name;
        builder.AppendLine("    " + comparison);
        builder.AppendLine("    " + (invert ? "bc1f " : "bc1t ") + trueLabel);
        builder.AppendLine("    li $t2, 0");
        builder.AppendLine("    j " + endLabel);
        builder.AppendLine(trueLabel + ":");
        builder.AppendLine("    li $t2, 1");
        builder.AppendLine(endLabel + ":");
        builder.AppendLine("    sw $t2, " + instruction.Result!.Name);
    }

    /// <summary>
    /// Emits integer exponentiation for the validated non-negative immediate exponent subset.
    /// </summary>
    private static void EmitPower(StringBuilder builder, string resultName)
    {
        string loopLabel = "pow_loop_" + resultName;
        string endLabel = "pow_end_" + resultName;
        builder.AppendLine("    li $t2, 1");
        builder.AppendLine(loopLabel + ":");
        builder.AppendLine("    beq $t1, $zero, " + endLabel);
        builder.AppendLine("    mult $t2, $t0");
        builder.AppendLine("    mflo $t2");
        builder.AppendLine("    addiu $t1, $t1, -1");
        builder.AppendLine("    j " + loopLabel);
        builder.AppendLine(endLabel + ":");
    }

    /// <summary>
    /// يحول السالب الأحادي والنفي المنطقي إلى MIPS.
    /// </summary>
    private static void EmitUnary(StringBuilder builder, IrInstruction instruction)
    {
        // يحمل معامل العملية الأحادية في سجل عمل.
        LoadValue(builder, instruction.Left!, "$t0");

        // ينفذ العملية المطلوبة في $t2.
        if (instruction.Operator == TokenType.Minus)
        {
            builder.AppendLine("    sub $t2, $zero, $t0");
        }
        else if (instruction.Operator == TokenType.Bang)
        {
            builder.AppendLine("    sltu $t2, $t0, 1");
        }
        else
        {
            throw new InvalidOperationException("عامل أحادي غير مدعوم في مولد MIPS: " + instruction.Operator);
        }

        // يخزن النتيجة المؤقتة في الذاكرة.
        builder.AppendLine("    sw $t2, " + instruction.Result!.Name);
    }

    /// <summary>
    /// يحمل قيمة فورية أو قيمة مخزنة في الذاكرة إلى سجل MIPS.
    /// </summary>
    private static void LoadValue(StringBuilder builder, IrValue value, string register)
    {
        // يحمل العدد المباشر بواسطة li.
        if (value.Kind == IrValueKind.Immediate)
        {
            builder.AppendLine("    li " + register + ", " + value.Name);
            return;
        }

        // يحمل المتغير أو القيمة المؤقتة بواسطة lw.
        if (value.Kind == IrValueKind.Variable || value.Kind == IrValueKind.Temporary)
        {
            builder.AppendLine("    lw " + register + ", " + value.Name);
            return;
        }

        // يمنع تحميل النص كعدد؛ PrintString يعالجه بواسطة la.
        throw new InvalidOperationException("لا يمكن تحميل قيمة MIPS من النوع: " + value.Kind);
    }

    /// <summary>
    /// Loads a real value into an FPU register and promotes integer-compatible values when required.
    /// </summary>
    private static void LoadFloatValue(StringBuilder builder, IrValue value, string register)
    {
        if (value.Kind == IrValueKind.FloatLabel)
        {
            builder.AppendLine("    l.s " + register + ", " + value.Name);
            return;
        }

        if ((value.Kind == IrValueKind.Variable || value.Kind == IrValueKind.Temporary) && value.Type == LanguageType.Real)
        {
            builder.AppendLine("    l.s " + register + ", " + value.Name);
            return;
        }

        LoadValue(builder, value, "$t0");
        builder.AppendLine("    mtc1 $t0, " + register);
        builder.AppendLine("    cvt.s.w " + register + ", " + register);
    }

    /// <summary>
    /// يطبع نهاية سطر بعد كل أمر اطبع.
    /// </summary>
    private static void EmitNewLine(StringBuilder builder)
    {
        // يمرر عنوان النص newline إلى syscall 4.
        builder.AppendLine("    la $a0, newline");
        builder.AppendLine("    li $v0, 4");
        builder.AppendLine("    syscall");
    }

    /// <summary>
    /// يحول النص إلى بايتات UTF-8 سداسية عشرية مع صفر نهائي لتمثيل C string في MIPS.
    /// </summary>
    private static string EncodeUtf8Bytes(string value)
    {
        // يحافظ على ملف ASM ASCII فقط، بينما syscall 4 يطبع البايتات UTF-8 المخزنة في الذاكرة.
        return string.Join(", ", Encoding.UTF8.GetBytes(value)
            .Select(item => "0x" + item.ToString("X2"))
            .Append("0"));
    }
}

/// <summary>
/// يمثل نتيجة توليد MIPS الجاهزة للعرض والحفظ بامتداد .asm.
/// </summary>
public sealed record MipsGenerationResult(string AssemblyCode, IrProgram IrProgram);
