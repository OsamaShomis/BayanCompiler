namespace BayanCompiler.Runtime;

/// <summary>
/// يخزن قيم المتغيرات أثناء التشغيل ويدعم النطاقات المتداخلة للكتل.
/// </summary>
public sealed class Environment
{
    // يخزن قيم المتغيرات المعرفة في النطاق الحالي.
    private readonly Dictionary<string, RuntimeValue> _values = new Dictionary<string, RuntimeValue>();

    // يحتفظ بالنطاق الخارجي للبحث عن المتغيرات المعرفة قبله.
    private readonly Environment? _parent;

    /// <summary>
    /// ينشئ بيئة تنفيذ، ويمكن ربطها ببيئة خارجية لكتلة متداخلة.
    /// </summary>
    public Environment(Environment? parent = null)
    {
        // يحفظ النطاق الأب عند تنفيذ كتلة داخل شرط أو حلقة.
        _parent = parent;
    }

    /// <summary>
    /// يعرّف متغيراً جديداً في النطاق الحالي مع قيمته الأولى.
    /// </summary>
    public void Define(string name, RuntimeValue value)
    {
        // يضيف الاسم أو يستبدل قيمته في النطاق الحالي فقط.
        _values[name] = value;
    }

    /// <summary>
    /// يبحث عن قيمة متغير في النطاق الحالي ثم النطاقات الخارجية.
    /// </summary>
    public RuntimeValue Get(string name)
    {
        // يعيد القيمة فور العثور عليها في النطاق الحالي.
        if (_values.TryGetValue(name, out RuntimeValue? value))
        {
            return value;
        }

        // يتابع البحث في النطاق الأب عند وجوده.
        if (_parent is not null)
        {
            return _parent.Get(name);
        }

        // يرفع خطأ داخلياً إذا وصل التنفيذ إلى اسم غير معرف بعد نجاح Semantic Analyzer.
        throw new InvalidOperationException("المتغير \"" + name + "\" غير معرّف أثناء التنفيذ.");
    }

    /// <summary>
    /// يحدث قيمة متغير معرّف في أقرب نطاق يحتوي اسمه.
    /// </summary>
    public void Assign(string name, RuntimeValue value)
    {
        // يحدث القيمة عند وجود الاسم في النطاق الحالي.
        if (_values.ContainsKey(name))
        {
            _values[name] = value;
            return;
        }

        // يمرر التحديث إلى النطاق الأب كي تعمل الإسنادات داخل الكتل.
        if (_parent is not null)
        {
            _parent.Assign(name, value);
            return;
        }

        // يرفع خطأ داخلياً عند محاولة تحديث اسم غير معرف.
        throw new InvalidOperationException("المتغير \"" + name + "\" غير معرّف أثناء التنفيذ.");
    }

    /// <summary>
    /// ينشئ بيئة فرعية لتنفيذ كتلة من دون تسريب تعريفاتها إلى الخارج.
    /// </summary>
    public Environment CreateChild()
    {
        // يربط البيئة الجديدة بالبيئة الحالية.
        return new Environment(this);
    }
}
