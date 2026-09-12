# تكييف مشروع «بيان» إلى Windows Forms في Visual Studio 2022

## القرار المعماري

ستكون Windows Forms مسؤولة عن **التفاعل مع المستخدم فقط**: كتابة الكود، فتح الملف، حفظه، عرض Tokens وAST والأخطاء والنتيجة. أما منطق المترجم فيبقى داخل مجلدات `Frontend` و`Lexing` و`Parsing` و`Syntax` و`Semantics` و`Runtime`، ولا يوضع داخل أحداث الأزرار في النموذج.

> لا ينبغي أن يحتوي حدث زر مثل `btnAnalyze_Click` على منطق تحليل النص حرفاً بحرف. يجب أن يستدعي `Lexer` أو لاحقاً منسقاً مثل `CompilerService`، ثم يعرض ناتجه فقط.

## نوع المشروع في Visual Studio

عند إنشاء المشروع من البداية اختر:

```text
Create a new project
→ Windows Forms App
→ C#
→ Framework: .NET 8.0 (Windows)
```

إذا كان Visual Studio أو جهاز الجامعة لا يدعم .NET 8، استخدم النسخة المتاحة لديك مثل .NET 6، ثم وحّد الرقم في جميع ملفات المشروع.

## الهيكلية المناسبة لـWindows Forms

```text
BayanCompiler/
├── Forms/
│   ├── MainForm.cs                 # واجهة المستخدم والأحداث البسيطة فقط.
│   └── MainForm.Designer.cs        # عناصر الواجهة التي ينشئها المصمم تلقائياً.
├── Frontend/
│   ├── SourceText.cs               # النص المصدر ومواقع الأسطر.
│   ├── SourceSpan.cs               # السطر والعمود لكل رمز أو خطأ.
│   ├── Diagnostic.cs               # رسالة الخطأ المفردة.
│   └── DiagnosticBag.cs            # قائمة رسائل الأخطاء.
├── Lexing/
│   ├── TokenType.cs                # كل أنواع Tokens.
│   ├── Token.cs                    # الرمز وقيمته وموقعه.
│   └── Lexer.cs                    # تحويل النص إلى Tokens.
├── Parsing/
│   └── Parser.cs                   # تحويل Tokens إلى AST في مرحلة لاحقة.
├── Syntax/
│   ├── AstNode.cs                  # العقدة الأساسية للشجرة.
│   ├── Statements.cs               # عقد العبارات.
│   └── Expressions.cs              # عقد التعابير.
├── Semantics/
│   ├── SymbolTable.cs              # جدول الرموز.
│   └── SemanticAnalyzer.cs         # فحص الأنواع والأسماء.
├── Runtime/
│   └── Interpreter.cs              # تنفيذ AST.
├── Diagnostics/
│   ├── TokenPrinter.cs             # تنسيق قائمة Tokens.
│   └── AstPrinter.cs               # تنسيق AST.
└── Samples/
    └── أمثلة_لغة_بيان.bayan         # ملفات الاختبار.
```

## مكونات الشاشة الرئيسية

| الاسم المقترح | نوع عنصر Windows Forms | الوظيفة |
|---|---|---|
| `txtSourceCode` | `RichTextBox` | كتابة أو عرض برنامج «بيان» المصدر. اضبط `RightToLeft = Yes`. |
| `btnOpen` | `Button` | فتح ملف امتداده `.bayan`. |
| `btnSave` | `Button` | حفظ النص الحالي في ملف `.bayan`. |
| `btnTokens` | `Button` | تشغيل Lexer وعرض Tokens فقط. |
| `btnAnalyze` | `Button` | تشغيل Lexer ثم Parser ثم Semantic Analyzer عند اكتمالها. |
| `btnRun` | `Button` | تشغيل البرنامج بعد نجاح التحليل. يبقى معطلاً مؤقتاً. |
| `txtTokens` | `RichTextBox` للقراءة فقط | عرض Tokens أو AST مستقبلاً. |
| `txtDiagnostics` | `RichTextBox` للقراءة فقط | عرض الأخطاء مع السطر والعمود. |
| `txtOutput` | `RichTextBox` للقراءة فقط | عرض مخرجات `اطبع`. |
| `lblStatus` | `Label` أو `StatusStrip` | عرض حالة العملية مثل «تم التحليل بنجاح». |

## تخطيط واجهة بسيط ومناسب

قسّم النافذة إلى قسمين رأسيين باستخدام `SplitContainer`. في اليسار ضع `txtSourceCode`. في اليمين ضع `TabControl` بثلاث علامات تبويب: **Tokens** و**الأخطاء** و**الناتج**. ضع أزرار فتح وحفظ وتحليل وتشغيل أعلى النموذج داخل `ToolStrip` أو `Panel`.

## تدفق العمل في المرحلة الحالية

```text
المستخدم يكتب الكود في txtSourceCode
        │
        ▼
يضغط زر «عرض Tokens»
        │
        ▼
MainForm يستدعي Lexer على txtSourceCode.Text
        │
        ├── عند وجود Tokens: تعرض في txtTokens
        └── عند وجود أخطاء: تعرض في txtDiagnostics
```

في هذه المرحلة لا يقرأ `Lexer` مباشرة من `RichTextBox` ولا يعرف شيئاً عن `Button` أو `Form`. يأخذ فقط قيمة `string`، ويعيد Tokens وتشخيصات. هذا الفصل يجعل اختبار `Lexer` سهلاً ويمنع ارتباط منطق المترجم بالواجهة.

## إعدادات عربية موصى بها للنموذج

| الخاصية | القيمة المقترحة | السبب |
|---|---|---|
| `MainForm.RightToLeft` | `Yes` | مناسبة للنص العربي العام. |
| `MainForm.RightToLeftLayout` | `true` | تجعل توزيع واجهة النموذج ملائماً للعربية. |
| `txtSourceCode.Font` | خط يدعم العربية مثل Segoe UI أو Tahoma | وضوح الكلمات المحجوزة العربية. |
| `txtSourceCode.AcceptsTab` | `true` | يسهل كتابة الكتل البرمجية. |
| `txtSourceCode.WordWrap` | `false` | يمنع التفاف السطور البرمجية ويثبت مواقع الأعمدة. |
| `txtTokens.ReadOnly` | `true` | يمنع تعديل نتائج التحليل بالخطأ. |
| `txtDiagnostics.ReadOnly` | `true` | يجعل الأخطاء مخرجات فقط. |

## الخطوة التالية بعد تجهيز الواجهة

ابدأ بملفات `SourceSpan.cs` و`Diagnostic.cs` ثم `TokenType.cs` و`Token.cs`. بعد ذلك اكتب `Lexer.cs` واربطه مؤقتاً بزر `btnTokens`. لا تضف Parser أو المفسر قبل أن تظهر Tokens بصورة صحيحة للكلمات العربية والأرقام والسلاسل والتعليقات.
