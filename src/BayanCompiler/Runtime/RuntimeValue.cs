using System.Globalization;
using BayanCompiler.Semantics;

namespace BayanCompiler.Runtime;

/// <summary>
/// يمثل قيمة فعلية أثناء تنفيذ برنامج بيان مع نوعها الداخلي.
/// </summary>
public sealed class RuntimeValue
{
    /// <summary>
    /// ينشئ قيمة وقت تشغيل من نوع محدد ومحتوى فعلي.
    /// </summary>
    public RuntimeValue(LanguageType type, object value)
    {
        // يحفظ نوع القيمة لاستخدامه عند تنفيذ العمليات.
        Type = type;

        // يحفظ المحتوى الفعلي مثل int أو double أو bool أو string.
        Value = value;
    }

    /// <summary>
    /// نوع القيمة داخل لغة بيان.
    /// </summary>
    public LanguageType Type { get; }

    /// <summary>
    /// المحتوى الفعلي للقيمة في وقت التشغيل.
    /// </summary>
    public object Value { get; }

    /// <summary>
    /// ينشئ قيمة عدد صحيحة.
    /// </summary>
    public static RuntimeValue FromInt(int value)
    {
        // يربط القيمة بالنوع عدد.
        return new RuntimeValue(LanguageType.Int, value);
    }

    /// <summary>
    /// ينشئ قيمة حقيقية.
    /// </summary>
    public static RuntimeValue FromReal(double value)
    {
        // يربط القيمة بالنوع حقيقي.
        return new RuntimeValue(LanguageType.Real, value);
    }

    /// <summary>
    /// ينشئ قيمة منطقية.
    /// </summary>
    public static RuntimeValue FromBool(bool value)
    {
        // يربط القيمة بالنوع منطقي.
        return new RuntimeValue(LanguageType.Bool, value);
    }

    /// <summary>
    /// ينشئ قيمة نصية.
    /// </summary>
    public static RuntimeValue FromText(string value)
    {
        // يربط القيمة بالنوع نص.
        return new RuntimeValue(LanguageType.Text, value);
    }

    /// <summary>
    /// يعيد القيمة بوصفها رقماً حقيقياً لعمليات الحساب والمقارنة.
    /// </summary>
    public double AsNumber()
    {
        // يحول العدد الصحيح إلى double عند الحاجة.
        if (Type == LanguageType.Int)
        {
            return Convert.ToDouble(Value, CultureInfo.InvariantCulture);
        }

        // يعيد القيمة الحقيقية كما هي.
        if (Type == LanguageType.Real)
        {
            return Convert.ToDouble(Value, CultureInfo.InvariantCulture);
        }

        // يمنع استخدام نص أو منطقي كقيمة عددية في Runtime.
        throw new InvalidOperationException("القيمة ليست عددية.");
    }

    /// <summary>
    /// يعيد القيمة بوصفها عددًا صحيحًا لعملية الباقي %.
    /// </summary>
    public int AsInt()
    {
        // يعيد العدد الصحيح فقط ولا يسمح بالحقيقي.
        if (Type == LanguageType.Int)
        {
            return Convert.ToInt32(Value, CultureInfo.InvariantCulture);
        }

        // يمنع عملية الباقي على القيم غير الصحيحة.
        throw new InvalidOperationException("القيمة ليست من النوع عدد.");
    }

    /// <summary>
    /// يعيد القيمة بوصفها قيمة منطقية.
    /// </summary>
    public bool AsBool()
    {
        // يعيد القيمة المنطقية فقط.
        if (Type == LanguageType.Bool)
        {
            return (bool)Value;
        }

        // يمنع استعمال عدد أو نص مكان شرط منطقي.
        throw new InvalidOperationException("القيمة ليست من النوع منطقي.");
    }

    /// <summary>
    /// يعيد القيمة بوصفها نصاً.
    /// </summary>
    public string AsText()
    {
        // يعيد النص فقط.
        if (Type == LanguageType.Text)
        {
            return (string)Value;
        }

        // يمنع التحويل غير المقصود إلى نص داخل عمليات النصوص.
        throw new InvalidOperationException("القيمة ليست من النوع نص.");
    }

    /// <summary>
    /// يعرض القيمة كنص مناسب لتبويب الناتج في الواجهة.
    /// </summary>
    public override string ToString()
    {
        // يعرض المنطقيين بالكلمات العربية المعتمدة في اللغة.
        if (Type == LanguageType.Bool)
        {
            return AsBool() ? "صحيح" : "خطأ";
        }

        // يعرض الحقيقي بثقافة ثابتة كي لا يتغير الفاصل العشري حسب إعداد الجهاز.
        if (Type == LanguageType.Real)
        {
            return AsNumber().ToString("0.##########", CultureInfo.InvariantCulture);
        }

        // يعرض العدد أو النص كما هو.
        return Convert.ToString(Value, CultureInfo.InvariantCulture) ?? string.Empty;
    }
}
