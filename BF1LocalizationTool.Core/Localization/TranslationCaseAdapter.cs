// =============================================================================
// BF1LocalizationTool.Core — Localization/TranslationCaseAdapter.cs
// Автор / Author: EMP_UA (https://github.com/EMP-UA)
// Ліцензія / License: MIT
// =============================================================================
// UA: Узгодження РЕГІСТРУ перекладу, перенесеного з іншої гри, з регістром
//     англійського рядка цільової гри.
//
//     ПРИЧИНА: англійський текст BF1 зберігається ВЕЛИКИМИ ЛІТЕРАМИ
//     ("REPUBLIC ASSAULT SHIP"), а BF2 — звичайним регістром ("Republic
//     Assault Ship"); переклад BF1 здебільшого теж набраний ВЕЛИКИМИ. Без
//     узгодження перенесений у BF2 переклад виглядав би як "НІКОЛИ" поруч
//     з англійським "Never".
//
//     ПРАВИЛА (лише для перекладу, набраного ПОВНІСТЮ великими літерами;
//     будь-який інший переклад переноситься без змін):
//       1. Англійський рядок цілі теж повністю великими — переклад
//          лишається великими.
//       2. Інакше — звичайне речення: велика літера лише на початку
//          речення, решта малі. Початок рядка отримує велику літеру лише
//          тоді, коли англійський рядок цілі сам починається з великої
//          (фрагменти на кшталт "to enter the AT-ST" починаються з малої —
//          так само й переклад). Крапка завершує речення лише тоді, коли
//          англійський рядок цілі сам містить розрив речень (". " перед
//          великою літерою) — інакше крапка в перекладі позначає
//          скорочення ("ВИМК.", "УТРИМ."). "!" і "?" завершують речення
//          завжди. Двокрапка, скісна риска й перенесення рядка речення не
//          завершують.
//       3. Слово, що має велику літеру в англійському рядку цілі
//          (латиниця: "Windows", "AT-ST", "Y"), бере регістр звідти.
//       4. Слово, що має велику літеру в ПОТОЧНОМУ перекладі цього ж рядка
//          цілі не на початку речення чи сегмента (після ":" або "/") —
//          власні назви на кшталт "Набу", "Республіки" — бере регістр
//          звідти. Поточний переклад, набраний повністю великими, як
//          джерело не використовується.
//     Технічні маркери (%s, {btn...}, [KEY] тощо — той самий шаблон, що й
//     у ValidationService) не змінюються ніколи.
//
// EN: Aligns the CASE of a translation transferred from another game with
//     the case of the target game's English string.
//
//     REASON: BF1's English text is stored in UPPER CASE ("REPUBLIC
//     ASSAULT SHIP"), while BF2's uses normal case ("Republic Assault
//     Ship"); BF1's translation is mostly typed in UPPER CASE as well.
//     Without alignment, a translation transferred into BF2 would read as
//     "НІКОЛИ" next to the English "Never".
//
//     RULES (only for a translation typed ENTIRELY in upper case; any
//     other translation is transferred unchanged):
//       1. The target's English string is also entirely upper case — the
//          translation stays upper case.
//       2. Otherwise — sentence case: upper case only at the start of a
//          sentence, the rest lower case. The start of the string gets an
//          upper-case letter only when the target's English string itself
//          starts with one (fragments like "to enter the AT-ST" start in
//          lower case — so does the translation). A period ends a sentence
//          only when the target's English string itself contains a
//          sentence break (". " before an upper-case letter) — otherwise a
//          period in the translation marks an abbreviation ("ВИМК.",
//          "УТРИМ."). "!" and "?" always end a sentence. Colons, slashes
//          and line breaks do not end a sentence.
//       3. A word that has an upper-case letter in the target's English
//          string (Latin script: "Windows", "AT-ST", "Y") takes its case
//          from there.
//       4. A word that has an upper-case letter in the CURRENT translation
//          of the same target row, not at the start of a sentence or
//          segment (after ":" or "/") — proper names like "Набу",
//          "Республіки" — takes its case from there. A current translation
//          typed entirely in upper case is not used as a source.
//     Technical markers (%s, {btn...}, [KEY] etc. — the same pattern as in
//     ValidationService) are never changed.
// =============================================================================

using System.Text.RegularExpressions;

namespace BF1LocalizationTool.Core.Localization;

public static class TranslationCaseAdapter
{
    private static readonly Regex MarkerRegex = new(
        ValidationService.MarkerPattern,
        RegexOptions.Compiled | RegexOptions.IgnoreCase);

    // UA: Слово — неперервна послідовність літер (цифри й апостроф його
    //     розривають; для зіставлення регістру цього достатньо).
    // EN: A word is a contiguous run of letters (digits and apostrophes
    //     split it; that is sufficient for case matching).
    private static readonly Regex WordRegex = new(@"\p{L}+", RegexOptions.Compiled);

    // UA: Розрив речень в англійському рядку: ". ", "! " або "? " перед
    //     великою латинською літерою.
    // EN: A sentence break in an English string: ". ", "! " or "? " before
    //     an upper-case Latin letter.
    private static readonly Regex EnglishSentenceBreakRegex = new(@"[.!?]\s+[A-Z]", RegexOptions.Compiled);

    // -------------------------------------------------------------------------
    // UA: translation — переклад із донора; targetOriginal — англійський
    //     рядок цілі; currentTranslation — поточний переклад цього ж рядка
    //     цілі (null, якщо його немає).
    // EN: translation — the donor's translation; targetOriginal — the
    //     target's English string; currentTranslation — the current
    //     translation of the same target row (null if there is none).
    // -------------------------------------------------------------------------
    public static string Adapt(string translation, string targetOriginal, string? currentTranslation)
    {
        if (!IsAllCaps(translation) || IsAllCaps(targetOriginal))
            return translation;

        var mask = BuildMarkerMask(translation);
        var starts = FindSentenceStarts(translation, mask,
            dotEndsSentence: EnglishSentenceBreakRegex.IsMatch(targetOriginal),
            segmentBreaks: false);

        var firstLetter = FirstLetterIndex(translation, mask);
        if (firstLetter >= 0 && starts.Contains(firstLetter) && StartsWithLowerCaseSentence(targetOriginal))
            starts.Remove(firstLetter);

        var chars = translation.ToCharArray();
        for (var i = 0; i < chars.Length; i++)
        {
            if (mask[i] || !char.IsLetter(chars[i])) continue;
            chars[i] = starts.Contains(i)
                ? char.ToUpperInvariant(chars[i])
                : char.ToLowerInvariant(chars[i]);
        }
        var result = new string(chars);

        var forms = CollectCasedWords(targetOriginal, skipSentenceInitial: false);
        if (!string.IsNullOrEmpty(currentTranslation) && !IsAllCaps(currentTranslation))
        {
            foreach (var (key, form) in CollectCasedWords(currentTranslation, skipSentenceInitial: true))
                forms.TryAdd(key, form);
        }

        if (forms.Count == 0) return result;

        // UA: Довжина рядка після зміни регістру не змінюється (char.To*Invariant
        //     — посимвольне перетворення), тож маска маркерів лишається дійсною.
        // EN: The string length doesn't change after the case change
        //     (char.To*Invariant is a per-char mapping), so the marker mask
        //     stays valid.
        return WordRegex.Replace(result, m =>
        {
            if (Overlaps(mask, m.Index, m.Length)) return m.Value;
            return forms.TryGetValue(m.Value.ToUpperInvariant(), out var form) ? form : m.Value;
        });
    }

    // UA: Є хоча б одна літера з регістром поза маркерами, і жодна з них не мала.
    // EN: At least one cased letter outside markers, and none of them is lower case.
    public static bool IsAllCaps(string text)
    {
        var mask = BuildMarkerMask(text);
        var anyUpper = false;
        for (var i = 0; i < text.Length; i++)
        {
            if (mask[i]) continue;
            if (char.IsLower(text[i])) return false;
            if (char.IsUpper(text[i])) anyUpper = true;
        }
        return anyUpper;
    }

    private static bool StartsWithLowerCaseSentence(string text)
    {
        var mask = BuildMarkerMask(text);
        var first = FirstLetterIndex(text, mask);
        if (first < 0) return false;

        // UA: Лише якщо перша літера справді починає речення (не стоїть після
        //     маркера чи цифри, як у "%s failed").
        // EN: Only if the first letter really starts a sentence (not after a
        //     marker or a digit, as in "%s failed").
        var starts = FindSentenceStarts(text, mask, dotEndsSentence: true, segmentBreaks: false);
        return starts.Contains(first) && char.IsLower(text[first]);
    }

    private static Dictionary<string, string> CollectCasedWords(string text, bool skipSentenceInitial)
    {
        var result = new Dictionary<string, string>(StringComparer.Ordinal);
        var mask = BuildMarkerMask(text);
        var starts = FindSentenceStarts(text, mask, dotEndsSentence: true, segmentBreaks: skipSentenceInitial);

        foreach (Match m in WordRegex.Matches(text))
        {
            if (Overlaps(mask, m.Index, m.Length)) continue;

            var word = m.Value;
            if (!word.Any(char.IsUpper)) continue;

            // UA: Велика літера на початку речення/сегмента — ознака позиції,
            //     а не власної назви; такі слова не беруться як зразок.
            // EN: An upper-case letter at the start of a sentence/segment
            //     marks the position, not a proper name; such words aren't
            //     taken as a pattern.
            if (skipSentenceInitial && starts.Contains(m.Index) && !word.Skip(1).Any(char.IsUpper))
                continue;

            result.TryAdd(word.ToUpperInvariant(), word);
        }
        return result;
    }

    private static HashSet<int> FindSentenceStarts(string text, bool[] mask, bool dotEndsSentence, bool segmentBreaks)
    {
        var starts = new HashSet<int>();
        var pending = true;

        for (var i = 0; i < text.Length; i++)
        {
            if (mask[i]) { pending = false; continue; }

            var c = text[i];
            if (char.IsLetter(c))
            {
                if (pending) starts.Add(i);
                pending = false;
            }
            else if (char.IsDigit(c))
            {
                pending = false;
            }
            else if ((c == '!' || c == '?' || (c == '.' && dotEndsSentence)) &&
                     (i + 1 == text.Length || char.IsWhiteSpace(text[i + 1])))
            {
                pending = true;
            }
            else if (segmentBreaks && (c == ':' || c == '/'))
            {
                pending = true;
            }
        }
        return starts;
    }

    private static bool[] BuildMarkerMask(string text)
    {
        var mask = new bool[text.Length];
        foreach (Match m in MarkerRegex.Matches(text))
        {
            for (var i = m.Index; i < m.Index + m.Length; i++)
                mask[i] = true;
        }
        return mask;
    }

    private static int FirstLetterIndex(string text, bool[] mask)
    {
        for (var i = 0; i < text.Length; i++)
        {
            if (!mask[i] && char.IsLetter(text[i])) return i;
        }
        return -1;
    }

    private static bool Overlaps(bool[] mask, int index, int length)
    {
        for (var i = index; i < index + length; i++)
        {
            if (mask[i]) return true;
        }
        return false;
    }
}
