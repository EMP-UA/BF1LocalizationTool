// =============================================================================
// BF1LocalizationTool.Core — Fonts/GlyphTransparentPixelStats.cs
// Автор / Author: EMP_UA (https://github.com/EMP-UA)
// Ліцензія / License: MIT
// =============================================================================
// UA: Агреговані лічильники стилю запису прозорих пікселів для ОДНОЇ
//     пари (базовий шрифт, текстурна сторінка) — по ВСІХ гліфах з FBOD,
//     а не по одному вручну обраному символу.
// EN: Aggregate transparent-pixel encoding style counters for a SINGLE
//     (font base name, texture page) pair — across ALL glyphs from FBOD,
//     not a single manually chosen character.
// =============================================================================

namespace BF1LocalizationTool.Core.Fonts;

public sealed record GlyphTransparentPixelStats
{
    public required string FontBaseName { get; init; }
    public required string TexturePageName { get; init; }
    public required int TotalGlyphs { get; init; }
    public required long TotalPixelsChecked { get; init; }

    // UA: Alpha=0, RGB=білий — конвенція, яку ми бачили в BF1.
    // EN: Alpha=0, RGB=white — the convention observed in BF1.
    public required long ZeroAlphaWhiteRgbCount { get; init; }

    // UA: Alpha=0, RGB=0 — конвенція, яку ми бачили в BF2.
    // EN: Alpha=0, RGB=0 — the convention observed in BF2.
    public required long ZeroAlphaZeroRgbCount { get; init; }

    // UA: Alpha=0, RGB — щось ІНШЕ (не білий, не нуль). Якщо >0 —
    //     існує ТРЕТЯ, ще не описана конвенція, і жодне з двох
    //     припущень не можна хардкодити наосліп.
    // EN: Alpha=0, RGB is something ELSE (not white, not zero). If >0 —
    //     a THIRD, undocumented convention exists, and neither
    //     assumption can be hardcoded blindly.
    public required long ZeroAlphaOtherRgbCount { get; init; }

    // UA: Alpha>0, але RGB≠білий — АНОМАЛІЯ базового припущення
    //     "Alpha несе покриття, RGB завжди білий на видимих пікселях".
    //     Якщо >0 — це ламає обидва варіанти конвертера, треба
    //     досліджувати окремо перед написанням будь-якого коду.
    // EN: Alpha>0, but RGB≠white — an ANOMALY in the core assumption
    //     "Alpha carries coverage, RGB is always white on visible
    //     pixels". If >0 — this breaks both converter variants and
    //     needs separate investigation before any code is written.
    public required long NonZeroAlphaNonWhiteRgbCount { get; init; }
}