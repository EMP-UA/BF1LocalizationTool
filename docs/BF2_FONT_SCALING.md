# BF2: збільшення шрифту під 1080p / BF2: font enlargement for 1080p

## 1. Причина / Reason

**UA:** Battlefront (2004) має нативну підтримку широкого екрана — рушій сам
коректно масштабує інтерфейс на 1920×1080, тому шрифт для нього не
змінювався.

Battlefront II (2005) такої підтримки не має: інтерфейс розрахований під
роздільність 800×600, а оригінальний розмір шрифту на сучасному екрані
нечитабельний — особливо для кирилиці, у якої більшість літер мають
складнішу форму, ніж у латиниці. Тому для Battlefront II шрифт збільшено.

**EN:** Battlefront (2004) natively supports widescreen — the engine scales the
interface correctly on 1920×1080 on its own, so its font was left
unchanged.

Battlefront II (2005) has no such support: its interface is built for
800×600, and the original font size is not readable on a modern screen —
especially for Cyrillic, whose letterforms are generally more complex
than Latin ones. The font was therefore enlarged for Battlefront II.

## 2. Що саме змінено / What changed

**UA:** Висота шрифту — значення, яке гра зберігає у ресурсі `HEAD` кожного
шрифтового файлу і яке читають власні формули верстки рушія (зокрема
розрахунок розміру підкладки під заголовком):

| Шрифт | Оригінал (px) | Українська версія (px) | Приріст |
|---|---|---|---|
| `gamefont_large` | 22 | 33 | +50% |
| `gamefont_medium` | 19 | 28 | +47% |
| `gamefont_small` | 17 | 26 | +53% |
| `gamefont_tiny` / `gamefont_super_tiny` | 13 | 20 | +54% |

Значення звірені побайтово: поле `HEAD` прочитане напряму з реального
українського `core.lvl` (не з опису чи журналу збірки) і зіставлене з тим
самим полем у ванільному файлі.

**EN:** Font height — the value the game stores in each font resource's `HEAD`
field and that the engine's own layout formulas read (including the
title-backdrop size calculation):

| Font | Original (px) | Ukrainian version (px) | Increase |
|---|---|---|---|
| `gamefont_large` | 22 | 33 | +50% |
| `gamefont_medium` | 19 | 28 | +47% |
| `gamefont_small` | 17 | 26 | +53% |
| `gamefont_tiny` / `gamefont_super_tiny` | 13 | 20 | +54% |

The values are verified byte-for-byte: the `HEAD` field was read
directly from the real Ukrainian `core.lvl` (not from a description or
build log) and compared against the same field in the vanilla file.

## 3. Чому висота `HEAD` виправлена окремим кроком / Why the `HEAD` height needed a separate fix

**UA:** Збільшення самих гліфів (їхнього зображення в шрифтовому атласі) не
оновлює автоматично 4-байтове поле `HEAD`, яке гра читає окремо як
«офіційну» висоту шрифту. Це поле використовують формули верстки —
наприклад, висота підкладки під заголовком обчислюється як
`bgexpandy = висота_шрифту / 2`. Якщо `HEAD` лишити старим, рушій і далі
рахує розміри контейнерів за оригінальним, дрібним розміром — підкладки
й капсули кнопок виходять замалими для вже збільшеного тексту.

Пряма правка цього поля всередині основного генератора шрифту відхилена:
значення `HEAD` читають 47 місць у 21 скрипті гри, і зміна порядку його
обчислення там зачепила б забагато залежностей одночасно. Замість цього
застосовується окремий, ізольований патч, що міняє лише цей один байт на
шрифт у вже готовому `core.lvl`, не перегенеровуючи атлас:

```
нова_висота = max(
    поточна_висота,
    максимальна_комірка_гліфа_цілі + max(0, ваніль_висота − ваніль_максимальна_комірка_гліфа)
)
```

Формула гарантує, що нова висота ніколи не менша за реальний розмір
найбільшого гліфа плюс той самий запас, який мала оригінальна гра —
завдяки цьому підхід переноситься на будь-який інший шрифтовий файл чи
екран без підбору окремого значення вручну.

**EN:** Enlarging the glyphs themselves (their image in the font atlas) does not
automatically update the 4-byte `HEAD` field, which the game reads
separately as the font's "official" height. Layout formulas rely on that
field — for example, a title backdrop's height is computed as
`bgexpandy = fontHeight / 2`. Left unchanged, the engine keeps sizing
containers for the original, small font, so backdrops and button capsules
end up too small for the now-larger text.

Patching this field directly inside the main font generator was rejected:
`HEAD` is read from 47 call sites across 21 game scripts, and changing how
it's computed there would touch too many dependencies at once. Instead, a
separate, isolated patch changes just this one byte per font inside an
already-built `core.lvl`, without regenerating the atlas:

```
new_height = max(
    current_height,
    target_max_glyph_cell + max(0, vanilla_height − vanilla_max_glyph_cell)
)
```

The formula guarantees the new height is never smaller than the actual
largest-glyph size plus the same margin the original game had — which is
what makes the approach reusable for any other font file or screen
without hand-picking a value.

## 4. Джерело гліфів / Glyph source

**UA:** Кирилицю намальовано заново, реальними кодами Unicode — без запозичення
слотів іншої мови й без накладання на існуючі латинські літери. Малюнок
літер узято зі шрифту Fira Sans (SIL Open Font License), підібраного за
товщиною штриха й формою під стиль оригінальних шрифтів гри.

**EN:** Cyrillic glyphs were drawn from scratch as real Unicode code points — not
borrowed from another language's slots and not overlaid on existing Latin
letters. The letterforms come from Fira Sans (SIL Open Font License),
chosen to match the stroke weight and shape of the game's own fonts.
