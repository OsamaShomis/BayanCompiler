# قرار الخلفية التنفيذية لـWindows

## الطلب

المطلوب الإضافي هو أن يستقبل مولد الخلفية تعليمات TAC، وينشئ ملفاً قابلاً للتشغيل في Windows، ثم يعرض المحرر ناتج التنفيذ. هذا **مسار خلفي إضافي بعد TAC** ولا يلغي مسار MIPS المطلوب سابقاً.

## عقد الأدوات الصحيح

| المسار | ملف الإدخال | الأداة | ملف الخرج | النتيجة |
| --- | --- | --- | --- | --- |
| MIPS الحالي | `program.asm` بتعليمات MIPS | SPIM أو MARS | لا يحتاج EXE | تنفيذ في محاكي MIPS. |
| CIL / ILAsm | `program.il` بتعليمات CIL وmetadata | `ilasm.exe` | `program.exe` | ملف PE مُدار يعمل عبر .NET Framework. |
| x86 الأصلي | `program.asm` بتعليمات x86 وWindows ABI | MASM `ml.exe` أو `ml64.exe` مع linker | `program.exe` | ملف Windows native. |

`ilasm.exe` يقبل ملف `.il` يحتوي Intermediate Language وmetadata، ويولد Portable Executable؛ لا يقبل تعليمات x86 مثل `mov eax, 1`. أما MASM فيجمع ويربط ملفات Assembly الأصلية. لذلك لا يجوز أن يكتب مولد واحد ملف x86 `.asm` ثم يرسله إلى `ilasm.exe`.

## القرار المبدئي المقترح

بما أن طلب الدكتور المعروض يسمي `ilasm.exe` صراحةً، فالخيار الأكثر اتساقاً هو إضافة **CIL executable backend**:

```text
بيان → AST → TAC → CilExecutableGenerator → program.il → ilasm.exe → program.exe → stdout
```

يُبقي هذا المسار `MipsCodeGenerator` كما هو، ويضيف هدفاً مستقلاً باسم `Windows EXE (CIL)`. لا يدعي أنه x86 Assembly. تبدأ النسخة الأولى بالنطاق القابل للتحقق: تعريفات صحيحة، إسناد، الحساب، و`اطبع`، ثم تتوسع الاختبارات تدريجياً للشرط والحلقات والأنواع الأخرى.

## بديل x86

إذا أكد الدكتور أنه يريد **x86 Assembly حرفياً**، فالمسار الصحيح يكون `TAC → x86 .asm → MASM → EXE`، وهو Backend منفصل يتطلب ABI لـWindows، وتعريفات runtime للطباعة والإدخال، وربطاً. لا ينبغي خلطه مع `ilasm.exe`.

## المصادر

1. Microsoft, [Ilasm.exe (IL Assembler)](https://learn.microsoft.com/en-us/dotnet/framework/tools/ilasm-exe-il-assembler): يعرّف `.il` كمدخل ويولد ملف PE قابل للتنفيذ.
2. Microsoft, [ML and ML64 command-line reference](https://learn.microsoft.com/en-us/cpp/assembler/masm/ml-and-ml64-command-line-reference?view=msvc-170): يعرّف MASM كأداة تجميع وربط لملفات Assembly الأصلية.
