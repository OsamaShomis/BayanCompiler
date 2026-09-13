using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Globalization;
using System.IO;
using System.Linq;
using System.Text;
using BayanCompiler.Frontend;
using BayanCompiler.Intermediate;
using BayanCompiler.Lexing;
using BayanCompiler.Semantics;

namespace BayanCompiler.CodeGeneration;

public sealed record AssemblyGenerationResult(string AssemblyDisplayCode, string ExecutablePath);

public sealed class AssemblyGenerator
{
    public AssemblyGenerationResult Generate(IrProgram program, string outputDirectory)
    {
        // Use the official MipsCodeGenerator for genuine, runnable MIPS Assembly
        string asmText = new MipsCodeGenerator().Generate(program).AssemblyCode;
        string asmPath = Path.Combine(outputDirectory, "output.asm");
        File.WriteAllText(asmPath, asmText, Encoding.UTF8);

        string ilText = GenerateIl(program);
        string ilPath = Path.Combine(outputDirectory, "output.il");
        File.WriteAllText(ilPath, ilText, Encoding.UTF8);

        string exePath = Path.Combine(outputDirectory, "output.exe");
        try
        {
            CompileIlToExe(ilPath, exePath);
        }
        catch
        {
            exePath = string.Empty;
        }

        return new AssemblyGenerationResult(asmText, exePath);
    }

    private string GenerateIl(IrProgram program)
    {
        var builder = new StringBuilder();
        builder.AppendLine(".assembly extern mscorlib {}");
        builder.AppendLine(".assembly BayanProgram {}");
        builder.AppendLine(".module BayanProgram.exe");
        builder.AppendLine();
        builder.AppendLine(".class public auto ansi beforefieldinit BayanProgram");
        builder.AppendLine("       extends [mscorlib]System.Object");
        builder.AppendLine("{");

        // Collect all variables, parameters, and temporaries as static fields
        var fields = new Dictionary<string, string>(StringComparer.Ordinal);
        foreach (var storage in program.Storage)
        {
            fields[storage.Key] = GetCilFieldType(storage.Key, program);
        }

        foreach (var inst in program.Instructions)
        {
            foreach (var val in new[] { inst.Result, inst.Left, inst.Right })
            {
                if (val != null && (val.Kind == IrValueKind.Temporary || val.Kind == IrValueKind.Variable) && !fields.ContainsKey(val.Name))
                {
                    string typeName = val.Type == LanguageType.Real ? "float32" : (val.Type == LanguageType.Text ? "string" : "int32");
                    fields[val.Name] = typeName;
                }
            }
        }

        foreach (var f in fields)
        {
            builder.AppendLine($"    .field public static {f.Value} {f.Key}");
        }
        builder.AppendLine();

        // Separate instructions into procedures vs main method
        var procedureInstructions = new Dictionary<string, List<IrInstruction>>(StringComparer.Ordinal);
        var mainInstructions = new List<IrInstruction>();

        string? currentProc = null;
        foreach (var inst in program.Instructions)
        {
            if (inst.Opcode == IrOpcode.Label && !string.IsNullOrWhiteSpace(inst.TargetLabel) && inst.TargetLabel.StartsWith("proc_"))
            {
                currentProc = inst.TargetLabel;
                procedureInstructions[currentProc] = new List<IrInstruction>();
                continue;
            }

            if (currentProc != null)
            {
                if (inst.Opcode == IrOpcode.Return)
                {
                    procedureInstructions[currentProc].Add(inst);
                    currentProc = null;
                }
                else
                {
                    procedureInstructions[currentProc].Add(inst);
                }
            }
            else
            {
                mainInstructions.Add(inst);
            }
        }

        // Emit each procedure as a static method
        foreach (var proc in procedureInstructions)
        {
            builder.AppendLine($"    .method public static void {proc.Key}() cil managed");
            builder.AppendLine("    {");
            builder.AppendLine("        .maxstack 32");
            var procLabels = BuildLabelMap(proc.Value);
            foreach (var inst in proc.Value)
            {
                EmitIlInstruction(builder, inst, program, fields, procLabels);
            }
            builder.AppendLine("        ret");
            builder.AppendLine("    }");
            builder.AppendLine();
        }

        // Emit Main method
        builder.AppendLine("    .method public static void Main() cil managed");
        builder.AppendLine("    {");
        builder.AppendLine("        .entrypoint");
        builder.AppendLine("        .maxstack 32");

        // Initialize aggregate array fields in Main
        foreach (var f in fields)
        {
            if (f.Value == "int32[]")
            {
                int words = program.StorageWordCounts.TryGetValue(f.Key, out int count) ? count : 1;
                builder.AppendLine($"        ldc.i4 {words}");
                builder.AppendLine($"        newarr [mscorlib]System.Int32");
                builder.AppendLine($"        stsfld int32[] BayanProgram::{f.Key}");
            }
        }

        var mainLabels = BuildLabelMap(mainInstructions);
        foreach (var instruction in mainInstructions)
        {
            EmitIlInstruction(builder, instruction, program, fields, mainLabels);
        }

        builder.AppendLine("        ret");
        builder.AppendLine("    }");
        builder.AppendLine("}");
        return builder.ToString();
    }

    private static string GetCilFieldType(string name, IrProgram program)
    {
        int words = program.StorageWordCounts.TryGetValue(name, out int count) ? count : 1;
        if (words > 1) return "int32[]";
        if (program.Storage.TryGetValue(name, out LanguageType type))
        {
            if (type == LanguageType.Real) return "float32";
            if (type == LanguageType.Text) return "string";
            if (type == LanguageType.Char) return "int32";
        }
        return "int32";
    }

    private static Dictionary<string, string> BuildLabelMap(IEnumerable<IrInstruction> instructions)
    {
        var labels = new Dictionary<string, string>(StringComparer.Ordinal);
        int labelIndex = 0;
        foreach (var inst in instructions)
        {
            if (!string.IsNullOrWhiteSpace(inst.TargetLabel) && !labels.ContainsKey(inst.TargetLabel))
            {
                labels[inst.TargetLabel] = "IL_" + (labelIndex++).ToString("D4");
            }
        }
        return labels;
    }

    private void EmitIlInstruction(StringBuilder builder, IrInstruction instruction, IrProgram program, Dictionary<string, string> fields, Dictionary<string, string> labels)
    {
        switch (instruction.Opcode)
        {
            case IrOpcode.Assign:
                EmitLoadIlValue(builder, instruction.Left!, program, fields);
                EmitStoreIlValue(builder, instruction.Result!, fields);
                break;
            case IrOpcode.Binary:
                EmitLoadIlValue(builder, instruction.Left!, program, fields);
                EmitLoadIlValue(builder, instruction.Right!, program, fields);
                EmitBinaryOp(builder, instruction.Operator!.Value);
                EmitStoreIlValue(builder, instruction.Result!, fields);
                break;
            case IrOpcode.Unary:
                EmitLoadIlValue(builder, instruction.Left!, program, fields);
                if (instruction.Operator == TokenType.Minus) builder.AppendLine("        neg");
                else if (instruction.Operator == TokenType.Bang) { builder.AppendLine("        ldc.i4.0"); builder.AppendLine("        ceq"); }
                EmitStoreIlValue(builder, instruction.Result!, fields);
                break;
            case IrOpcode.Label:
                if (labels.TryGetValue(instruction.TargetLabel!, out string? lbl))
                {
                    builder.AppendLine(lbl + ":");
                }
                break;
            case IrOpcode.Goto:
                if (labels.TryGetValue(instruction.TargetLabel!, out string? gotoLbl))
                {
                    builder.AppendLine("        br " + gotoLbl);
                }
                break;
            case IrOpcode.IfZeroGoto:
                EmitLoadIlValue(builder, instruction.Left!, program, fields);
                if (labels.TryGetValue(instruction.TargetLabel!, out string? ifLbl))
                {
                    builder.AppendLine("        brfalse " + ifLbl);
                }
                break;
            case IrOpcode.PrintInt:
                EmitLoadIlValue(builder, instruction.Left!, program, fields);
                builder.AppendLine("        call void [mscorlib]System.Console::WriteLine(int32)");
                break;
            case IrOpcode.PrintChar:
                EmitLoadIlValue(builder, instruction.Left!, program, fields);
                builder.AppendLine("        conv.u2");
                builder.AppendLine("        call void [mscorlib]System.Console::WriteLine(char)");
                break;
            case IrOpcode.PrintReal:
                EmitLoadIlValue(builder, instruction.Left!, program, fields);
                builder.AppendLine("        call void [mscorlib]System.Console::WriteLine(float32)");
                break;
            case IrOpcode.PrintString:
                EmitLoadIlValue(builder, instruction.Left!, program, fields);
                if (instruction.Left != null &&
                    instruction.Left.Kind == IrValueKind.StringLabel &&
                    program.Strings.TryGetValue(instruction.Left.Name, out string? strVal) &&
                    !strVal.StartsWith("===") &&
                    !strVal.StartsWith("---") &&
                    (strVal.EndsWith(": ") || strVal.EndsWith("= ") || strVal.EndsWith(" : ") || strVal.EndsWith(" = ")))
                {
                    builder.AppendLine("        call void [mscorlib]System.Console::Write(string)");
                }
                else
                {
                    builder.AppendLine("        call void [mscorlib]System.Console::WriteLine(string)");
                }
                break;
            case IrOpcode.ReadInt:
                builder.AppendLine("        call string [mscorlib]System.Console::ReadLine()");
                builder.AppendLine("        call int32 [mscorlib]System.Int32::Parse(string)");
                EmitStoreIlValue(builder, instruction.Result!, fields);
                break;
            case IrOpcode.ReadReal:
                builder.AppendLine("        call string [mscorlib]System.Console::ReadLine()");
                builder.AppendLine("        call float32 [mscorlib]System.Single::Parse(string)");
                EmitStoreIlValue(builder, instruction.Result!, fields);
                break;
            case IrOpcode.ReadChar:
                builder.AppendLine("        call int32 [mscorlib]System.Console::Read()");
                EmitStoreIlValue(builder, instruction.Result!, fields);
                break;
            case IrOpcode.ReadString:
                builder.AppendLine("        call string [mscorlib]System.Console::ReadLine()");
                EmitStoreIlValue(builder, instruction.Result!, fields);
                break;
            case IrOpcode.LoadIndexed:
                EmitLoadIlValue(builder, instruction.Left!, program, fields); 
                EmitLoadIlValue(builder, instruction.Right!, program, fields); 
                builder.AppendLine("        ldelem.i4");
                EmitStoreIlValue(builder, instruction.Result!, fields);
                break;
            case IrOpcode.StoreIndexed:
                EmitLoadIlValue(builder, instruction.Result!, program, fields); 
                EmitLoadIlValue(builder, instruction.Left!, program, fields); 
                EmitLoadIlValue(builder, instruction.Right!, program, fields); 
                builder.AppendLine("        stelem.i4");
                break;
            case IrOpcode.Call:
                builder.AppendLine($"        call void BayanProgram::{instruction.TargetLabel}()");
                break;
            case IrOpcode.Return:
                builder.AppendLine("        ret");
                break;
            default:
                break;
        }
    }

    private void EmitLoadIlValue(StringBuilder builder, IrValue value, IrProgram program, Dictionary<string, string> fields)
    {
        if (value == null)
        {
            builder.AppendLine("        ldc.i4.0");
            return;
        }

        if (value.Kind == IrValueKind.Immediate)
        {
            if (value.Type == LanguageType.Real && float.TryParse(value.Name, NumberStyles.Float, CultureInfo.InvariantCulture, out float fVal))
            {
                builder.AppendLine($"        ldc.r4 {fVal.ToString("G", CultureInfo.InvariantCulture)}");
            }
            else
            {
                builder.AppendLine($"        ldc.i4 {value.Name}");
            }
        }
        else if (value.Kind == IrValueKind.StringLabel)
        {
            if (program.Strings.TryGetValue(value.Name, out string? text))
            {
                builder.AppendLine($"        ldstr \"{EscapeString(text)}\"");
            }
            else
            {
                builder.AppendLine("        ldstr \"\"");
            }
        }
        else if (value.Kind == IrValueKind.FloatLabel)
        {
            if (program.Floats.TryGetValue(value.Name, out string? fVal) && float.TryParse(fVal, NumberStyles.Float, CultureInfo.InvariantCulture, out float parsed))
            {
                builder.AppendLine($"        ldc.r4 {parsed.ToString("G", CultureInfo.InvariantCulture)}");
            }
            else
            {
                builder.AppendLine("        ldc.r4 0.0");
            }
        }
        else if (fields.TryGetValue(value.Name, out string? typeName))
        {
            builder.AppendLine($"        ldsfld {typeName} BayanProgram::{value.Name}");
        }
        else
        {
            builder.AppendLine("        ldc.i4.0");
        }
    }

    private void EmitStoreIlValue(StringBuilder builder, IrValue value, Dictionary<string, string> fields)
    {
        if (value != null && fields.TryGetValue(value.Name, out string? typeName))
        {
            if (typeName == "float32")
            {
                builder.AppendLine("        conv.r4");
            }
            builder.AppendLine($"        stsfld {typeName} BayanProgram::{value.Name}");
        }
    }

    private void EmitBinaryOp(StringBuilder builder, TokenType op)
    {
        switch (op)
        {
            case TokenType.Plus: builder.AppendLine("        add"); break;
            case TokenType.Minus: builder.AppendLine("        sub"); break;
            case TokenType.Star: builder.AppendLine("        mul"); break;
            case TokenType.Slash: builder.AppendLine("        div"); break;
            case TokenType.Less: builder.AppendLine("        clt"); break;
            case TokenType.Greater: builder.AppendLine("        cgt"); break;
            case TokenType.EqualEqual: builder.AppendLine("        ceq"); break;
            case TokenType.BangEqual: builder.AppendLine("        ceq"); builder.AppendLine("        ldc.i4.0"); builder.AppendLine("        ceq"); break;
            case TokenType.LessEqual: builder.AppendLine("        cgt"); builder.AppendLine("        ldc.i4.0"); builder.AppendLine("        ceq"); break;
            case TokenType.GreaterEqual: builder.AppendLine("        clt"); builder.AppendLine("        ldc.i4.0"); builder.AppendLine("        ceq"); break;
            case TokenType.AndAnd: builder.AppendLine("        and"); break;
            case TokenType.OrOr: builder.AppendLine("        or"); break;
        }
    }

    private static string EscapeString(string str)
    {
        if (string.IsNullOrEmpty(str)) return "";
        return str.Replace("\\", "\\\\").Replace("\"", "\\\"").Replace("\n", "\\n").Replace("\r", "\\r").Replace("\t", "\\t");
    }

    private static string? FindIlasmPath()
    {
        string? envPath = Environment.GetEnvironmentVariable("BAYAN_ILASM_PATH");
        if (!string.IsNullOrEmpty(envPath) && File.Exists(envPath))
        {
            return envPath;
        }

        string[] candidates =
        {
            @"C:\Windows\Microsoft.NET\Framework64\v4.0.30319\ilasm.exe",
            @"C:\Windows\Microsoft.NET\Framework\v4.0.30319\ilasm.exe",
        };

        foreach (string candidate in candidates)
        {
            if (File.Exists(candidate))
            {
                return candidate;
            }
        }

        string? pathEnv = Environment.GetEnvironmentVariable("PATH");
        if (!string.IsNullOrEmpty(pathEnv))
        {
            foreach (string p in pathEnv.Split(Path.PathSeparator, StringSplitOptions.RemoveEmptyEntries))
            {
                string fullPath = Path.Combine(p.Trim(), "ilasm.exe");
                if (File.Exists(fullPath))
                {
                    return fullPath;
                }
            }
        }

        return null;
    }

    private void CompileIlToExe(string ilPath, string exePath)
    {
        string? ilasmPath = FindIlasmPath();
        if (ilasmPath == null)
        {
            return;
        }
        
        using Process process = Process.Start(new ProcessStartInfo
        {
            FileName = ilasmPath,
            Arguments = $"/nologo /quiet /exe /output:\"{exePath}\" \"{ilPath}\"",
            RedirectStandardOutput = true,
            RedirectStandardError = true,
            UseShellExecute = false,
            CreateNoWindow = true
        }) ?? throw new InvalidOperationException("Failed to launch ilasm process.");
        
        string stdOut = process.StandardOutput.ReadToEnd();
        string stdErr = process.StandardError.ReadToEnd();
        process.WaitForExit();
        if (process.ExitCode != 0)
        {
            string error = string.IsNullOrWhiteSpace(stdErr) ? stdOut : stdErr;
            throw new Exception("ILAsm compilation failed: " + error);
        }
    }
}
