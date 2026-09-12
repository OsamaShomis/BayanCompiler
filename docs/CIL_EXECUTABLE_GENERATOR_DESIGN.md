# تصميم مولد CIL التنفيذي

## مسار البيانات

```text
OfficialBayanCompilationService
  → IrGenerator
  → IrProgram
  → MipsCodeGenerator        → program.asm
  → CilExecutableGenerator   → program.il
  → WindowsExecutableBuilder → program.exe
```

لا يستبدل CIL مولد MIPS؛ كلاهما يستهلك `IrProgram` نفسه، ولذلك يبقى TAC هو التمثيل الوسيط المشترك.

## ملفات النتائج

| الملف | مصدره | متى يوجد؟ |
| --- | --- | --- |
| `program.asm` | `MipsCodeGenerator` | عند نجاح هدف MIPS. |
| `program.il` | `CilExecutableGenerator` | عند نجاح خفض TAC إلى CIL. |
| `program.exe` | `WindowsExecutableBuilder` عبر `ilasm.exe` | على Windows فقط، عند العثور على ILAsm ونجاحه. |
| `windows-executable-diagnostics.txt` | استدعاء ILAsm أو EXE | عند فشل التجميع أو التشغيل. |

## نطاق CIL الأولي

يدعم الإصدار الأول العمليات التي تجعل المثال `س = 10 + 30؛ اطبع(س)؛` قابلاً للبناء والتشغيل:

| TAC | CIL المقابل |
| --- | --- |
| `Assign` | `ldc` أو `ldloc` ثم `stloc` |
| `Binary` | `add`, `sub`, `mul`, `div`, `rem`، والمقارنات الصحيحة |
| `Unary` | `neg` أو منطق صحيح بقيم `0/1` |
| `Label`, `Goto`, `IfZeroGoto` | تعليمات labels و`br` و`brfalse` |
| `PrintInt`, `PrintChar`, `PrintReal`, `PrintString` | نداءات `System.Console::WriteLine` أو `Write` |
| `ReadInt` | `System.Console::ReadLine` و`Int32::Parse` |

أي IR لإجراءات أو قوائم أو سجلات أو خيط إدخال أو أنواع غير مكتملة في هذا المسار يوقف **هدف CIL فقط** بتشخيص `CIL001` مع بقاء artifacts التحليل وMIPS المتاحة.

## بنية ملف IL

المولد يكتب ملفاً مستقلاً عن C# يحتوي `.assembly extern mscorlib`، و`.assembly Bayan.Generated`، وفئة `BayanProgram`، ودالة `Main` تحمل `.entrypoint`. تستخدم المتغيرات والقيم المؤقتة `locals` بحيث لا يتسرب الاسم العربي إلى identifier في IL، بينما يبقى النص العربي المطبوع محفوظاً من IR.

## فصل مسؤوليات Windows

`CilExecutableGenerator` يعمل على أي نظام لأنه لا يستدعي أدوات Windows. أما `WindowsExecutableBuilder` فيستدعي `ilasm.exe` على Windows فقط، عبر متغير البيئة `BAYAN_ILASM_PATH` أو أمر `ilasm` المتاح من Visual Studio Developer Command Prompt. وبهذا يمكن اختبار توليد `program.il` في Linux واختبار بناء EXE وتشغيله لاحقاً في Windows.
