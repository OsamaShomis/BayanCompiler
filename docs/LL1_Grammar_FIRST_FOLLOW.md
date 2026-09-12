# لغة «بيان» — Grammar LL(1) وFIRST وFOLLOW وجدول التحليل

**الغرض:** هذه الوثيقة هي المواصفة الرسمية التي يستخدمها `Ll1Parser` الجديد. وهي لا تضيف خصائص لغوية؛ بل تعيد صياغة قواعد الإصدار الحالي لتصبح مناسبة لمحلل تنبؤي **Table-Driven LL(1)**.

> في هذه الوثيقة تكتب أسماء الرموز الطرفية بأسماء `TokenType` البرمجية، مثل `Program` و`Identifier` و`Semicolon`، حتى يمكن مطابقتها مباشرة مع ما ينتجه `Lexer`.

## 1. تهيئة Grammar إلى LL(1)

كانت قواعد التعبيرات بصيغة EBNF تستخدم التكرار `{ ... }` والاختيار `[ ... ]`. يحولها المحلل إلى Non-terminals لاحقة مثل `OrTail` و`AddTail`. هذا يحافظ على أولوية العمليات ويزيل التكرار اليساري من القواعد، وهو شرط رئيسي للمحلل LL(1).

| الحالة | صيغة EBNF المختصرة | صيغة LL(1) المكافئة |
|---|---|---|
| تعليمات متعددة | `{ التعليمة }` | `StatementList → Statement StatementList | ε` |
| تهيئة اختيارية | `[ "=" Expression ]` | `InitializerOpt → Assign Expression | ε` |
| فرع وإلا | `[ "وإلا" Block ]` | `ElsePart → Else Block | ε` |
| سلسلة `||` | `And { "||" And }` | `Or → And OrTail` و`OrTail → OrOr And OrTail | ε` |
| سلسلة الجمع | `Multiplication { (+ | -) Multiplication }` | `Addition → Multiplication AddTail` و`AddTail → Plus Multiplication AddTail | Minus Multiplication AddTail | ε` |

## 2. القواعد النهائية بصيغة BNF

```text
Start              → ProgramNode Eof
ProgramNode        → Program Identifier StatementList End
StatementList      → Statement StatementList | ε
Statement          → Declaration | Assignment | PrintStatement | IfStatement | WhileStatement | Block

Declaration        → Let Identifier Colon Type InitializerOpt Semicolon
InitializerOpt     → Assign Expression | ε
Type               → TypeInt | TypeReal | TypeBool | TypeText
Assignment         → Identifier Assign Expression Semicolon
PrintStatement     → Print LeftParen Expression RightParen Semicolon
IfStatement        → If LeftParen Expression RightParen Block ElsePart
ElsePart           → Else Block | ε
WhileStatement     → While LeftParen Expression RightParen Block
Block              → LeftBrace StatementList RightBrace

Expression         → Or
Or                 → And OrTail
OrTail             → OrOr And OrTail | ε
And                → Equality AndTail
AndTail            → AndAnd Equality AndTail | ε
Equality           → Comparison EqualityTail
EqualityTail       → EqualEqual Comparison EqualityTail | BangEqual Comparison EqualityTail | ε
Comparison         → Addition ComparisonTail
ComparisonTail     → Less Addition ComparisonTail | LessEqual Addition ComparisonTail
                  | Greater Addition ComparisonTail | GreaterEqual Addition ComparisonTail | ε
Addition           → Multiplication AdditionTail
AdditionTail       → Plus Multiplication AdditionTail | Minus Multiplication AdditionTail | ε
Multiplication     → Unary MultiplicationTail
MultiplicationTail → Star Unary MultiplicationTail | Slash Unary MultiplicationTail
                  | Percent Unary MultiplicationTail | ε
Unary              → Bang Unary | Minus Unary | Primary
Primary            → Integer | Real | String | True | False | Identifier | LeftParen Expression RightParen
```

## 3. مجموعات FIRST

| Non-terminal | FIRST |
|---|---|
| `Start` | `{ Program }` |
| `ProgramNode` | `{ Program }` |
| `StatementList` | `{ Let, Identifier, Print, If, While, LeftBrace, ε }` |
| `Statement` | `{ Let, Identifier, Print, If, While, LeftBrace }` |
| `Declaration` | `{ Let }` |
| `InitializerOpt` | `{ Assign, ε }` |
| `Type` | `{ TypeInt, TypeReal, TypeBool, TypeText }` |
| `Assignment` | `{ Identifier }` |
| `PrintStatement` | `{ Print }` |
| `IfStatement` | `{ If }` |
| `ElsePart` | `{ Else, ε }` |
| `WhileStatement` | `{ While }` |
| `Block` | `{ LeftBrace }` |
| `Expression`, `Or`, `And`, `Equality`, `Comparison`, `Addition`, `Multiplication`, `Unary` | `{ Bang, Minus, Integer, Real, String, True, False, Identifier, LeftParen }` |
| `OrTail` | `{ OrOr, ε }` |
| `AndTail` | `{ AndAnd, ε }` |
| `EqualityTail` | `{ EqualEqual, BangEqual, ε }` |
| `ComparisonTail` | `{ Less, LessEqual, Greater, GreaterEqual, ε }` |
| `AdditionTail` | `{ Plus, Minus, ε }` |
| `MultiplicationTail` | `{ Star, Slash, Percent, ε }` |
| `Primary` | `{ Integer, Real, String, True, False, Identifier, LeftParen }` |

## 4. مجموعات FOLLOW

| Non-terminal | FOLLOW |
|---|---|
| `Start` | `{ Eof }` |
| `ProgramNode` | `{ Eof }` |
| `StatementList` | `{ End, RightBrace }` |
| `Statement`, `Declaration`, `Assignment`, `PrintStatement`, `IfStatement`, `ElsePart`, `WhileStatement` | `{ Let, Identifier, Print, If, While, LeftBrace, End, RightBrace }` |
| `InitializerOpt` | `{ Semicolon }` |
| `Type` | `{ Assign, Semicolon }` |
| `Block` | `{ Else, Let, Identifier, Print, If, While, LeftBrace, End, RightBrace }` |
| `Expression`, `Or`, `OrTail` | `{ Semicolon, RightParen }` |
| `And`, `AndTail` | `{ OrOr, Semicolon, RightParen }` |
| `Equality`, `EqualityTail` | `{ AndAnd, OrOr, Semicolon, RightParen }` |
| `Comparison`, `ComparisonTail` | `{ EqualEqual, BangEqual, AndAnd, OrOr, Semicolon, RightParen }` |
| `Addition`, `AdditionTail` | `{ Less, LessEqual, Greater, GreaterEqual, EqualEqual, BangEqual, AndAnd, OrOr, Semicolon, RightParen }` |
| `Multiplication`, `MultiplicationTail` | `{ Plus, Minus, Less, LessEqual, Greater, GreaterEqual, EqualEqual, BangEqual, AndAnd, OrOr, Semicolon, RightParen }` |
| `Unary`, `Primary` | `{ Star, Slash, Percent, Plus, Minus, Less, LessEqual, Greater, GreaterEqual, EqualEqual, BangEqual, AndAnd, OrOr, Semicolon, RightParen }` |

## 5. جدول التحليل LL(1) المختصر

يسجل الجدول التالي الخلية غير الفارغة فقط. عندما لا توجد خلية للزوج `(Non-terminal, Lookahead)` يسجل المحلل الخطأ `SYN001` ويبدأ استرداداً محدوداً وفق FOLLOW للقاعدة الحالية.

| Non-terminal | Lookahead | Production المختارة |
|---|---|---|
| `Start` | `Program` | `Start → ProgramNode Eof` |
| `ProgramNode` | `Program` | `ProgramNode → Program Identifier StatementList End` |
| `StatementList` | `Let, Identifier, Print, If, While, LeftBrace` | `StatementList → Statement StatementList` |
| `StatementList` | `End, RightBrace` | `StatementList → ε` |
| `Statement` | `Let` | `Statement → Declaration` |
| `Statement` | `Identifier` | `Statement → Assignment` |
| `Statement` | `Print` | `Statement → PrintStatement` |
| `Statement` | `If` | `Statement → IfStatement` |
| `Statement` | `While` | `Statement → WhileStatement` |
| `Statement` | `LeftBrace` | `Statement → Block` |
| `InitializerOpt` | `Assign` | `InitializerOpt → Assign Expression` |
| `InitializerOpt` | `Semicolon` | `InitializerOpt → ε` |
| `ElsePart` | `Else` | `ElsePart → Else Block` |
| `ElsePart` | `Let, Identifier, Print, If, While, LeftBrace, End, RightBrace` | `ElsePart → ε` |
| `OrTail` | `OrOr` | `OrTail → OrOr And OrTail` |
| `OrTail` | `Semicolon, RightParen` | `OrTail → ε` |
| `AndTail` | `AndAnd` | `AndTail → AndAnd Equality AndTail` |
| `AndTail` | `OrOr, Semicolon, RightParen` | `AndTail → ε` |
| `EqualityTail` | `EqualEqual` أو `BangEqual` | إنتاج العملية المطابق ثم `EqualityTail` |
| `EqualityTail` | `AndAnd, OrOr, Semicolon, RightParen` | `ε` |
| `ComparisonTail` | أحد `Less, LessEqual, Greater, GreaterEqual` | إنتاج المقارنة المطابق ثم `ComparisonTail` |
| `ComparisonTail` | ما في FOLLOW(`ComparisonTail`) | `ε` |
| `AdditionTail` | `Plus` أو `Minus` | إنتاج الجمع أو الطرح المطابق ثم `AdditionTail` |
| `AdditionTail` | ما في FOLLOW(`AdditionTail`) | `ε` |
| `MultiplicationTail` | `Star` أو `Slash` أو `Percent` | إنتاج العامل المطابق ثم `MultiplicationTail` |
| `MultiplicationTail` | ما في FOLLOW(`MultiplicationTail`) | `ε` |
| `Unary` | `Bang` أو `Minus` | إنتاج العامل الأحادي المطابق ثم `Unary` |
| `Primary` | `Integer, Real, String, True, False, Identifier` | إنتاج القيمة أو المعرف المطابق |
| `Primary` | `LeftParen` | `LeftParen Expression RightParen` |

## 6. برهان عدم وجود تعارضات

اختيارات `Statement` تبدأ بـTokens مختلفة: `Let` للتعريف، `Identifier` للإسناد، `Print` للطباعة، `If` للشرط، `While` للحلقة، و`LeftBrace` للكتلة. لذلك لا تتقاطع مجموعات FIRST الخاصة بها. وفي القواعد ذات `ε`، مثل `InitializerOpt` و`ElsePart` وTails، لا يتقاطع FIRST للبديل غير الفارغ مع FOLLOW للقاعدة؛ لذلك لا توجد خانة جدول تستقبل إنتاجين مختلفين.

## 7. سياسة AST والتشخيصات

المحلل Table-Driven يبني أولاً شجرة اشتقاق (Concrete Parse Tree) باستخدام Stack وجدول. بعد النجاح أو الاسترداد المحدود من الخطأ، يحول ممر مستقل هذه الشجرة إلى نفس عقد AST الحالية: `ProgramNode` و`VarDeclarationNode` و`AssignmentNode` و`PrintNode` و`IfNode` و`WhileNode` و`BlockNode` وعقد التعبيرات. هذا الفصل مهم: **التحليل Table-Driven، أما المرور اللاحق لبناء AST فليس محللاً نحوياً جديداً.**

يحافظ التنفيذ على `ParseResult` نفسه وعلى التشخيصات `SYN001` و`SYN002` و`SYN003` ليبقى `MainForm` و`SemanticAnalyzer` و`IrGenerator` و`MipsCodeGenerator` متوافقة.
