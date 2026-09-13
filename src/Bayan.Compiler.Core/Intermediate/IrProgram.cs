using BayanCompiler.Semantics;

namespace BayanCompiler.Intermediate;

/// <summary>
/// يجمع تعليمات IR وقيم قسم البيانات اللازمة لمولد MIPS.
/// </summary>
public sealed class IrProgram
{
    // يحتفظ بتعليمات البرنامج بالترتيب الذي ستولد به إلى قسم .text.
    private readonly List<IrInstruction> _instructions = new List<IrInstruction>();

    // يحتفظ بأسماء المتغيرات والقيم المؤقتة وأنواعها لقسم .data.
    private readonly Dictionary<string, LanguageType> _storage = new Dictionary<string, LanguageType>(StringComparer.Ordinal);

    // Keeps the number of 32-bit words reserved by each storage label.
    private readonly Dictionary<string, int> _storageWordCounts = new Dictionary<string, int>(StringComparer.Ordinal);

    // يحتفظ بالنصوص المطلوب تعريفها في قسم .data.
    private readonly Dictionary<string, string> _strings = new Dictionary<string, string>(StringComparer.Ordinal);

    // Stores single-precision literal values that must be loaded through the MIPS floating-point unit.
    private readonly Dictionary<string, string> _floats = new Dictionary<string, string>(StringComparer.Ordinal);

    // Stores byte-buffer sizes used by runtime string input.
    private readonly Dictionary<string, int> _buffers = new Dictionary<string, int>(StringComparer.Ordinal);

    // Maps safe MIPS storage labels to Arabic source names for presentation only.
    private readonly Dictionary<string, string> _storageSourceNames = new Dictionary<string, string>(StringComparer.Ordinal);

    /// <summary>
    /// تعليمات البرنامج الوسيطة الجاهزة للطباعة أو التوليد.
    /// </summary>
    public IReadOnlyList<IrInstruction> Instructions => _instructions;

    /// <summary>
    /// مساحات الذاكرة المطلوبة للمتغيرات والقيم المؤقتة.
    /// </summary>
    public IReadOnlyDictionary<string, LanguageType> Storage => _storage;

    /// <summary>
    /// Gets the number of machine words reserved for each storage label.
    /// </summary>
    public IReadOnlyDictionary<string, int> StorageWordCounts => _storageWordCounts;

    /// <summary>
    /// النصوص التي ستتحول إلى تعليمات .asciiz في MIPS.
    /// </summary>
    public IReadOnlyDictionary<string, string> Strings => _strings;

    /// <summary>
    /// Gets addressable single-precision floating-point literals.
    /// </summary>
    public IReadOnlyDictionary<string, string> Floats => _floats;

    /// <summary>
    /// Gets runtime byte buffers allocated for string input.
    /// </summary>
    public IReadOnlyDictionary<string, int> Buffers => _buffers;

    /// <summary>
    /// Maps each internal storage label to its Arabic source name when available.
    /// </summary>
    public IReadOnlyDictionary<string, string> StorageSourceNames => _storageSourceNames;

    /// <summary>
    /// يضيف تعليمة واحدة إلى البرنامج الوسيط.
    /// </summary>
    public void Emit(IrInstruction instruction)
    {
        // يحفظ التعليمة بترتيبها لأن الفرع والحلقة يعتمدان على الترتيب.
        _instructions.Add(instruction);
    }

    /// <summary>
    /// يحجز كلمة ذاكرة لمتغير أو قيمة مؤقتة من النوع عدد أو منطقي.
    /// </summary>
    public void AddStorage(string name, LanguageType type, string? sourceName = null, int wordCount = 1)
    {
        // يمنع تعريف التخزين نفسه أكثر من مرة عند تكرار استخدام المتغير.
        if (!_storage.ContainsKey(name))
        {
            _storage.Add(name, type);
            _storageWordCounts.Add(name, Math.Max(1, wordCount));
        }

        // Keeps the Arabic source name only for IR display and MIPS comments.
        if (!string.IsNullOrWhiteSpace(sourceName))
        {
            _storageSourceNames[name] = sourceName;
        }
    }

    /// <summary>
    /// Returns the Arabic source name associated with an internal storage label.
    /// </summary>
    public string? GetStorageSourceName(string name)
    {
        // Returns null for compiler temporaries that intentionally have no source name.
        return _storageSourceNames.TryGetValue(name, out string? sourceName) ? sourceName : null;
    }

    /// <summary>
    /// يضيف نصاً معرفاً بعلامة ليستخدم في الطباعة.
    /// </summary>
    public void AddString(string label, string value)
    {
        // يحفظ النص مرة واحدة تحت علامة فريدة.
        _strings[label] = value;
    }

    /// <summary>
    /// Adds one addressable floating-point literal for FPU loading.
    /// </summary>
    public void AddFloat(string label, string value)
    {
        _floats[label] = value;
    }

    /// <summary>
    /// Allocates a named byte buffer in the MIPS data section.
    /// </summary>
    public void AddBuffer(string label, int byteCount)
    {
        _buffers[label] = Math.Max(2, byteCount);
    }
}
