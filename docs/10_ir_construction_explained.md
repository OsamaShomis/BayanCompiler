# كيف بُنيت مرحلة IR في مترجم بيان

## الفكرة العامة

الـIR، أو **Intermediate Representation**، هو تمثيل وسيط مستقل نسبياً عن لغة بيان وعن MIPS. لا يحاول تمثيل النص العربي كما هو، ولا يكتب سجلات MIPS مباشرة. بدلاً من ذلك، يحول شجرة AST إلى تعليمات بسيطة، ثم يحول مولد MIPS هذه التعليمات إلى Assembly.

```text
برنامج بيان
    ↓
Lexer
    ↓
Parser
    ↓
AST
    ↓
IrGenerator
    ↓
Three-Address IR
    ↓
MipsCodeGenerator
    ↓
MIPS Assembly
```

## الملفات المسؤولة

| الملف | المسؤولية |
|---|---|
| `Intermediate/IrOpcode.cs` | تعريف العمليات الوسيطة مثل `Assign` و`Binary` و`Label` و`Goto` و`IfZeroGoto` و`PrintInt` و`PrintString`. |
| `Intermediate/IrValue.cs` | تمثيل المتغير والقيمة المباشرة والقيمة المؤقتة وعنوان النص. |
| `Intermediate/IrInstruction.cs` | تمثيل تعليمة IR واحدة مع النتيجة والمعاملات والعامل وموقع المصدر. |
| `Intermediate/IrProgram.cs` | جمع التعليمات ومساحات الذاكرة والنصوص. |
| `Intermediate/IrGenerator.cs` | تحويل عقد AST إلى تعليمات IR. |
| `Intermediate/IrPrinter.cs` | عرض IR في تبويب `IR`. |
| `CodeGeneration/MipsCodeGenerator.cs` | تحويل IR إلى MIPS/MARS Assembly. |

## شكل تعليمة IR

تعليمة IR في المشروع ممثلة بالحقول التالية:

```text
Opcode, Result, Left, Right, Operator, TargetLabel, Span
```

فمثلاً العملية:

```بيان
س + 1
```

تتحول إلى:

```text
temp_0 = var_0 + 1
```

حيث إن `var_0` هو اسم MIPS داخلي للمتغير العربي `س`، و`temp_0` قيمة مؤقتة تحفظ نتيجة العملية.

## تحويل التعريف

العبارة:

```بيان
دع س : عدد = 5؛
```

تتحول إلى IR:

```text
var var_0 : Int
var_0 = 5
```

ويحجز مولد MIPS مساحة:

```asm
var_0: .word 0
```

ثم يولد:

```asm
li $t0, 5
sw $t0, var_0
```

## تحويل العملية الحسابية

العبارة:

```بيان
مجموع ← مجموع + س؛
```

قد تتحول إلى:

```text
temp_0 = var_1 + var_0
var_1 = temp_0
```

ثم إلى MIPS:

```asm
lw $t0, var_1
lw $t1, var_0
add $t2, $t0, $t1
sw $t2, temp_0
lw $t0, temp_0
sw $t0, var_1
```

## تحويل المقارنة والشرط

الشرط:

```بيان
إذا (س > 3) {
    اطبع("القيمة كبيرة")؛
}
```

يتحول إلى IR:

```text
temp_0 = var_0 > 3
ifZero temp_0 goto else_0
print_string &str_0
else_0:
```

ثم إلى MIPS تقريبي:

```asm
lw $t0, var_0
li $t1, 3
slt $t2, $t1, $t0
sw $t2, temp_0
lw $t0, temp_0
beq $t0, $zero, else_0
la $a0, str_0
li $v0, 4
syscall
else_0:
```

استخدمنا `slt $t2, $t1, $t0` لتمثيل `س > 3`؛ لأن العبارة المكافئة هي `3 < س`.

## تحويل الحلقة

العبارة:

```بيان
طالما (س <= 5) {
    س ← س + 1؛
}
```

تتحول إلى:

```text
while_0:
temp_0 = var_0 <= 5
ifZero temp_0 goto endwhile_1
temp_1 = var_0 + 1
var_0 = temp_1
goto while_0
endwhile_1:
```

ثم ينشئ مولد MIPS العلامات والقفزات:

```asm
while_0:
...
beq $t0, $zero, endwhile_1
...
j while_0
endwhile_1:
```

## لماذا نستخدم القيم المؤقتة؟

القيم المؤقتة تمنع فقدان نتائج العمليات المركبة. فبدلاً من محاولة تنفيذ التعبير كله في تعليمة واحدة، يقسمه المولد إلى خطوات قصيرة:

```text
س + 2 * 3
```

تصبح:

```text
temp_0 = 2 * 3
temp_1 = س + temp_0
```

وهذا يجعل تحويل IR إلى سجلات وتعليمات MIPS مباشراً ومنظماً.

## خريطة العمليات إلى MIPS

| IR | MIPS |
|---|---|
| `Assign` | `li` أو `lw` ثم `sw` |
| `Plus` | `add` |
| `Minus` | `sub` |
| `Star` | `mult` ثم `mflo` |
| `Slash` | `div` ثم `mflo` |
| `Percent` | `div` ثم `mfhi` |
| `Less` | `slt` |
| `Greater` | `slt` مع عكس الطرفين |
| `LessEqual` | `slt` ثم `xori` |
| `GreaterEqual` | `slt` ثم `xori` |
| `EqualEqual` | `sub` ثم `sltiu` |
| `BangEqual` | `sub` ثم `sltu` |
| `IfZeroGoto` | `beq register, $zero, label` |
| `Goto` | `j label` |
| `PrintInt` | `li $v0, 1` ثم `syscall` |
| `PrintString` | `li $v0, 4` ثم `syscall` |

## تسلسل التنفيذ في الواجهة

عند الضغط على **توليد MIPS**، تنفذ الواجهة الخطوات التالية:

1. يشغل `Lexer` على النص العربي.
2. إذا لم توجد أخطاء، يشغل `Parser` لبناء AST.
3. يشغل `SemanticAnalyzer` لفحص الأسماء والأنواع.
4. يشغل `IrGenerator` لتحويل AST إلى `IrProgram`.
5. يعرض `IrPrinter.Format` في تبويب `IR`.
6. يشغل `MipsCodeGenerator` لتحويل IR إلى نص `.asm`.
7. يعرض النص في تبويب `MIPS Assembly`.
8. يحفظه زر `حفظ ASM` من دون BOM حتى يتعرف JsSpim على `.data` من أول بايت.

بهذا يكون IR مرحلة مستقلة يمكن لاحقاً استخدامها لإضافة Optimization قبل توليد MIPS، من دون تغيير Lexer أو Parser أو Semantic Analyzer.

## عرض الأسماء العربية في IR مع أسماء MIPS الآمنة

أضيف إلى كل `IrValue` حقل اختياري باسم `SourceName`. يحمل هذا الحقل الاسم العربي الذي كتبه المستخدم في برنامج بيان، بينما يبقى الحقل `Name` هو الاسم الداخلي الآمن الذي يستخدمه مولد MIPS.

| الحقل | مثال | الاستخدام |
|---|---|---|
| `SourceName` | `مجموع` | يظهر في تبويب IR والتعليقات لتسهيل القراءة والعرض. |
| `Name` | `var_1` | يظهر في ملف MIPS ضمن `.data` وتعليمات `lw` و`sw`. |

لذلك يظهر تبويب IR بعد التعديل بصيغة مقروءة مثل:

```text
س [var_0] : Int
مجموع [var_1] : Int
مؤقت_0 [temp_0] : Int

مجموع = 0
مؤقت_0 = مجموع + س
مجموع = مؤقت_0
```

لكن يبقى ملف MIPS متوافقاً مع المحاكي:

```asm
# المصدر: س
var_0: .word 0
# المصدر: مجموع
var_1: .word 0

lw $t0, var_1
lw $t1, var_0
add $t2, $t0, $t1
sw $t2, temp_0
```

بهذا لا نستخدم العربية كاسم Assembly، وإنما نستخدمها كاسم عرض وتعليق، وهو تصميم يجمع قابلية القراءة مع التوافق التقني للمحاكي.
