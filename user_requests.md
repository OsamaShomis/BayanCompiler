<USER_REQUEST>
قم بفهم هيكلية المشروف=ع كاملا
</USER_REQUEST>
<ADDITIONAL_METADATA>
The current local time is: 2026-09-01T00:06:39+03:00.
</ADDITIONAL_METADATA>
<USER_SETTINGS_CHANGE>
The user changed setting `Model Selection` from None to Gemini 3.1 Pro (Low). No need to comment on this change if the user doesn't ask about it. If reporting what model you are, please use a human readable name instead of the exact string.
</USER_SETTINGS_CHANGE>

<USER_REQUEST>
بعد عمل اي رسالة لك سوي خطة وانا اواقف عليها :
أنت مهندس برمجيات مكلَّف بتطوير مشروع مترجم لغة «بيان» (C# / .NET 6) ليطابق تعليمات الدكتور الرسمية تماماً. لديك وصول كامل إلى الكود المصدري. نفّذ كل ما يلي بنفسك مباشرة على الكود (لا تكتفِ باقتراح الخطوات)، والتزم حرفياً بكل شرط "إلزامي" أدناه دون تخفيف أو تفسير مرن له.

# السياق التقني الحالي (اقرأه قبل أي تعديل)

المشروع يتكون من ثلاثة مشاريع C#:
- `Bayan.Compiler.Core` — منطق الترجمة (Lexer/Parser/Semantics/IR/CodeGen).
- `Bayan.Compiler.Cli` — مترجم سطر أوامر مستقل.
- `BayanCompiler` — محرر Windows Forms، ملفه الرئيسي الحالي `Forms/CompilerJourneyForm.cs`.

خط الأنابيب الحالي:
```
.bayan → Lexer → DoctorLl1Parser (LL(1)) → AST → DoctorSemanticAnalyzer
       → IrGenerator (Three-Address Code) → MipsCodeGenerator → program.asm
       → (تنفيذ خارجي عبر SPIM أو MARS)
```

**المشكلة الجوهرية التي يجب حلّها:** الدكتور يطلب أن ينتج المترجم **ملف EXE حقيقي قابل للتشغيل المباشر عند نجاح الترجمة**. الناتج الحالي (`program.asm` بصيغة MIPS، يحتاج محاكي SPIM/MARS خارجي غير مضمّن) **لا يحقق هذا الشرط إطلاقاً**.

# الآلية المرجعية التي طلبها الدكتور حرفياً — التزم بها كما هي

> 2. توليد كود لغة التجميع (x86 Assembly) وإنتاج برنامج تنفيذي حقيقي (output.exe) باستخدام مترجم `ilasm`.
>
> **الكلاس الثاني: AssemblyGenerator.cs**
> - يستقبل قائمة تعليمات الـ TAC.
> - يقوم بتوليد كود أسمبلي x86 قياسي وتخزينه في ملف `output.asm`.
> - يقوم بتوليد كود IL وتجميعه تلقائياً عبر `ilasm.exe` (هذا البرنامج افتراضياً موجود في windows) إلى ملف تنفيذي `output.exe`.

**فهمك التقني الإلزامي لهذه الآلية (لا تُعِد اختراعها):**
- `output.asm` نص عرضي/توثيقي فقط بصيغة تشبه x86 — **لا يُجمَّع ولا يُنفَّذ أبداً**. دوره الوحيد تلبية بند "Assembly Code" وعرضه في المحرر والتقرير.
- `output.exe` هو الناتج التنفيذي الحقيقي، عبر مسار منفصل: TAC → كود IL نصي (Common Intermediate Language) → استدعاء `ilasm.exe` كعملية نظام خارجية → ملف PE تنفيذي حقيقي.
- `ilasm.exe` أداة تأتي مع Windows/.NET Framework، **وليس محاكياً خارجياً يجب تثبيته** (بخلاف SPIM/MARS الحالي) — يحقق شرط "Portable تعمل دون الحاجة إلى تثبيت".

---

# أولاً: مهام يجب تنفيذها (إلزامية)

## 1. أنشئ `AssemblyGenerator.cs` بمسارين منفصلين تماماً

أنشئ `src/BayanCompiler/CodeGeneration/AssemblyGenerator.cs` يستقبل `IrProgram` (نفس مدخل `MipsCodeGenerator` الحالي).

> ⚠️ **شرط إلزامي غير قابل للتفاوض: اجعل المولّد عاماً (Generic) على مستوى تعليمات IR، وليس مخصصاً لأي برنامج أو مثال بعينه.**
> لا تكتب أي كود يتعرّف على اسم برنامج معيّن، أو محتوى مصدر مطابق لعينة موجودة في `samples/`، أو أي شرط من نوع "إذا كان اسم المتغير كذا فافعل كذا". ابنِ `AssemblyGenerator` عبر **معالجة عامة لكل قيمة من `IrOpcode`** فقط (وليس عبر قراءة نص المصدر أو مطابقة نمط برنامج بعينه)، بحيث يعمل تلقائياً مع **أي برنامج بيان صحيح لم يُختبَر من قبل**، طالما أنتج `IrGenerator` له IR سليماً. المرجع الوحيد المسموح لبناء المولّد هو تعريف `IrInstruction`/`IrOpcode` نفسه.
>
> **غطِّ جميع تعليمات IR بلا استثناء** (من `Intermediate/IrOpcode.cs`):
> `Assign`, `Binary` (حسابي/مقارنة/منطقي)، `Unary` (سالب/نفي)، `Label`، `Goto`، `IfZeroGoto`، `PrintInt`، `PrintString`، `ReadInt`، `Call`، `Return`، `PrintChar`، `ReadChar`، `LoadIndexed`/`StoreIndexed` (عناصر القوائم وحقول السجلات)، `PrintReal`، `ReadReal`، `ReadString`.
> أي تعليمة غير مغطّاة تعني فشل أي برنامج يستخدمها — هذا مرفوض.

**أ) مسار العرض النصي (`output.asm`):**
- حوّل تعليمات TAC إلى نص أسمبلي بأسلوب x86 قياسي (`mov`, `add`, `sub`, `cmp`, `jmp`, `je`/`jne`, `call`...) لكل بند من IR.
- هذا الملف **لا يُشغَّل** — فقط يُكتب ويُعرض في مرحلة "Assembly Code" بالمحرر وملحق التقرير.

**ب) مسار التنفيذ الحقيقي (IL → EXE):**
- ترجم نفس تعليمات TAC إلى **كود IL نصي صحيح** (`.assembly`, `.method`, `ldc.i4`, `stloc`, `ldloc`, `call`, `brtrue.s`, `br.s`, `ret`...) يطابق بنية ملف `.il` قابل للتجميع.
- اكتب الكود في ملف `.il` مؤقت.
- ابحث عن `ilasm.exe` (مسارات معروفة على Windows مثل `C:\Windows\Microsoft.NET\Framework\v4.0.30319\ilasm.exe` أو `Framework64`)، واجعل المسار قابلاً للضبط عبر متغير بيئة `BAYAN_ILASM_PATH`، لكن لا تعتبره اختيارياً — هو المسار الأساسي وليس بديلاً.
- شغّل `ilasm.exe` كعملية خارجية (`Process.Start`) مع الوسيطة المناسبة لإنتاج EXE (وليس DLL)، والتقط الخرج/الخطأ ورمز الخروج.
- الناتج النهائي: **`output.exe` حقيقي**. اجعل زر "تنفيذ" الحالي في المحرر يشغّله مباشرة كعملية Windows عادية (`Process.Start`) بدل استدعاء `MipsRuntimeClient`، ويعرض `stdout`/`stderr` الحقيقيين في مرحلة "نتيجة التنفيذ" بنفس أسلوب معالجة الأخطاء الحالي (حدود زمنية، عدم الادّعاء بنجاح وهمي).

## 2. عدّل `OfficialBayanCompilationService.cs` والـ CLI

- أضف حقلين جديدين لنتيجة الترجمة: `AssemblyDisplayCode` (نص x86 للعرض) و`ExecutablePath` (مسار `output.exe` بعد التجميع بنجاح).
- عدّل `Bayan.Compiler.Cli/Program.cs` ليكتب `output.asm` و`output.exe` داخل مجلد النتائج، ويضيفهما إلى `manifest.json`.
- اجعل نجاح الترجمة الكامل معلَّقاً على وجود `output.exe` فعلياً على القرص، لا فقط على نجاح توليد IR.

## 3. احصر واجهة المحرر على قائمة الدكتور فقط — لا تعرض أي شيء زائد

الدكتور حدد بدقة ما يجب أن يظهر في المحرر، وهو **الحد الأدنى والأقصى معاً**. اجعل مراحل/تبويبات `CompilerJourneyForm.cs` (`StageTitles`/`StageButtonLabels`) تطابق **حرفياً وبالترتيب التالي فقط**:

1. المصدر (كتابة/تعديل الكود)
2. Tokens
3. Parse Tree / Syntax Tree
4. Symbol Table
5. نتائج Semantic Analysis (الأخطاء الدلالية إن وجدت)
6. Three-Address Code
7. Assembly Code (`output.asm`)
8. الأخطاء النحوية والدلالية (Errors)
9. نتيجة تنفيذ البرنامج (`output.exe`)

**احذف من الواجهة المرئية** مرحلتَي **"جدول LL(1)"** و**"تتبع LL(1)"** الموجودتين حالياً (لا وجود لهما في طلب الدكتور). يمكنك إبقاء الكود الذي يولّدهما داخلياً لغرض تعليمي، لكن لا تعرضه في الواجهة ولا في manifest المعروض للمستخدم.

تأكد أن أزرار الملفات الأساسية موجودة بالضبط كما طلب الدكتور: إنشاء ملف جديد، فتح ملف موجود، حفظ الملفات، تنفيذ/تشغيل البرنامج — بدون أزرار إضافية غير مذكورة في التعليمات.

## 4. أعِد تصميم تخطيط الواجهة بحيث لا تتداخل أي عناصر إطلاقاً

هذا شرط جودة إلزامي منفصل عن مطابقة القائمة. راجع `CompilerJourneyForm.cs` (وأي Form آخر تبقيه) والتزم بما يلي:

- **ممنوع تماماً** وضع عنصرين بـ`Dock = Fill` أو `Dock = Top` في نفس الحاوية دون صف/عمود صريح يفصل بينهما — استخدم `TableLayoutPanel` بصفوف/أعمدة محددة الحجم (`RowStyles`/`ColumnStyles`) لكل منطقة رئيسية (الترويسة، شريط الأوامر، منطقة المحتوى، شريط مراحل الرحلة)، بحيث تستضيف كل خلية عنصراً واحداً فقط.
- **شريط الأوامر (الأزرار العلوية):** استخدم `FlowLayoutPanel` مع `AutoScroll = true` و`WrapContents = false` (أو `true` إن سمحت المساحة بالتفاف الأزرار لصف جديد بدل تراكبها)، وحدّد `MinimumSize`/`Padding` لكل زر بحيث لا يتقلص نصّه أو يتراكب مع الزر المجاور عند تصغير النافذة.
- **شريط أزرار المراحل التسعة (`stageButtons`):** بعد حذف "جدول LL(1)" و"تتبع LL(1)" (البند 3 أعلاه) سيصبح العدد 9 بدل 10 — أعد حساب العرض المتاح لكل زر بدل الاعتماد على قيم ثابتة قد تسبب تراكباً؛ استخدم توزيعاً نسبياً (`Percent` في `ColumnStyles`) أو `FlowLayoutPanel` قابل للتمرير أفقياً بدل عرض ثابت لكل زر.
- **اختبر التخطيط عند ثلاثة أحجام نافذة على الأقل** (الحد الأدنى `MinimumSize`، حجم متوسط، وحجم كبير/ملء الشاشة) وتأكد بصرياً (أو عبر منطق حساب المساحة) أن لا نص يُقصّ (Clip) ولا زر يغطي زراً آخر ولا يتجاوز حدود حاويته.
- استفد من التوثيق الموجود مسبقاً في `docs/NO_OVERLAP_LAYOUT_OPTIONS.md` و`docs/COMPILER_JOURNEY_DESIGN_SYSTEM.md` كمرجع للمسافات والألوان والمقاييس المعتمدة، وحافظ على تناسقها بدل ابتكار نظام جديد.
- حافظ على دعم RTL الكامل (`RightToLeft = RightToLeft.Yes`, `RightToLeftLayout = true`) في كل تعديل تخطيطي جديد.

## 5. أضف دعم الإدخال التفاعلي (`اقرأ`) عند التنفيذ

بما أن `output.exe` عملية Windows حقيقية (وليس محاكي MIPS محدود)، أضف تمرير إدخال المستخدم لـ`stdin` العملية من داخل المحرر عند وجود تعليمات `اقرأ` في البرنامج — كان هذا قيداً موثقاً صراحة سابقاً، ويجب حله الآن لأن الآلية الجديدة تسمح به بسهولة.

## 6. أضف أمثلة الأخطاء الرسمية الناقصة

أضف إلى `samples/` عينتين جديدتين على الأقل **بمفردات القواعد الرسمية النشطة فقط**:
- `samples/26_official_syntax_error.bayan` — خطأ نحوي واضح، يجب أن يظهر برمز `SYN` من `DoctorLl1Parser`.
- `samples/27_official_semantic_error.bayan` — خطأ دلالي واضح، يجب أن يظهر برمز `SEM` من `DoctorSemanticAnalyzer`.

شغّلهما فعلياً عبر `dotnet run --project src/Bayan.Compiler.Cli` وتأكد أن `diagnostics.txt` يحتوي الرمز المتوقع، وأضف تأكيدات (`Assert`) لهما في `tests/BayanCompiler.Ll1Tests/Program.cs`.

## 7. انشر نسخة Portable حقيقية

عدّل أمر النشر لكل من `Bayan.Compiler.Cli` و`BayanCompiler` إلى:
```
dotnet publish -c Release -r win-x64 --self-contained true -p:PublishSingleFile=true
```
بدلاً من `--self-contained false` الحالي، حتى يعمل الملف التنفيذي **دون تثبيت .NET Runtime مسبقاً**.

## 8. جهّز التقرير والتسليم النهائي

- حدّث `docs/bayan_compiler_project_report.md` بالكامل ليصف **القواعد الرسمية الحالية** (الثوابت، الأنواع المركبة، القوائم، السجلات، الإجراءات بالقيمة/بالمرجع...) بدلاً من وصف اللغة القديمة v1.
- أضف قسماً بعنوان "الأدوات والمكتبات الجاهزة" يوثّق `ilasm.exe` (الاسم، الغرض، الإصدار، الجزء المستخدم فيه) وأي أداة أخرى فعلية.
- أضف صور فعلية لتنفيذ المشروع (Tokens، Parse Tree، Symbol Table، TAC، Assembly، تنفيذ `output.exe` وناتجه، مثال خطأ نحوي ودلالي).
- بعد معرفة رقم المجموعة `GN`، حوّل التقرير إلى PDF باسم `RPT-GN.pdf`، احزم المصادر بصيغة RAR باسم `PRFL-GN.rar`، وسمِّ الملف التنفيذي النهائي (نسخة المحرر self-contained) باسم `PREXE-GN.exe`.

---

# ثانياً: مهام يجب حذفها (لتفادي الالتباس مع الدكتور)

## 1. احذف الاعتماد الافتراضي على SPIM/MARS من مسار التنفيذ

لا تحذف `MipsCodeGenerator.cs` بالكامل إن احتجته داخلياً، لكن **احذف/عطّل `MipsRuntimeClient.cs` نهائياً كمسار "تنفيذ"**، ولا تُبقِ `program.asm` (ناتج MIPS) أو أي إشارة له ظاهرة في الواجهة أو manifest المعروض — غير مذكور في طلب الدكتور، والاعتماد على محاكي خارجي غير مضمّن خطر فشل حقيقي في التقييم.

## 2. احذف/أرشِف `MainForm.cs` نهائياً

نموذج واجهة قديم استُبدل فعلياً بـ`CompilerJourneyForm.cs`. إبقاؤه يخلق التباساً ويضيف كوداً ميتاً. احذفه من `.sln` أو انقله لمجلد أرشيف صريح خارج `src/`.

## 3. لا تُبقِ مفردات اللغة القديمة (v1) في `Lexer.cs` بصيغة نشطة جزئياً

`LegacyKeywords` غير مفعّلة فعلياً لكن وجودها في نفس الملف يُربك أي فاحص. انقلها لتعليق توثيقي أو ملف أرشيف منفصل، ووضّح في `README.md` أنها غير مستخدمة إطلاقاً.

## 4. احذف/أرشِف عينات القواعد القديمة (01–10)

تستخدم مفردات غير نشطة ولن تعمل مع `DoctorLl1Parser`. احذفها من `samples/` أو انقلها إلى `samples/archive_v1/` مع توضيح صريح في التقرير أنها ليست من ضمن الـ10 أمثلة الرسمية.

---

# معيار القبول النهائي — تحقق منه بنفسك قبل أن تعتبر المهمة منتهية

- [ ] تشغيل ملف `.bayan` صحيح عبر CLI ينتج فعلياً `output.exe` قابلاً للنقر والتشغيل المباشر على Windows نظيف (بدون تثبيت .NET يدوياً).
- [ ] `output.exe` يطبع نفس ناتج البرنامج المتوقع (المجموع `15`، الحلقات، الإجراء بالمرجع...).
- [ ] `output.asm` يُعرض كنص أسمبلي x86 توثيقي بدون أي محاولة تشغيله.
- [ ] المحرر يشغّل `output.exe` مباشرة عند الضغط على "تنفيذ"، بدون أي رسالة عن غياب محاكي MIPS.
- [ ] لا يوجد أي مسار في الكود الحيّ يعتمد على `BAYAN_MARS_JAR`/`BAYAN_SPIM_PATH` كمسار تنفيذ أساسي.
- [ ] واجهة المحرر تعرض **حصراً** المراحل التسع المذكورة في البند 3، وتبويبا "جدول LL(1)" و"تتبع LL(1)" غير ظاهرين إطلاقاً.
- [ ] **لا يوجد أي تداخل بصري بين أي عنصرين** عند اختبار النافذة بثلاثة أحجام مختلفة (أدنى، متوسط، ملء الشاشة) — لا نص مقصوص، لا زر يغطي زراً آخر.
- [ ] `samples/` يحتوي 10 أمثلة رسمية على الأقل، من ضمنها مثال خطأ نحوي ومثال خطأ دلالي بالمفردات الرسمية، وكلها تعمل فعلياً مع المترجم الحالي.
- [ ] **اختبار العمومية:** برنامج بيان جديد كلياً غير موجود في `samples/` (يدمج مثلاً حلقة + إجراء بالمرجع + قائمة معاً) يترجم وينتج `output.exe` يعمل صحيحاً دون أي تعديل يدوي على `AssemblyGenerator.cs`.
- [ ] راجعت `AssemblyGenerator.cs` وتأكدت أن **كل** قيم `IrOpcode` (الثمانية عشر) لها معالجة فعلية، وليس فقط ما تحتاجه العينات 11–25 الحالية.
- [ ] `MainForm.cs` محذوف أو مؤرشف خارج مسار البناء الرسمي.
- [ ] التقرير يصف القواعد الرسمية الحالية فقط، ويحتوي قسم أدوات/مكتبات موثّقاً (بما فيها `ilasm.exe`).
- [ ] ملفات التسليم الثلاثة جاهزة بالتسمية الصحيحة: `RPT-GN.pdf`, `PREXE-GN.exe`, `PRFL-GN.rar`.

لا تعتبر المهمة مكتملة إلا بعد تحقيق كل بند في هذه القائمة فعلياً في الكود، لا وصفاً نظرياً له فقط.
</USER_REQUEST>
<ADDITIONAL_METADATA>
The current local time is: 2026-09-01T00:08:53+03:00.
</ADDITIONAL_METADATA>

Comments on artifact URI: file:///c%3A/Users/ComputerWorled/.gemini/antigravity-ide/brain/aaaaaac0-e370-4610-940f-8c5aa321ce01/implementation_plan.md

The user has approved this document.


<USER_REQUEST>

</USER_REQUEST>
<ADDITIONAL_METADATA>
The current local time is: 2026-09-01T00:12:30+03:00.
</ADDITIONAL_METADATA>

<USER_REQUEST>
اكمل ا
</USER_REQUEST>
<ADDITIONAL_METADATA>
The current local time is: 2026-09-01T00:16:55+03:00.

The user's current state is as follows:
Active Document: e:\BayanCompiler\src\BayanCompiler\CodeGeneration\AssemblyGenerator.cs (LANGUAGE_CSHARP)
Cursor is on line: 1
Other open documents:
- e:\BayanCompiler\src\BayanCompiler\CodeGeneration\AssemblyGenerator.cs (LANGUAGE_CSHARP)
- e:\BayanCompiler\src\Bayan.Compiler.Cli\Program.cs (LANGUAGE_CSHARP)
</ADDITIONAL_METADATA>

