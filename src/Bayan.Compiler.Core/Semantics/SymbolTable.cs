namespace BayanCompiler.Semantics;

/// <summary>
/// يخزن المتغيرات المعرفة في نطاق واحد، ويرجع إلى النطاق الأب عند البحث.
/// </summary>
public sealed class SymbolTable
{
    // يخزن رموز النطاق الحالي مع مقارنة حساسة للاسم كما هو مكتوب في المصدر.
    private readonly Dictionary<string, Symbol> _symbols = new Dictionary<string, Symbol>();

    // يحتفظ بالنطاق الخارجي عند دخول كتلة شرط أو حلقة.
    private readonly SymbolTable? _parent;

    /// <summary>
    /// ينشئ جدول رموز، ويمكن تمرير نطاق أب عند إنشاء كتلة متداخلة.
    /// </summary>
    public SymbolTable(SymbolTable? parent = null)
    {
        // يربط الجدول بالنطاق الخارجي إن وجد.
        _parent = parent;
    }

    /// <summary>
    /// يضيف رمزاً إلى النطاق الحالي فقط، ويعيد false عند تكرار الاسم في النطاق نفسه.
    /// </summary>
    public bool TryDeclare(Symbol symbol)
    {
        // يمنع تعريف المتغير نفسه مرتين في الكتلة ذاتها.
        if (_symbols.ContainsKey(symbol.Name))
        {
            return false;
        }

        // يحفظ الرمز ليكون متاحاً للتعليمات التالية.
        _symbols.Add(symbol.Name, symbol);
        return true;
    }

    /// <summary>
    /// يبحث عن اسم من النطاق الحالي ثم النطاقات الخارجية بالتتابع.
    /// </summary>
    public bool TryLookup(string name, out Symbol symbol)
    {
        // يعيد الرمز فور العثور عليه في النطاق الحالي.
        if (_symbols.TryGetValue(name, out Symbol? foundSymbol))
        {
            // يمرر الرمز إلى المتصل بعد تأكيد العثور عليه.
            symbol = foundSymbol;
            return true;
        }

        // يتابع البحث في النطاق الأب عند وجوده.
        if (_parent is not null)
        {
            return _parent.TryLookup(name, out symbol);
        }

        // يعيد قيمة مؤقتة لأن الاسم غير موجود في أي نطاق.
        symbol = null!;
        return false;
    }

    /// <summary>
    /// ينشئ نطاقاً فرعياً لكتلة تعليمات مستقلة.
    /// </summary>
    public SymbolTable CreateChildScope()
    {
        // يربط النطاق الفرعي بالنطاق الحالي كي يستطيع رؤية المتغيرات الخارجية.
        return new SymbolTable(this);
    }

    /// <summary>
    /// Gets a stable read-only snapshot of symbols declared directly in this scope.
    /// </summary>
    public IReadOnlyList<Symbol> GetCurrentSymbols()
    {
        return _symbols.Values.OrderBy(symbol => symbol.Name, StringComparer.Ordinal).ToList();
    }

    /// <summary>
    /// Formats the current scope as a real symbol-table artifact for the CLI and editor.
    /// </summary>
    public string FormatCurrentScope()
    {
        var builder = new System.Text.StringBuilder();
        builder.AppendLine("# Symbol Table");
        builder.AppendLine("Name | Kind | Type | Read-only | Line | Column");
        builder.AppendLine("--- | --- | --- | --- | --- | ---");

        foreach (Symbol symbol in GetCurrentSymbols())
        {
            builder.AppendLine(symbol.Name + " | " + symbol.Kind + " | " + symbol.Type + " | " + symbol.IsReadOnly + " | " + symbol.DeclarationSpan.Line + " | " + symbol.DeclarationSpan.Column);
        }

        return builder.ToString();
    }
}
