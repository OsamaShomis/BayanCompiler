# تحويل مشروع «بيان» من مفسر إلى مترجم مولد لكود C#

## القرار المقترح

إذا كان الدكتور يطلب **مترجماً فقط**، فإن المسار العملي المناسب لمشروع Windows Forms هو تحويل برنامج «بيان» إلى ملف مصدر C# قابل للفتح والبناء في Visual Studio. تسمى هذه الطريقة **Source-to-Source Compilation** أو **Transpilation**، وهي توليد كود هدف بدلاً من تنفيذ AST مباشرة.

> احتفظوا بـLexer وParser وAST وSemantic Analyzer كما هي؛ التعديل يبدأ فقط بعد نجاح التحليل الدلالي.

## خط المعالجة الجديد

```text
ملف .bayan
    ↓
Lexer
    ↓
Parser وAST
    ↓
Semantic Analyzer
    ↓
CSharpCodeGenerator
    ↓
ملف GeneratedProgram.cs
    ↓
Visual Studio أو dotnet build
    ↓
برنامج C# قابل للتنفيذ
```

## ماذا يتغير في المشروع؟

| الجزء الحالي | القرار بعد التحويل |
|---|---|
| `Lexing` | يبقى كما هو. |
| `Parsing` و`Syntax` | تبقى كما هي. |
| `Semantics` | يبقى كما هو، ويجب أن ينجح قبل توليد الكود. |
| `Runtime/Interpreter.cs` | يصبح اختيارياً للاختبار فقط، ولا يستدعى من زر المترجم. |
| مجلد جديد `CodeGeneration` | يضاف فيه مولد C# ونتيجة التوليد. |
| زر `تشغيل` | يستبدل أو يعاد تسميته إلى `توليد كود C#`. |
| تبويب `الناتج` | يعرض كود C# المولد أو مسار الملف الناتج. |

## الملفات الجديدة المقترحة

```text
src/BayanCompiler/
├── CodeGeneration/
│   ├── CSharpCodeGenerator.cs
│   ├── GeneratedCodeResult.cs
│   └── CodeWriter.cs
└── Generated/
    └── GeneratedProgram.cs
```

### مسؤولية كل ملف

| الملف | المسؤولية |
|---|---|
| `CSharpCodeGenerator.cs` | يزور عقد AST ويحوّلها إلى أسطر C#. |
| `GeneratedCodeResult.cs` | يحمل النص المولد واسم البرنامج ومسار الحفظ. |
| `CodeWriter.cs` | يدير المسافات البادئة والأسطر المفتوحة والأقواس. |

## تحويل تعليمات «بيان» إلى C#

| بيان | C# الهدف |
|---|---|
| `دع س : عدد = 5؛` | `int س = 5;` |
| `دع ص : حقيقي = 2.5؛` | `double ص = 2.5;` |
| `دع صحيح : منطقي = صحيح؛` | `bool صحيح = true;` |
| `دع رسالة : نص = "مرحباً"؛` | `string رسالة = "مرحباً";` |
| `س ← س + 1؛` | `س = س + 1;` |
| `اطبع(س)؛` | `Console.WriteLine(س);` |
| `إذا (س > 3) { ... }` | `if (س > 3) { ... }` |
| `وإلا { ... }` | `else { ... }` |
| `طالما (س <= 5) { ... }` | `while (س <= 5) { ... }` |
| `صحيح` و`خطأ` | `true` و`false` |

## مثال كامل

### المصدر بلغة بيان

```بيان
برنامج تجربة
    دع س : عدد = 5؛
    إذا (س > 3) {
        اطبع("القيمة كبيرة")؛
    }
نهاية
```

### الكود المولد `GeneratedProgram.cs`

```csharp
using System;

public static class GeneratedProgram
{
    public static void Main()
    {
        int س = 5;

        if (س > 3)
        {
            Console.WriteLine("القيمة كبيرة");
        }
    }
}
```

## شكل مولد الكود

يحتوي `CSharpCodeGenerator` على دالة رئيسية شبيهة بالتالي:

```csharp
public GeneratedCodeResult Generate(ProgramNode program)
{
    // يكتب using وclass وMain، ثم يزور كل StatementNode.
}
```

ويتفرع داخلياً إلى دوال مثل:

```text
GenerateStatement(StatementNode statement)
GenerateExpression(ExpressionNode expression)
GenerateIf(IfNode ifNode)
GenerateWhile(WhileNode whileNode)
GenerateBlock(BlockNode block)
```

## تعديل الواجهة

يستبدل حدث زر **تشغيل** بهذا المنطق:

```text
Lexer → Parser → Semantic Analyzer
    ↓ عند عدم وجود أخطاء
CSharpCodeGenerator
    ↓
عرض GeneratedProgram.cs في تبويب الناتج
    ↓
حفظه في مجلد Generated
```

يفضل تغيير اسم الزر إلى:

```text
توليد كود C#
```

وإضافة زر اختياري:

```text
حفظ الكود المولد
```

## هل يجب توليد EXE؟

ليس بالضرورة في النسخة الجامعية الأولى. **توليد ملف C# سليم ومنظم** يثبت وجود مرحلة Code Generation. يمكن فتح الملف المولد في Visual Studio ثم بناءه بصورة طبيعية للحصول على EXE.

إذا طلب الدكتور ملفاً تنفيذياً مباشرة، فهناك خياران لاحقان:

1. إضافة Roslyn (`Microsoft.CodeAnalysis.CSharp`) داخل المشروع لبناء ملف DLL أو EXE برمجياً.
2. إنشاء مشروع C# هدف ثم استخدام `dotnet build` من عملية منفصلة.

> لا يوصى ببدء التوليد المباشر لـAssembly أو IL في هذا المشروع، لأنه سيضيف تعقيداً كبيراً لا يعطي قيمة تعليمية أعلى من توليد C# في نطاق الوقت الحالي.

## التحسين Optimization

مرحلة Optimization مستقلة عن Code Generation. إن كانت مطلوبة، أضيفوا قبل مولد الكود تحسيناً بسيطاً مثل **Constant Folding**:

```text
2 + 3 * 4  →  14
```

لكن إذا لم يطلبها الدكتور صراحة، اكتبوا في التقرير أنها خارج نطاق الإصدار الأول.

## صياغة التقرير بعد التعديل

> يتكون مترجم لغة بيان من المحلل اللغوي، والمحلل النحوي، وبناء شجرة AST، والتحليل الدلالي، ثم مولد كود يحول البرنامج العربي إلى كود C# مكافئ قابل للبناء والتنفيذ. لم ينفذ الإصدار الأول تحسينات للكود المولد، وتعد مرحلة Optimization تطويراً مستقبلياً.
