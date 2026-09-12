# تحقق الهدف التنفيذي CIL / ILAsm

## ما نُفّذ

أضيف هدف تنفيذي مستقل بعد TAC. ينتج `CilExecutableGenerator` ملف `program.il` من `IrProgram` نفسه الذي يستهلكه مولد MIPS. ويكتب CLI هذا الملف إلى مجلد artifacts، ثم يحاول `WindowsExecutableBuilder` تشغيل `ilasm.exe` على Windows لإنتاج `program.exe` من دون أن يغيّر نجاح MIPS أو يحذف `program.asm`.

يمتد محرر Compiler Journey إلى ثلاث مراحل جديدة: **CIL / IL** لعرض الملف المتولد، و**Windows EXE** لعرض نجاح أو فشل البناء، و**نتيجة التنفيذ** لعرض stdout الحقيقي بعد الضغط على **تنفيذ EXE**. يبقى زر **تنفيذ MIPS** منفصلاً كي لا يختلط مسارا التنفيذ.

## الفحوصات المنفذة

| الفحص | النتيجة |
| --- | --- |
| `dotnet build Bayan.Compiler.Core` | نجح بلا تحذيرات أو أخطاء. |
| `dotnet build Bayan.Compiler.Cli` | نجح؛ ظهرت تحذيرات `NETSDK1138` فقط لأن `net6.0` خارج الدعم. |
| مجموعة اختبارات LL(1) الرسمية | نجحت **26 من 26**، وتتضمن اختبار هدف CIL. |
| CLI على `12_official_basic_execution.bayan` | كتب `program.asm` و`program.il` وmanifest يعلن `CilAvailable = true`. |
| CIL إلى EXE للعينة الرسمية | جُمع `program.il` محلياً عبر ILAsm متوافق وشُغّل EXE؛ النتيجة `15`. |
| حالة `10 + 30` | مرّت عبر بيان ثم TAC ثم CIL ثم EXE؛ stdout يساوي `40`. |
| عينة صيغتها قديمة | لا تعتمد كاختبار رسمي؛ عينات القبول هي 11–25. |

## نطاق الإصدار الأول

يدعم هدف CIL في هذه الدفعة التخزين المفرد للأنواع `صحيح` و`منطقي` و`حرفي`، والإسناد والحساب الصحيح والمقارنة والمنطق والتفرع والحلقات والطباعة، إضافة إلى `اقرأ` الصحيح في مستوى IL. أما الإجراءات والقوائم والسجلات والحقيقي والخيط الرمزي وإدخال الخيط وبقية قدرات MIPS فلا يدعمها هدف EXE بعد؛ يعيد المولد تشخيص `CIL001` لهذا الهدف فقط، مع بقاء نتائج LL(1) وTAC وMIPS متاحة.

## تحقق Windows المطلوب

> لم يُبنَ أو يُشغَّل محرر Windows Forms أو `ilasm.exe` الخاص بـVisual Studio في هذه البيئة؛ التحقق النهائي يتطلب Windows وVisual Studio 2022.

1. افتح **Developer Command Prompt for Visual Studio 2022** ونفذ `where ilasm`.
2. إن لم يكن `ilasm.exe` على `PATH`، عيّن متغير المستخدم `BAYAN_ILASM_PATH` إلى مساره الكامل.
3. افتح `Bayan.sln` ثم نفذ **Clean Solution** و**Rebuild Solution**.
4. شغّل `Bayan.Editor.WinForms`، ثم استخدم برنامجاً رسمياً يجمع 10 و30 ويطبع المتغير.
5. اضغط **تحليل وترجمة**، وتحقق من ظهور `program.il` في مرحلة CIL.
6. افتح مرحلة **Windows EXE** وتحقق من وجود مسار `program.exe` بدلاً من `EXE001` أو `EXE002`.
7. اضغط **تنفيذ EXE** وتحقق من ظهور `40` في مرحلة **نتيجة التنفيذ**.

## المراجع

1. Microsoft, [Ilasm.exe (IL Assembler)](https://learn.microsoft.com/en-us/dotnet/framework/tools/ilasm-exe-il-assembler): يعرّف ملف `.il` كمدخل ويولد Portable Executable.
2. Microsoft, [ML and ML64 command-line reference](https://learn.microsoft.com/en-us/cpp/assembler/masm/ml-and-ml64-command-line-reference?view=msvc-170): يوضح أن Assembly الأصلي لـx86 يمر عبر MASM، لا ILAsm.
