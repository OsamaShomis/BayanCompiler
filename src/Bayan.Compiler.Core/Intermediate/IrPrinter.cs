using BayanCompiler.Lexing;
using BayanCompiler.Semantics;

namespace BayanCompiler.Intermediate;

/// <summary>
/// يحول تعليمات IR إلى نص واضح لعرضه في واجهة المترجم.
/// </summary>
public static class IrPrinter
{
    /// <summary>
    /// يعرض البرنامج الوسيط كاملاً مع تعليمات قسم البيانات.
    /// </summary>
    public static string Format(IrProgram program)
    {
        // يجمع أسطر IR بالترتيب.
        var lines = new List<string>();

        // Adds a bilingual title so the tab is understandable in a project demonstration.
        lines.Add("# Three-Address IR — التمثيل الوسيط ثلاثي العناوين");

        // Displays the source-to-storage mapping before the instruction list.
        if (program.StorageSourceNames.Count > 0)
        {
            lines.Add(string.Empty);
            lines.Add("خريطة الأسماء:");
            foreach (KeyValuePair<string, string> mapping in program.StorageSourceNames)
            {
                lines.Add("  " + mapping.Value + "  →  " + mapping.Key);
            }
        }

        // يعرض المتغيرات والقيم المؤقتة المحجوزة.
        foreach (KeyValuePair<string, LanguageType> storage in program.Storage)
        {
            lines.Add(FormatStorage(program, storage));
        }

        // يفصل تعريفات الذاكرة عن التعليمات عند وجودها.
        if (program.Storage.Count > 0)
        {
            lines.Add(string.Empty);
        }

        // يعرض كل تعليمة بصيغة قريبة من ثلاثة عناوين.
        foreach (IrInstruction instruction in program.Instructions)
        {
            lines.Add(FormatInstruction(instruction));
        }

        // يعيد النص كسطور متتابعة للعرض.
        return string.Join(System.Environment.NewLine, lines);
    }

    /// <summary>
    /// Formats one storage declaration using the Arabic source name when available.
    /// </summary>
    private static string FormatStorage(IrProgram program, KeyValuePair<string, LanguageType> storage)
    {
        // Preserves the safe internal label in brackets for tracing to generated MIPS.
        string? sourceName = program.GetStorageSourceName(storage.Key);
        return string.IsNullOrWhiteSpace(sourceName)
            ? storage.Key + " : " + FriendlyType(storage.Value)
            : sourceName + " [" + storage.Key + "] : " + FriendlyType(storage.Value);
    }

    /// <summary>
    /// يحول تعليمة IR واحدة إلى سطر نصي.
    /// </summary>
    private static string FormatInstruction(IrInstruction instruction)
    {
        // يعرض التخزين البسيط للقيم.
        if (instruction.Opcode == IrOpcode.Assign)
        {
            return instruction.Result + " = " + instruction.Left;
        }

        // يعرض العمليات الثنائية مثل temp_0 = var_0 + 1.
        if (instruction.Opcode == IrOpcode.Binary)
        {
            return instruction.Result + " = " + instruction.Left + " " + OperatorText(instruction.Operator) + " " + instruction.Right;
        }

        // يعرض العمليات الأحادية مثل temp_0 = !var_0.
        if (instruction.Opcode == IrOpcode.Unary)
        {
            return instruction.Result + " = " + OperatorText(instruction.Operator) + instruction.Left;
        }

        // يعرض العلامة بصيغة مألوفة للقفز.
        if (instruction.Opcode == IrOpcode.Label)
        {
            return FormatLabel(instruction.TargetLabel) + ":";
        }

        // يعرض القفز غير المشروط.
        if (instruction.Opcode == IrOpcode.Goto)
        {
            return "اذهب إلى " + FormatLabel(instruction.TargetLabel);
        }

        // يعرض القفز عند صفر الشرط.
        if (instruction.Opcode == IrOpcode.IfZeroGoto)
        {
            return "إذا كان " + instruction.Left + " = 0 اذهب إلى " + FormatLabel(instruction.TargetLabel);
        }

        // Shows integer output in Arabic while the generator keeps the internal PrintInt opcode.
        if (instruction.Opcode == IrOpcode.PrintInt)
        {
            return "اطبع_عدد " + instruction.Left;
        }

        // Shows string output in Arabic while the generator keeps the internal PrintString opcode.
        if (instruction.Opcode == IrOpcode.PrintString)
        {
            return "اطبع_نص " + instruction.Left;
        }

        if (instruction.Opcode == IrOpcode.ReadInt)
        {
            return "اقرأ_عدد " + instruction.Result;
        }

        if (instruction.Opcode == IrOpcode.Call)
        {
            return "استدعِ " + FormatLabel(instruction.TargetLabel);
        }

        if (instruction.Opcode == IrOpcode.Return)
        {
            return "عودة";
        }

        if (instruction.Opcode == IrOpcode.PrintChar)
        {
            return "اطبع_حرف " + instruction.Left;
        }

        if (instruction.Opcode == IrOpcode.ReadChar)
        {
            return "اقرأ_حرف " + instruction.Result;
        }

        if (instruction.Opcode == IrOpcode.LoadIndexed)
        {
            return instruction.Result + " = " + instruction.Left + "[" + instruction.Right + "]";
        }

        if (instruction.Opcode == IrOpcode.StoreIndexed)
        {
            return instruction.Result + "[" + instruction.Left + "] = " + instruction.Right;
        }

        if (instruction.Opcode == IrOpcode.PrintReal)
        {
            return "اطبع_حقيقي " + instruction.Left;
        }

        if (instruction.Opcode == IrOpcode.ReadReal)
        {
            return "اقرأ_حقيقي " + instruction.Result;
        }

        if (instruction.Opcode == IrOpcode.ReadString)
        {
            return "اقرأ_خيط " + instruction.Result;
        }

        // يعيد نصاً احتياطياً عند ظهور Opcode جديد.
        return instruction.Opcode.ToString();
    }

    /// <summary>
    /// Converts compiler types to the Arabic labels used by the source language.
    /// </summary>
    private static string FriendlyType(LanguageType type)
    {
        // Keeps technical type names out of the primary Arabic presentation.
        return type switch
        {
            LanguageType.Int => "عدد",
            LanguageType.Real => "حقيقي",
            LanguageType.Bool => "منطقي",
            LanguageType.Text => "نص",
            LanguageType.Char => "حرفي",
            _ => type.ToString()
        };
    }

    /// <summary>
    /// Converts internal ASCII control labels to Arabic labels for IR presentation only.
    /// </summary>
    private static string FormatLabel(string? label)
    {
        // Keeps MIPS labels unchanged in the generator and localizes only the viewer text.
        if (string.IsNullOrWhiteSpace(label))
        {
            return string.Empty;
        }

        if (label.StartsWith("while_", StringComparison.Ordinal))
        {
            return "حلقة_" + label.Substring("while_".Length);
        }

        if (label.StartsWith("endwhile_", StringComparison.Ordinal))
        {
            return "نهاية_الحلقة_" + label.Substring("endwhile_".Length);
        }

        if (label.StartsWith("else_", StringComparison.Ordinal))
        {
            return "وإلا_" + label.Substring("else_".Length);
        }

        if (label.StartsWith("endif_", StringComparison.Ordinal))
        {
            return "نهاية_إذا_" + label.Substring("endif_".Length);
        }

        if (label.StartsWith("proc_", StringComparison.Ordinal))
        {
            return "إجراء_" + label.Substring("proc_".Length);
        }

        return label;
    }

    /// <summary>
    /// يعيد رمز العامل النصي المقابل لـTokenType.
    /// </summary>
    private static string OperatorText(TokenType? operation)
    {
        // يطابق العوامل المدعومة في لغة بيان.
        return operation switch
        {
            TokenType.Plus => "+",
            TokenType.Minus => "-",
            TokenType.Star => "*",
            TokenType.Slash => "/",
            TokenType.Backslash => "\\",
            TokenType.Percent => "%",
            TokenType.Caret => "^",
            TokenType.Less => "<",
            TokenType.LessEqual => "<=",
            TokenType.Greater => ">",
            TokenType.GreaterEqual => ">=",
            TokenType.EqualEqual => "==",
            TokenType.BangEqual => "!=",
            TokenType.AndAnd => "&&",
            TokenType.OrOr => "||",
            TokenType.Bang => "!",
            _ => "?"
        };
    }
}
