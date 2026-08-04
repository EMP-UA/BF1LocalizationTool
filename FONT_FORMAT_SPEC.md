# SWBF Font Format Specification (BF1/BF2)

**UA:** Формат-референс шрифтових ресурсів Star Wars Battlefront (2004)
і Battlefront II Classic та кириличного кодування, яке генерує
`BF1LocalizationTool.FontGenerator`. Усі значення підтверджені
інструментами `BF1LocalizationTool.Diagnostic` на реальних `core.lvl`
обох ігор.
**EN:** Format reference for the reverse-engineered font resources of
Star Wars Battlefront (2004) and Battlefront II Classic, and for the
Cyrillic encoding produced by `BF1LocalizationTool.FontGenerator`. All
values are verified against the real `core.lvl` of both games via
`BF1LocalizationTool.Diagnostic`.

## 1. Розташування шрифтів / Font location

**UA:** Шрифти лежать у `core.lvl`, поруч з локалізацією
(`BF1LocalizationTool.Core.Fonts.FontChunkLocator.FindAll(root)`).
**EN:** Fonts live inside `core.lvl`, next to the localization data
(`BF1LocalizationTool.Core.Fonts.FontChunkLocator.FindAll(root)`).

**UA:** Базові імена (спільні для BF1 і BF2, крім `starwars_small` —
лише BF1): `gamefont_large`, `gamefont_medium`, `gamefont_small`,
`gamefont_tiny`, `gamefont_super_tiny`, `starwars_small`. Кожен має
1–4 текстурні сторінки `{ім'я}_texN`.
**EN:** Base names (shared by BF1 and BF2, except `starwars_small` — BF1
only): `gamefont_large`, `gamefont_medium`, `gamefont_small`,
`gamefont_tiny`, `gamefont_super_tiny`, `starwars_small`. Each has 1–4
texture pages `{name}_texN`.

**UA:**
- **`starwars_small` (лише BF1)** — набір іконок HUD/UI, не текстовий
  шрифт, виключений з кириличної ін'єкції.
- **BF2 `gamefont_tiny`/`gamefont_super_tiny`** — побайтово ідентичні
  ресурси (`FontResourceIdentityCheckCommand`). У BF1 це два різні
  шрифти.

**EN:**
- **`starwars_small` (BF1 only)** — a set of HUD/UI icons, not a text
  font; excluded from Cyrillic injection.
- **BF2 `gamefont_tiny`/`gamefont_super_tiny`** — byte-identical
  resources (`FontResourceIdentityCheckCommand`). In BF1 these are two
  distinct fonts.

## 2. Структура ресурсу (ucfb-дерево) / Resource tree structure

```
[hashed ID] (font container)
├── NAME              — "gamefont_super_tiny\0"
├── HEAD  (6 bytes)    — font metadata (byte[0:2] = glyph count, §4)
├── FTEX  (FourCC!)    — texture container
│   ├── NAME           — "{name}_tex0\0"
│   ├── tex_ (hashed)  — one texture page
│   │   ├── NAME
│   │   ├── INFO (8b)
│   │   └── FMT_
│   │       ├── INFO (16b) — format=0x1A(26)=D3DFMT_A4R4G4B4, width, height, mip×face
│   │       └── FACE
│   │           └── LVL_
│   │               ├── INFO (8b) — mip index, size
│   │               └── BODY  — raw pixels (leaf, no children)
│   └── ... (tex1, tex2 — same structure)
└── FBOD               — glyph table (leaf, no children)
```

**UA:** `BODY` і `FBOD` — чанки без дітей; діти на цьому рівні
означають баг парсера, не формат гри (§6.2). Пакування атласу —
bin-packer, не послідовний текстовий рядок: кожен гліф-слот пакується
незалежно, без спільної "базової лінії" на рівні атласу.
**EN:** `BODY` and `FBOD` are childless leaf chunks; children at this
level indicate a parser bug, not the game's format (§6.2). The atlas is
packed by a bin-packer, not a text sequence: each glyph slot is packed
independently, with no atlas-level "baseline" shared between glyphs.

## 3. Формат пікселів BODY / Pixel format (BODY)

**UA:**
- **D3DFMT_A4R4G4B4**, 16 біт/піксель (напр. `gamefont_super_tiny_tex0`
  = 128×128, 32768 байти = 128×128×2).
- Піксель = `uint16` LE: `[15:12]=A, [11:8]=R, [7:4]=G, [3:0]=B`.
  Розширення 4→8 біт: `value * 17`.
- Розмір інших сторінок — з `INFO` (offset 4/6 у 16-байтному `INFO`
  всередині `FMT_`).
- `BODY.Length == Width×Height×2`, без padding. Офсет пікселя:
  `(row×TextureWidth + col) × 2`.
- RGB завжди `0xFFF` там, де `Alpha>0`, без винятків; там, де `Alpha=0`,
  значення RGB на рендер не впливає. Конвертер завжди пише `RGB=0xFFF`.

**EN:**
- **D3DFMT_A4R4G4B4**, 16 bits/pixel (e.g. `gamefont_super_tiny_tex0` =
  128×128, 32768 bytes = 128×128×2).
- Pixel = `uint16` LE: `[15:12]=A, [11:8]=R, [7:4]=G, [3:0]=B`. 4→8-bit
  expansion: `value * 17`.
- Other pages' size comes from `INFO` (offsets 4/6 of the 16-byte
  `INFO` inside `FMT_`).
- `BODY.Length == Width×Height×2`, no padding. Pixel offset:
  `(row×TextureWidth + col) × 2`.
- RGB is always `0xFFF` where `Alpha>0`, no exceptions; where
  `Alpha=0`, RGB has no rendering effect. The converter always writes
  `RGB=0xFFF`.

## 4. Формат таблиці гліфів FBOD / Glyph table format (FBOD)

**UA:** Записи по 24 байти, кількість = `RawData.Length / 24`. Коди
йдуть підряд, без пропусків (напр. `super_tiny`: 0x1E…0xFF, 226 записів).
**EN:** Fixed 24-byte records, count = `RawData.Length / 24`. Codes are
contiguous, no gaps (e.g. `super_tiny`: 0x1E…0xFF, 226 records).

```
offset  size  field
0       2     code             — uint16 LE, character byte value
2       1     page_index       — 0-based texture page index (§4.1)
3       1     xadvance         — px, cursor advance (§4.4)
4       1     reserved_byte_4  — purpose unknown, NOT always 0
5       1     ink_width        — px, cursor metadata, NOT the UV box size (§4.3)
6       1     bearing          — px, horizontal offset, varies by glyph shape (§4.4)
7       1     cell_h           — px, cursor metadata, NOT the UV box size (§4.3)
8       4     U0               — float, left texture edge (0..1)
12      4     U1               — float, right texture edge
16      4     V0               — float, top/bottom edge (direction: §4.2)
20      4     V1               — float, opposite edge
```

**UA:** Порядок полів — **U0, U1, V0, V1** (не U0,V0,U1,V1). Пікселі
гліфа: `x0=round(U0×texW)`, `x1=round(U1×texW)`, аналогічно по Y.
`reserved_byte_4` читається й пишеться без змін
(`FontGlyphRecord.ReservedByte4`).
**EN:** Field order is **U0, U1, V0, V1** (not U0,V0,U1,V1). Glyph
pixels: `x0=round(U0×texW)`, `x1=round(U1×texW)`, analogous for Y.
`reserved_byte_4` is read and written unchanged
(`FontGlyphRecord.ReservedByte4`).

### 4.1. `page_index`

**UA:** 0-based індекс сторінки `{ім'я}_texN`. Кожен гліф існує лише на
ОДНІЙ сторінці; ті самі UV-координати на інших сторінках належать
іншим гліфам. Читання/запис МАЄ використовувати
`font.TexturePages[glyph.PageIndex]`.
**EN:** 0-based index of the `{name}_texN` page. Each glyph exists on
exactly one page; the same UV coordinates on other pages belong to
unrelated glyphs. Reads/writes MUST use
`font.TexturePages[glyph.PageIndex]`.

### 4.2. Напрямок осі V / V-axis direction

**UA:** `min(V0,V1)` = верхній край на екрані, `max(V0,V1)` = нижній.
Напрямок (`U0<U1`/`V0<V1`) не гарантовано однаковий для всіх записів —
запис нового UV має зберігати напрямок оригінального. Формула:
`y0=round(min(V0,V1)×texHeight)` (верх), `y1=round(max(V0,V1)×texHeight)`
(низ, ексклюзивно).
**EN:** `min(V0,V1)` = the top edge on screen, `max(V0,V1)` = the
bottom. Direction (`U0<U1`/`V0<V1`) is not guaranteed uniform across
records — writing a new UV must preserve the original record's
direction. Formula: `y0=round(min(V0,V1)×texHeight)` (top),
`y1=round(max(V0,V1)×texHeight)` (bottom, exclusive).

### 4.3. `ink_width`/`cell_h` ≠ розмір UV-боксу / ≠ UV box size

**UA:** Метадані курсора двигуна (позиціонування наступного символу,
висота рядка), не розмір ділянки пікселів — та завжди береться з
UV-прямокутника.
**EN:** Engine cursor metadata (next-glyph positioning, line height),
not the pixel area size — that always comes from the UV rectangle.

### 4.4. `XAdvance`/`Bearing`/`CellHeight`

**UA:** `XAdvance ≈ InkWidth + стала` (ширина + інтервал). `Bearing` —
справжній left-side bearing, природно різний по формі літери.
`CellHeight` — стала в межах шрифту (висота рядка). Жодне з полів не
керує вертикальним позиціонуванням — те визначає лише UV-прямокутник.
При перезаписі коду новим гліфом: `XAdvance_new = InkWidth_new +
медіана(XAdvance−InkWidth)` реальних записів цього шрифту.
**EN:** `XAdvance ≈ InkWidth + a constant` (width + gap). `Bearing` is a
genuine left-side bearing, naturally different per glyph shape.
`CellHeight` is constant within a font (line height). None of these
fields control vertical positioning — only the UV rectangle does. When
overwriting a code with a new glyph: `XAdvance_new = InkWidth_new +
median(XAdvance−InkWidth)` of that font's real records.

### 4.5. Вертикальна геометрія слотів / slot vertical geometry

**UA:** BF1 — чорнило торкається всіх чотирьох країв власного
UV-боксу, 100% випадків (щільний crop під кожен гліф). BF2 — верхній
край завжди щільний crop; нижній залежить від форми: заокруглені
(`a,c,e,o,s,u`) торкаються низу (overshoot), плоскодонні
(`m,n,r,v,w,x,z`) — ні (спільна базова лінія із запасом).
**EN:** BF1 — ink touches all four edges of its own UV box in 100% of
cases (tight crop per glyph). BF2 — the top edge is always a tight
crop; the bottom depends on shape: rounded letters (`a,c,e,o,s,u`)
touch bottom (overshoot), flat-bottomed ones (`m,n,r,v,w,x,z`) do not
(shared baseline with headroom).

## 5. Конвертація пікселів для запису / Pixel conversion for writing

**UA:** `BF1LocalizationTool.FontGenerator.PixelConversion.GlyphPixelConverter.ToA4R4G4B4`
конвертує 32bpp BGRA у сирі байти A4R4G4B4:
- RGB завжди `0xFFF`, незалежно від Alpha (§3).
- 8→4 біт: округлення до найближчого значення (`round(v/17)`,
  `MidpointRounding.AwayFromZero`), не truncate — бо декодування
  робить `value*17`.

**EN:** `BF1LocalizationTool.FontGenerator.PixelConversion.GlyphPixelConverter.ToA4R4G4B4`
converts 32bpp BGRA into raw A4R4G4B4 bytes:
- RGB is always `0xFFF`, regardless of Alpha (§3).
- 8→4 bit: rounding to the nearest value (`round(v/17)`,
  `MidpointRounding.AwayFromZero`), not truncation — because decoding
  does `value*17`.

## 6. Критичні інваріанти UcfbReader/UcfbWriter / Critical invariants

**UA: ОБОВ'ЯЗКОВО НЕ РЕГРЕСУВАТИ** — при рефакторингу цих класів зберегти:
1. `UcfbReader.TryParseChildren` приймає дітей чанку ЛИШЕ якщо вони
   повністю покрили `size` батька; інакше весь вміст — сирі дані
   (`Children = []`).
2. Чанки з `FourCC` у `AlwaysLeafFourCC = ["BODY", "DATA", "PVS_", "INFO"]`
   завжди трактуються як листові без спроби парсингу дітей — шумні
   бінарні дані (пікселі/звук/шейдерні параметри) можуть випадково
   пройти перевірку (1) і розпарситись як фантомне дерево. Розширювати
   список можна лише після вичерпної побайтової перевірки на обох
   `core.lvl`, не за одним прикладом.
3. `UcfbWriter.WriteFile`: `replacements: Dictionary<long,byte[]>` не
   звіряє довжину заміни з оригіналом; розміри батьківських контейнерів
   перераховуються рекурсивно. Це дозволяє UV-прямокутнику рости (§7.8)
   без зміни довжини `BODY`/`FBOD` самих.

**EN: MUST NOT REGRESS** — when refactoring these classes, preserve:
1. `UcfbReader.TryParseChildren` accepts a chunk's children only if they
   fully cover the parent's `size`; otherwise the whole content is raw
   data (`Children = []`).
2. Chunks whose `FourCC` is in `AlwaysLeafFourCC = ["BODY", "DATA", "PVS_", "INFO"]`
   are always treated as leaves without attempting to parse children —
   noisy binary data (pixels/sound/shader parameters) can accidentally
   pass check (1) and parse as a phantom tree. Extending the list
   requires an exhaustive byte-level check on both `core.lvl` files, not
   a single example.
3. `UcfbWriter.WriteFile`: `replacements: Dictionary<long,byte[]>` does
   not check replacement length against the original; parent container
   sizes are recalculated recursively. This is what lets a UV rectangle
   grow (§7.8) without changing the length of `BODY`/`FBOD` itself.

**UA:** Перевірено: no-op `read → write` дає файл, байт-в-байт
ідентичний оригіналу, на обох `core.lvl` (`UcfbWriterNoOpRoundTripCommand`).
**EN:** Verified: a no-op `read → write` produces a file byte-identical
to the original, on both `core.lvl` (`UcfbWriterNoOpRoundTripCommand`).

## 7. Кирилиця: реальні Unicode-коди (production) / Cyrillic: real Unicode codes

**UA:** Виробничий підхід — `GenerateNoDonorCyrillicCoreCommand`. Для
кожної з 66 кириличних літер (33 × 2 регістри) додається новий
`FontGlyphRecord` з `Code` = справжній Unicode код-поінт (напр.
`'А'`=`0x0410`) у вільне місце атласу — без таблиці-мапера код↔літера.
GUI пише текст як звичайний UTF-16LE.
**EN:** Production approach: `GenerateNoDonorCyrillicCoreCommand`. For
each of the 66 Cyrillic letters (33 × 2 case) a new `FontGlyphRecord`
is added with `Code` set to the real Unicode code point (e.g.
`'А'`=`0x0410`), placed in free atlas space — no code↔letter mapping
table. The GUI writes text as plain UTF-16LE.

### 7.1. `HEAD[0:2]` — кількість гліфів / glyph count

**UA:** Перші 2 байти `HEAD` (`uint16` LE) = точна кількість записів
`FBOD` (ванільні шрифти: 226). Без оновлення цього поля нові літери
показуються як порожні прямокутники, не як помилка завантаження.
`FontResourceBuilder.BuildHead` виводить кількість з `font.Glyphs.Count`
— розсинхронізація `HEAD`/`FBOD` неможлива за конструкцією.
**EN:** The first 2 bytes of `HEAD` (`uint16` LE) equal the exact
`FBOD` record count (vanilla fonts: 226). Without updating this field,
new letters render as empty boxes, not a load error.
`FontResourceBuilder.BuildHead` derives the count from
`font.Glyphs.Count` — `HEAD`/`FBOD` desync is impossible by
construction.

### 7.2. `FBOD` мусить лишатись відсортованим за `Code`

**UA:** Оригінальні записи — строго зростаюча послідовність `Code`
(бінарний пошук у грі). Об'єднаний список
(`originalRecords.Concat(newRecords)`) сортується за `Code` перед
серіалізацією; кириличні коди (`>0xFF`) стають хвостом.
**EN:** Original records form a strictly increasing `Code` sequence
(the engine binary-searches for a glyph). The combined list
(`originalRecords.Concat(newRecords)`) is sorted by `Code` before
serialization; Cyrillic codes (`>0xFF`) become a sorted tail.

### 7.3. Вертикальна модель: великі літери / uppercase vertical model

**UA:** Інваріант `Bearing + BoxHeight(px) = CellHeight`. Для кожного
шрифту виводиться `FontMetricReference`: `BaselineOffset` = медіана
`CellHeight` реальних записів; `CapHeightGame` = `BaselineOffset −
медіана(Bearing A-Z)`. Масштаб `scale = CapHeightGame /
NaturalCapAscent` застосовується до висоти.
**EN:** Invariant `Bearing + BoxHeight(px) = CellHeight`. A per-font
`FontMetricReference` is derived: `BaselineOffset` = median `CellHeight`
of real records; `CapHeightGame` = `BaselineOffset − median(Bearing A-Z)`.
Scale `scale = CapHeightGame / NaturalCapAscent` is applied to height.

### 7.4. Ширина: масштаб + підлога / width: scale + floor

**UA:** Ширина масштабується окремо: `widthScale = CapWidthGame /
NaturalCapInkWidth` (`CapWidthGame` = медіана `InkWidth` рідних `A-Z`).
Для великих літер діє підлога `CapWidthFloor` = `InkWidth` найвужчої
рідної капітелі (типово `I`): `boxWidth = Math.Max(boxWidth,
CapWidthFloor)` — запобігає надмірному звуженню вузьких літер
(`І`/`Ї`/`Й`).
**EN:** Width is scaled separately: `widthScale = CapWidthGame /
NaturalCapInkWidth` (`CapWidthGame` = median `InkWidth` of native
`A-Z`). Uppercase letters get a floor, `CapWidthFloor` = `InkWidth` of
the narrowest native capital (typically `I`): `boxWidth =
Math.Max(boxWidth, CapWidthFloor)` — prevents over-narrowing already
narrow letters (`І`/`Ї`/`Й`).

### 7.5. Малі літери: "ядро + виступ" / lowercase "core + extension"

**UA:** `CoreHeightGame` = `BaselineOffset − медіана(Bearing реальних
a-z)` — спільна цільова висота тіла для всіх малих. `CoreMarginCapPx` =
`CapHeightGame − CoreHeightGame` — максимум виступу (дашок/хвіст) за
межі тіла. Для кожної малої `InkBounds` ділиться на перетин з
x-height-смугою ("тіло") і частини вище/нижче ("виступи"); тіло
масштабується до `CoreHeightGame` однаково для всіх, виступ — тим самим
коефіцієнтом, обмежений `CoreMarginCapPx`. Якщо природна ширина
перевищує ширину слота — стискається лише горизонтальний розмір; висота
тіла завжди `CoreHeightGame`, однакова для кожної малої незалежно від
ширини слота (інакше вузькі слоти на кшталт `і` втрачають спільну
базову лінію з рештою).
**EN:** `CoreHeightGame` = `BaselineOffset − median(Bearing of real
a-z)` — the shared target core height for all lowercase letters.
`CoreMarginCapPx` = `CapHeightGame − CoreHeightGame` — the max an
extension (dot/tail) may protrude beyond the core. Each lowercase
letter's `InkBounds` is split into the intersection with the x-height
band ("core") and the parts above/below ("extensions"); the core is
scaled to `CoreHeightGame` uniformly, extensions use the same factor,
capped at `CoreMarginCapPx`. If natural width exceeds slot width, only
the horizontal size is compressed; core height always stays
`CoreHeightGame` for every lowercase letter regardless of slot width
(otherwise narrow slots like `і` lose the shared baseline with the
rest).

### 7.6. Джерело гліфів і фінальний вибір шрифту / glyph source and final choice

**UA:** Гліфи рендеряться з `.ttf`-файлів через `PrivateFontRegistry`
(GDI+ `PrivateFontCollection`), не з системного реєстру шрифтів —
робить збірку відтворюваною на будь-якій машині. Реєстрація за
відносним шляхом файлу, не за назвою родини (Win32 `LOGFONT.lfFaceName`
обрізає назви на 31 символі; неоднозначні родини на кшталт голого
`Fira Sans` можуть дати непередбачуваний стиль). Системний
`Bahnschrift` навмисно не використовується — його ліцензія Windows
забороняє розповсюдження поза системою.
**EN:** Glyphs are rendered from `.ttf` files via `PrivateFontRegistry`
(GDI+ `PrivateFontCollection`), not the system font registry — this
makes the build reproducible on any machine. Registration is keyed by
file path, not family name (Win32 `LOGFONT.lfFaceName` truncates names
at 31 characters; ambiguous families like bare `Fira Sans` can resolve
to an unpredictable style). The system `Bahnschrift` font is
deliberately not used — its Windows license forbids redistribution
outside the OS.

**Фінальний вибір (`ResolveFontFamilyName`) / final choice:**

| Гра / Game | Розмір / Size | Файл / File |
|---|---|---|
| BF1 | усі 5 / all 5 | `SofiaSansExtraCondensed-Bold.ttf` |
| BF2 | large | `Unbounded-Bold.ttf` |
| BF2 | medium | `Unbounded-Black.ttf` |
| BF2 | small | `Unbounded-ExtraBold.ttf` |
| BF2 | tiny / super_tiny | `Exo2-ExtraBold.ttf` |

**UA:** Усі три родини — SIL Open Font License, з повним підтвердженим
покриттям українських літер (Є, і, Ї, Ґ) і без російського сліду в
провенансі дизайну.
**EN:** All three families are SIL Open Font License, with confirmed
full coverage of Ukrainian letters (Є, і, Ї, Ґ) and no Russian
provenance in their design history.

**UA:** `.ttf`-файли лежать у `BF1LocalizationTool.Diagnostic\Fonts\`;
`<None Include="Fonts\**\*.ttf" CopyToOutputDirectory="PreserveNewest" />`
у `.csproj` копіює їх у build output (`{AppContext.BaseDirectory}\Fonts\`,
звідки читає `PrivateFontRegistry`) автоматично після кожної збірки.
**EN:** The `.ttf` files live in `BF1LocalizationTool.Diagnostic\Fonts\`;
`<None Include="Fonts\**\*.ttf" CopyToOutputDirectory="PreserveNewest" />`
in the `.csproj` copies them to the build output
(`{AppContext.BaseDirectory}\Fonts\`, where `PrivateFontRegistry` reads
from) automatically after every build.

### 7.7. BF2 потребує збільшення шрифту / BF2 requires font enlargement

**UA:** BF1 нативно підтримує 1080p — масштабування не потрібне. BF2 —
ні. `GenerateEnlargedFontCoreCommand` (`FontRepacker.Repack`)
перепаковує кожен шрифт у свіжий атлас, множачи курсорні метрики
(`XAdvance`/`Bearing`/`CellHeight`/`InkWidth`) на коефіцієнт масштабу;
коди й таблиця символів не змінюються. Кирилична генерація й
BF2-збільшення виконуються одним кроком: спершу збільшення ванільного
атласу, потім рендер кирилиці одразу під фінальний розмір — уникає
подвійного bicubic-розмиття нових пікселів.
**EN:** BF1 natively supports 1080p — no scaling needed. BF2 does not.
`GenerateEnlargedFontCoreCommand` (`FontRepacker.Repack`) repacks each
font into a fresh atlas, multiplying cursor metrics
(`XAdvance`/`Bearing`/`CellHeight`/`InkWidth`) by a scale factor; codes
and the character table are unchanged. Cyrillic generation and BF2
enlargement run as one step: the vanilla atlas is enlarged first, then
Cyrillic glyphs are rendered directly at final size — avoiding a
double bicubic blur pass on the new pixels.

### 7.8. Розширення атласу й крайові пікселі / atlas growth and edge pixels

**UA:** `FontRepacker.RepackWithAdditions(src, additions, pageWidth, pageHeight)`
пакує наявні гліфи (пікселі 1-в-1) і нові разом через
`GlyphAtlasPacker` (shelf bin-packer, сортування за спаданням висоти),
додаючи стільки сторінок, скільки треба. `ReservedByte4` (§4) для нових
слотів береться як найчастіше значення серед реальних гліфів того
самого шрифту (mode варіюється між шрифтами, не константа).
**EN:** `FontRepacker.RepackWithAdditions(src, additions, pageWidth, pageHeight)`
packs existing glyphs (pixels 1:1) together with new ones via
`GlyphAtlasPacker` (a shelf bin-packer sorted by descending height),
adding as many pages as needed. `ReservedByte4` (§4) for new slots
takes the most frequent value among that font's real glyphs (the mode
varies between fonts, not a constant).

**UA:** `GlyphBoxFitRenderer.SlotPaddingPx=1` — нове чорнило
рендериться в повний природний розмір, а прозоре поле додається
розширенням фізичного слота (`BGRA(255,255,255,0)`, та сама конвенція,
що й ванільні шрифти, §3) — запобігає артефакту атласної фільтрації
("рамка" навколо широких великих літер) на щільно упакованих слотах.
Курсорні метрики лишаються похідними від чорнила; `Bearing`/`CellHeight`
розширюються рівно на padding, щоб масштаб слот→екран лишався 1:1.
**EN:** `GlyphBoxFitRenderer.SlotPaddingPx=1` — new ink is rendered at
full natural size, and a transparent margin is added by enlarging the
physical slot (`BGRA(255,255,255,0)`, the same convention vanilla fonts
use, §3) — this prevents an atlas-filtering artifact ("halo" around
wide uppercase letters) on tightly packed slots. Cursor metrics stay
derived from the ink; `Bearing`/`CellHeight` are enlarged by exactly the
padding so the slot→screen scale stays 1:1.

## 8. Локалізація тексту: розташування й хеш-функція / Text location and hash function

### 8.1. Розташування тексту / text location

**UA:** Повний скан інсталяції гри підтверджує: текст (`Locl`) лежить
лише у двох файлах, шрифти (`FBOD`/`FTEX`) — лише в одному.
**EN:** A full scan of the game install confirms text (`Locl`) lives in
exactly two files, fonts (`FBOD`/`FTEX`) in exactly one.

| Що / What | Де / Where | Скільки / Count |
|---|---|---|
| Текст / Text (`Locl`) | `Data\_LVL_PC\core.lvl` | 6 language tables |
| Текст / Text (`Locl`) | `AddOn\Tat3\Data\_lvl_pc\core.lvl` | 6 language tables |
| Шрифти / Fonts (`FBOD`/`FTEX`) | `Data\_LVL_PC\core.lvl` | 6 resources |

**UA:** Ніде більше — `common.lvl`, `shell.lvl`, `ingame.lvl`,
`mission.lvl`, звук, мапи не містять тексту. Аддон не має власних
шрифтів і не посилається на них — використовує вже завантажений атлас
базової гри.
**EN:** Nowhere else — `common.lvl`, `shell.lvl`, `ingame.lvl`,
`mission.lvl`, sound files, and maps contain no text. The add-on has no
fonts of its own and references none — it uses the base game's
already-loaded atlas.

### 8.2. Хеш-функція / hash function

**UA:** Реалізація: `BF1LocalizationTool.Core.Localization.SwbfStringHash`.
**EN:** Implementation: `BF1LocalizationTool.Core.Localization.SwbfStringHash`.

```
hash = 0x811C9DC5                       // FNV-1a offset basis
// UA: для кожного ASCII-байта ключа (нижній регістр):
// EN: for each ASCII byte of the key (lowercase):
    hash ^= b
    hash *= 0x01000193                  // FNV-1a prime
```

**UA:** FNV-1a, 32 біти, над ключем (не над показуваним текстом). Ключі
— крапковий шлях у нижньому регістрі (`level.tat3.objectives.1`,
`common.no`); у `Locl` за цим хешем лежить відображуваний рядок.
Приклад: `fnv1a("level.tat3.objectives.1") = 0x1CDDFE52 → "CAPTURE AND HOLD CPS"`.
Функція дозволяє генерувати нові хеші — додавати нові рядки
локалізації під власними ключами (приклад — §8.3).
**EN:** FNV-1a, 32-bit, over the key (not the displayed text). Keys are
a lowercase dotted path (`level.tat3.objectives.1`, `common.no`); the
`Locl` table stores the displayed string under that hash. Example:
`fnv1a("level.tat3.objectives.1") = 0x1CDDFE52 → "CAPTURE AND HOLD CPS"`.
The function allows generating new hashes — adding new localization
strings under new keys (example — §8.3).

### 8.3. Приклад: назва карти аддону Tat3 / example: the Tat3 add-on map name

**UA:** `AddOn\Tat3\addme.script` (Lua-байткод, ucfb → `scr_` → `BODY`)
містить `showstr = "TATOOINE: JABBA"`, переданий напряму через
`IFText_fnSetString` (локалізований виклик, хешує аргумент як ключ) —
на відміну від базових карт, які передають готовий ключ. Це єдиний
рядок гри поза таблицею `Locl`.
**EN:** `AddOn\Tat3\addme.script` (Lua bytecode, ucfb → `scr_` → `BODY`)
contains `showstr = "TATOOINE: JABBA"`, passed directly through
`IFText_fnSetString` (a localized call that hashes its argument as a
key) — unlike base maps, which pass a ready-made key. This is the
game's only string outside the `Locl` table.

**UA: Виправлення** (`PatchAddOnMapNameCommand`, Diagnostic категорія 7
пункт 9):

1. `showstr` → ключ `"level.tat3.name"` (обидва рівно 15 символів —
   довжина полів Lua/`BODY`/`scr_`/`ucfb` не змінюється);
2. у **базовий** `core.lvl` (усі 6 мовних таблиць) додається запис
   `Locl` з хешем `0x2DBABA24`.

**EN: Fix** (`PatchAddOnMapNameCommand`, Diagnostic category 7, item 9):

1. `showstr` → key `"level.tat3.name"` (both exactly 15 characters —
   Lua/`BODY`/`scr_`/`ucfb` field lengths are unchanged);
2. a `Locl` record with hash `0x2DBABA24` is added to the **base
   game's** `core.lvl` (all 6 language tables).

**UA:** Запис МАЄ лежати в базовому файлі, не в аддонному — екран
вибору карти резолвить ключі проти таблиці базової гри; аддонний
`core.lvl` завантажується разом з місією й ще не активний на екрані
меню. Підтверджено запуском гри.
**EN:** The record MUST be in the base file, not the add-on's — the map
selection screen resolves keys against the base game's table; the
add-on's `core.lvl` loads with the mission and is not yet active on the
menu screen. Confirmed by running the game.

### 8.4. Аддон Tat3: окрема таблиця, не змінюваний дублікат

**UA:** Таблиця аддону — майже повний дубль базової (2466 записів проти
2457): 2391 спільний хеш з ідентичним текстом і 22 унікальні рядки.
Переклад переноситься з базового CSV за хешем; доперекласти треба лише
ці 22. У GUI структурним майстром завжди є Оригінал — відкриття
робочого файлу переносить лише текст за `(Hash, Ordinal)`, ніколи не
шрифти чи структуру — це запобігає змішуванню документів різних
`core.lvl` в один робочий файл.
**EN:** The add-on table is nearly a full duplicate of the base one
(2466 records vs. 2457): 2391 shared hashes with identical text, and 22
unique strings. Translation is carried over from the base CSV by hash;
only those 22 need separate translation. In the GUI, the Original is
always the structural master — opening a working file transfers only
text by `(Hash, Ordinal)`, never fonts or structure — preventing
different `core.lvl` documents from being mixed into one working file.

---

*UA: Повний перелік класів і методів — див. XML-коментарі у вихідному
коді `BF1LocalizationTool.Core`/`.FontGenerator`/`.Diagnostic`.*
*EN: For the full class and method reference, see the XML doc comments
in the `BF1LocalizationTool.Core`/`.FontGenerator`/`.Diagnostic` source
code.*
