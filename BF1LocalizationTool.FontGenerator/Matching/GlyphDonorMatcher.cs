// =============================================================================
// BF1LocalizationTool.FontGenerator — Matching/GlyphDonorMatcher.cs
// Автор / Author: EMP_UA (https://github.com/EMP-UA)
// Ліцензія / License: MIT
// =============================================================================
// UA: Донорські слоти ЖОРСТКО ФІКСОВАНІ за розміром (стратегія
//     "перезаписати існуючий слот", FONT_FORMAT_SPEC.md розділ 5:
//     рухати/змінювати розмір сусідніх гліфів в атласі не
//     розглядається). Тому кінцевий рендер ЗАВЖДИ буде масштабований
//     точно під CanvasWidth×CanvasHeight донора — питання лише в тому,
//     НАСКІЛЬКИ сильним буде це масштабування.
//
//     Замість силуваного вписування/обрізання (обрізання гірше за
//     масштабування — ламає форму штриха, а не просто трохи стискає) —
//     задача ПРИЗНАЧЕННЯ: підібрати для кожної літери донора з
//     природно близькою пропорцією ширина/висота, щоб масштабування було
//     мінімальним.
//
//     Підбір враховує ДВА критерії одночасно, бо самої лише пропорції
//     (W/H) недостатньо: FilterOutTooSmall пропускає донорів від 50% до
//     100%+ висоти великої літери, тобто серед "безпечних" донорів
//     висота лишається дуже різною, а GlyphBoxFitRenderer розтягує кожну
//     літеру рівно під розмір ЇЇ ВЛАСНОГО донора без жодного узгодження
//     з сусідніми літерами — тому літера, що отримала донора на 55%
//     висоти, і сусідня літера, що отримала донора на 95% висоти, на
//     екрані виглядають як дрібна і велика, навіть якщо обидві мають
//     майже ідеальну пропорцію (підтверджено скріншотами з реальної
//     гри — BF1/BF2 головне меню, літери "стрибають": і/н/д тощо різного
//     розміру в тому самому слові, деякі помітно менші за сусідні
//     англійські літери поруч):
//       1. Пропорція (log-шкала).
//       2. Відносна висота — InkHeight літери (з CyrillicGlyphShapeProbe,
//          порівнянний між усіма літерами ЦЬОГО алфавіту, бо всі
//          растеризуються при ОДНАКОВОМУ розмірі шрифту) як частка від
//          висоти "еталонної" великої кириличної літери (медіана висот
//          усіх ВЕЛИКИХ цілей), звірена з висотою донора як часткою від
//          referenceCapHeight (медіана висот РЕАЛЬНИХ A-Z цього шрифту —
//          той самий еталон, що й у FilterOutTooSmall).
//     Вага 1.0 для обох доданків — обидва в порівнянному діапазоні
//     (0-1), рівна вага як обґрунтована стартова точка; потребує
//     візуальної перевірки в грі після перегенерації, за потреби
//     скоригувати.
//
//     Призначення обробляє спершу НАЙЕКСТРЕМАЛЬНІШІ цілі (найбільше
//     відхилення від "квадратної, повної висоти" форми) — так літерам,
//     яким найважче знайти відповідного донора, першими дістається
//     найкращий вибір із ще повного пулу, а не рештки після того, як усі
//     "звичайні" літери вже розібрали найкращих кандидатів.
// EN: Donor slots are RIGIDLY FIXED in size (the "overwrite existing
//     slot" strategy, FONT_FORMAT_SPEC.md section 5: moving/resizing
//     neighboring atlas glyphs is out of scope). So the final render
//     will ALWAYS be scaled to exactly the donor's
//     CanvasWidth×CanvasHeight — the only question is HOW MUCH that
//     scaling distorts things.
//
//     Instead of forcing a fit / cropping (cropping is worse than
//     scaling — it breaks stroke shape rather than just slightly
//     compressing it) — this is an ASSIGNMENT problem: match each letter
//     to a donor with a naturally close width/height ratio, so scaling
//     stays minimal.
//
//     Matching factors in TWO criteria at once, because ratio (W/H)
//     alone is not enough: FilterOutTooSmall lets through donors
//     anywhere from 50% to 100%+ of the capital letter's height, so even
//     among "safe" donors, height varies wildly, and GlyphBoxFitRenderer
//     stretches each letter to exactly fill ITS OWN donor's size with no
//     coordination with neighboring letters — so a letter that got a
//     55%-height donor and a neighbor that got a 95%-height donor render
//     as small and large on screen, even if both have a near-perfect
//     ratio match (confirmed via real in-game screenshots — BF1/BF2 main
//     menu, "jumping" letters: і/н/д etc. at inconsistent sizes within
//     the same word, some noticeably smaller than neighboring English
//     letters):
//       1. Ratio (log scale).
//       2. Relative height — the letter's InkHeight (from
//          CyrillicGlyphShapeProbe, comparable across all letters of
//          THIS alphabet since all are rasterized at the SAME font size)
//          as a fraction of a "reference" capital Cyrillic letter's
//          height (median height of all UPPERCASE targets), checked
//          against the donor's height as a fraction of referenceCapHeight
//          (median height of REAL A-Z glyphs in this font — the same
//          reference FilterOutTooSmall already uses).
//     Weight 1.0 for both terms — both are in a comparable range (0-1),
//     equal weighting as a reasoned starting point; needs visual
//     confirmation in-game after regeneration, adjust if needed.
//
//     Assignment processes "most extreme
//     targets first" (largest deviation from a "square, full-height"
//     shape) — so letters that are hardest to match get first pick from
//     the still-full donor pool, instead of leftovers after all
//     "ordinary" letters already took the best candidates.
// =============================================================================

namespace BF1LocalizationTool.FontGenerator.Matching;

public sealed record DonorSlot(ushort Code, int Width, int Height);

// UA: TargetInkTop — верхня межа природного чорнила ЦІЛЬОВОЇ літери, в
//     абсолютних координатах пробного canvas (те саме BaselineY, що й
//     скрізь в цьому алфавіті — CyrillicGlyphShapeProbe). Додано для
//     CyrillicFontInjector/GlyphBoxFitRenderer: щоб відділити "тіло"
//     (спільного розміру для регістру) від "виступу" (зверху/знизу,
//     обмеженого резервом) — SteamWorld Heist-підхід. НЕ впливає на сам
//     підбір донора (Cost/Assign нижче його не використовують) — лише
//     проноситься далі як частина вже обчисленого вимірювання форми.
// EN: TargetInkTop — the top edge of the target letter's natural ink, in
//     absolute probe-canvas coordinates (the same BaselineY used
//     everywhere in this alphabet — CyrillicGlyphShapeProbe). Added for
//     CyrillicFontInjector/GlyphBoxFitRenderer: to split a letter into
//     "core" (shared size across the case) and "extension" (top/bottom,
//     capped by a reserved margin) — the SteamWorld Heist approach. Does
//     NOT affect donor matching itself (Cost/Assign below don't use it)
//     — it's only carried through as part of the shape measurement
//     already computed.
public sealed record GlyphAssignment(
    char Character, ushort DonorCode, int CanvasWidth, int CanvasHeight,
    double TargetAspectRatio, double DonorAspectRatio,
    double TargetHeightFraction, double DonorHeightFraction,
    int TargetInkTop);

// UA: Розмір і геометрія одного РЕАЛЬНОГО (уже наявного в шрифті) гліфа —
//     використовується для еталонної висоти, а не як донор.
// EN: Size/geometry of one REAL (already present in the font) glyph —
//     used for a reference height, not as a donor.
public sealed record KnownGlyphGeometry(ushort Code, int Width, int Height);

public static class GlyphDonorMatcher
{
    // UA: Еталонна висота "нормальної" великої літери в ЦЬОМУ КОНКРЕТНОМУ
    //     шрифті — медіана висот реальних A-Z (вони існують у FBOD, лише
    //     не донори, бо "використані" англійською; медіана, а не
    //     максимум/перша знайдена — щоб один аномальний гліф не зіпсував
    //     еталон).
    // EN: Reference height of a "normal" capital letter in THIS SPECIFIC
    //     font — median height of the real A-Z glyphs (they exist in the
    //     FBOD, just aren't donors since English "uses" them; median, not
    //     max/first-found — so one anomalous glyph doesn't skew the
    //     reference).
    public static int GetReferenceCapHeight(IReadOnlyList<KnownGlyphGeometry> allFontGlyphs)
    {
        var capHeights = allFontGlyphs
            .Where(g => g.Code is >= (ushort)'A' and <= (ushort)'Z' && g.Height > 0)
            .Select(g => g.Height)
            .OrderBy(h => h)
            .ToList();

        if (capHeights.Count == 0)
            throw new InvalidOperationException(
                "UA: Жодної великої латинської літери (A-Z) не знайдено в шрифті для еталонної висоти. / " +
                "EN: No uppercase Latin letter (A-Z) found in the font for a reference height.");

        return capHeights[capHeights.Count / 2];
    }

    // UA: Відсіює донорів, чия висота значно менша за еталонну висоту
    //     великої літери — це майже напевно не повноцінні літери, а
    //     діакритика/пунктуація/маленькі символи, в які складну
    //     кириличну літеру все одно не вписати розбірливо (питання не
    //     ресемплінгу, а АБСОЛЮТНОГО браку пікселів). minHeightFraction
    //     навмисно застосовується лише до ВИСОТИ, не до ширини — вузькі,
    //     але повновисоті донори (потрібні для 'і'/'ї') лишаються
    //     доступними.
    // EN: Filters out donors whose height is significantly less than the
    //     reference capital-letter height — these are almost certainly
    //     not full letters but diacritics/punctuation/small symbols, into
    //     which a complex Cyrillic letter can't be drawn legibly anyway
    //     (a matter of an ABSOLUTE lack of pixels, not resampling).
    //     minHeightFraction is deliberately applied to HEIGHT only, not
    //     width — narrow-but-full-height donors (needed for 'і'/'ї')
    //     remain available.
    public static List<DonorSlot> FilterOutTooSmall(
        IReadOnlyList<DonorSlot> donors, int referenceCapHeight, double minHeightFraction = 0.5)
    {
        var threshold = referenceCapHeight * minHeightFraction;
        return donors.Where(d => d.Height >= threshold).ToList();
    }

    // UA: referenceCapHeight — та сама еталонна висота (медіана A-Z
    //     РЕАЛЬНИХ гліфів цього шрифту), яку викликач уже рахує для
    //     FilterOutTooSmall (GlyphDonorMatcher.GetReferenceCapHeight) —
    //     потрібна тут, щоб перевести висоту донора в частку від
    //     еталонної, порівнянну з відносною висотою цільової літери.
    // EN: referenceCapHeight — the SAME reference height (median of REAL
    //     A-Z glyphs in this font) the caller already computes for
    //     FilterOutTooSmall (GlyphDonorMatcher.GetReferenceCapHeight) —
    //     needed here to express donor height as a fraction of the
    //     reference, comparable to the target letter's relative height.
    public static List<GlyphAssignment> Assign(
        IReadOnlyList<char> targetCharacters,
        IReadOnlyList<DonorSlot> availableDonors,
        string fontFamilyName,
        int referenceCapHeight)
    {
        if (availableDonors.Count < targetCharacters.Count)
            throw new ArgumentException(
                $"UA: Донорів ({availableDonors.Count}) менше, ніж цільових символів ({targetCharacters.Count}) — призначення неможливе без повторного використання слоту. / " +
                $"EN: Fewer donors ({availableDonors.Count}) than target characters ({targetCharacters.Count}) — assignment is impossible without reusing a slot.");

        var targetsWithShape = targetCharacters
            .Select(c => (Character: c, Shape: CyrillicGlyphShapeProbe.MeasureShape(c, fontFamilyName)))
            .ToList();

        // UA: Еталонна висота ВЕЛИКОЇ кириличної літери — медіана
        //     InkHeight усіх ВЕЛИКИХ цілей (не лише 'А'), той самий
        //     принцип "медіана, не перша-ліпша", що й
        //     GetReferenceCapHeight для латиниці.
        // EN: Reference CAPITAL Cyrillic letter height — median InkHeight
        //     of all UPPERCASE targets (not just 'А'), same "median, not
        //     first-found" principle as GetReferenceCapHeight for Latin.
        var uppercaseHeights = targetsWithShape
            .Where(t => CyrillicAlphabet.UppercaseLetters.Contains(t.Character))
            .Select(t => t.Shape.InkHeight)
            .OrderBy(h => h)
            .ToList();

        if (uppercaseHeights.Count == 0)
            throw new ArgumentException(
                "UA: Серед цільових символів немає жодної великої літери — неможливо обчислити еталонну висоту. / " +
                "EN: No uppercase character among the targets — cannot compute a reference height.");

        var referenceCyrillicCapHeight = uppercaseHeights[uppercaseHeights.Count / 2];

        // UA: Комбінована вартість: |Δlog-пропорція| + |Δвідносна висота|
        //     — обидва доданки в порівнянному діапазоні (0-1), вага 1.0
        //     для кожного. Див. заголовок файлу.
        // EN: Combined cost: |Δlog-ratio| + |Δrelative height| — both
        //     terms in a comparable range (0-1), weight 1.0 each. See
        //     file header.
        double Cost(double targetLogRatio, double targetHeightFraction, DonorSlot d)
        {
            var donorLogRatio = Math.Log((double)d.Width / d.Height);
            var donorHeightFraction = (double)d.Height / referenceCapHeight;
            return Math.Abs(donorLogRatio - targetLogRatio) + Math.Abs(donorHeightFraction - targetHeightFraction);
        }

        var targetsOrdered = targetsWithShape
            .Select(t => (
                t.Character,
                TargetAspect: t.Shape.AspectRatio,
                TargetLogRatio: Math.Log(t.Shape.AspectRatio),
                TargetHeightFraction: (double)t.Shape.InkHeight / referenceCyrillicCapHeight,
                TargetInkTop: t.Shape.InkTop))
            // UA: НАЙЕКСТРЕМАЛЬНІШІ цілі першими — щоб їм дістався
            //     найкращий вибір із ще повного пулу донорів, а не
            //     рештки. Див. заголовок файлу.
            // EN: MOST EXTREME targets first — so they get the best pick
            //     from the still-full donor pool, not leftovers. See file
            //     header.
            .OrderByDescending(t => Math.Abs(t.TargetLogRatio) + Math.Abs(t.TargetHeightFraction - 1.0))
            .ToList();

        var remainingDonors = availableDonors.ToList();
        var result = new List<GlyphAssignment>();

        foreach (var t in targetsOrdered)
        {
            var best = remainingDonors
                .OrderBy(d => Cost(t.TargetLogRatio, t.TargetHeightFraction, d))
                .First();

            var donorHeightFraction = (double)best.Height / referenceCapHeight;
            result.Add(new GlyphAssignment(
                t.Character, best.Code, best.Width, best.Height,
                t.TargetAspect, (double)best.Width / best.Height,
                t.TargetHeightFraction, donorHeightFraction,
                t.TargetInkTop));
            remainingDonors.Remove(best);
        }

        return result;
    }
}
