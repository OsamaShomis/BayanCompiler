# تشخيص أولي لبناء Windows من لقطة Visual Studio

## مصدر الدليل

لقطة شاشة قائمة الأخطاء المرفقة من المستخدم، أبعادها الأصلية `3764 × 533`، قُسمت أفقياً إلى سبعة مقاطع متداخلة للقراءة الدقيقة.

## النتائج المؤكدة من المقاطع 1–2

| الرمز | النص المقروء | الملاحظة |
| --- | --- | --- |
| `CS0579` | `Duplicate 'global::System.Runtime.Versioning.TargetFrameworkAttribute' attribute` | يظهر في مشروع `Bayan.Compiler.Core`. |
| `CS0234` | `The type or namespace name 'Forms' does not exist in the namespace 'System.Windows'` | يظهر في مشروع `Bayan.Compiler.Core`. |
| `CS0234` | `The type or namespace name 'CodeGeneration' does not exist in the namespace 'BayanCompiler'` | يظهر في صف منفصل؛ يلزم قراءة الأعمدة التالية لتثبيت المشروع والملف كاملاً. |
| `CS0006` | `Metadata file ... Bayan.Compiler.Core.dll could not be found` | يظهر في مشروع `Bayan.Compiler.Cli`، وهو أثر لاحق متوقع لفشل بناء Core. |

> لا يُعالج `CS0006` قبل معالجة أخطاء Core الثلاثة السابقة؛ لأن CLI لا يستطيع استهلاك DLL لم تُنشأ بعد.

تشير المقاطع 3–4 أيضاً إلى أن DLL الغائبة تقع في مسار بناء Core تحت `src\Bayan.Compiler.Core\bin\Debug\net6.0\...`، وهو ما يؤكد أن `CS0006` نتيجة مباشرة لفشل Core لا عيب مستقل في مراجع CLI.

## تثبيت المشروع والملف من المقاطع 5–6

| الرمز | المشروع | الملف الظاهر | الاستنتاج المؤقت |
| --- | --- | --- | --- |
| `CS0579` | `Bayan.Compiler.Core` | `.NETCoreApp,Version=v...` | Core يولّد أو يلتقط `TargetFrameworkAttribute` مرتين. |
| `CS0234` (`System.Windows.Forms`) | `Bayan.Compiler.Core` | `BayanCompiler.GlobalUs...` | ملف global usings خاص بالمحرر دخل خطأً في عملية تجميع Core. |
| `CS0234` (`BayanCompiler.CodeGeneration`) | `BayanCompiler` | `MainForm.cs` | MainForm في نسخة المستخدم ما زال يحمل اعتماداً مباشراً على namespace قديم/غير مرجعي. |
| `CS0006` | `Bayan.Compiler.Cli` | `CSC` | أثر تابع لفشل إخراج `Bayan.Compiler.Core.dll`. |

> لا يكفي إصلاح ProjectReference في المحرر وحده: الدليل يقتضي عزل ملفات Windows Forms عن Core، ثم إزالة أو تصحيح الاستيراد القديم في MainForm وفق النسخة الحالية من الملف.

تثبت الخانة الأخيرة في المقطع السابع أيضاً أرقام الأسطر: سطر `4` لملف `.NETCoreApp,Version=v...` المتولد، وسطر `10` لملف `BayanCompiler.GlobalUs...` المتولد، وسطر `1` لـ`MainForm.cs`، وسطر `1` لاستدعاء مترجم CLI. تؤيد هذه المواضع استنتاج سلسلة الفشل أعلاه.

## السبب الجذري

مشروع `Bayan.Compiler.Core` يربط ملفات النواة من مشروع المحرر القديم بواسطة glob واسع هو `../BayanCompiler/**/*.cs`. كان الاستبعاد يزيل `Forms` و`Program.cs` فقط، لكنه لا يزيل `obj` و`bin`. عند البناء على Windows ينشئ مشروع المحرر تحت `obj` ملفي `BayanCompiler.GlobalUsings.g.cs` و`.NETCoreApp,Version=v6.0.AssemblyAttributes.cs`. لذلك حاول Core تجميع ملفات خاصة بـWindows Forms وتوليد assembly attribute مكرر، ففشل قبل إنشاء `Bayan.Compiler.Core.dll`.

أما خطأ `CS0234` في `MainForm.cs` فكان ناتجاً من سطر `using BayanCompiler.CodeGeneration;` قديم وغير مستخدم. بعد الفصل إلى CLI صار MainForm يتعامل مع `CliCompilationClient` ولا يحتاج ذلك الاستيراد. وأخيراً كان `CS0006` في CLI أثراً تالياً فقط لغياب DLL الناتجة من فشل Core.

## الإصلاح المطبق

| الملف | التغيير الأدنى | الغرض |
| --- | --- | --- |
| `src/Bayan.Compiler.Core/Bayan.Compiler.Core.csproj` | إضافة `../BayanCompiler/bin/**/*.cs` و`../BayanCompiler/obj/**/*.cs` إلى `Exclude` في glob المصدر | يمنع Core من التقاط ملفات المحرر المتولدة، مع إبقاء ملفات النواة الحالية مرتبطة كما هي. |
| `src/BayanCompiler/Forms/MainForm.cs` | إزالة `using BayanCompiler.CodeGeneration;` | يزيل اعتماداً قديماً غير مستخدم ويثبت فصل المحرر عن API النواة. |

## نتائج التحقق بعد الإصلاح

| التحقق | النتيجة |
| --- | --- |
| بناء `Bayan.Compiler.Core` في Linux | نجح، دون تحذيرات أو أخطاء. |
| بناء `Bayan.Compiler.Cli` في Linux | نجح. ظهرت تحذيرات `NETSDK1138` فقط لأن `net6.0` خارج فترة الدعم، ولا ترتبط بهذا الإصلاح. |
| تشغيل اختبار LL(1) الرسمي | نجح **25 من 25** باختبار runtime متوافق عبر `DOTNET_ROLL_FORWARD=Major`. |
| بناء `Bayan.Editor.WinForms` من Linux مع `EnableWindowsTargeting=true` | لم يصل إلى تحليل C#؛ توقف عند `MSB4019` بسبب غياب `Microsoft.NET.Sdk.WindowsDesktop.targets` من SDK المحلي. |

> لا يثبت Linux بناء Windows Forms. يتطلب الإغلاق النهائي تنفيذ `Rebuild Solution` وتشغيل المحرر على Visual Studio 2022 في Windows، لأن Windows Desktop SDK غير متاح في بيئة التحقق الحالية.

## إعادة التحقق المطلوبة على Windows

افتح الحل `Bayan.sln` من جذر المشروع، ثم اختر **Build → Clean Solution** يليه **Build → Rebuild Solution** بتكوين `Debug | Any CPU`. يجب أن تنتهي النتيجة من دون `CS0579` أو `CS0234` أو `CS0006`. بعد ذلك شغّل `Bayan.Editor.WinForms` وتحقق أن زر الترجمة يعرض artifacts الفعلية من CLI. إذا ظهرت رسالة جديدة، ينبغي إرسال نافذة **Output → Build** كاملة، لا قائمة الأخطاء وحدها، حتى يُفحص ترتيب MSBuild الفعلي.
