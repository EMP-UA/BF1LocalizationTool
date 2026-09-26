# BF2-UA-rem: генерація чотирьох фінальних файлів / BF2-UA-rem: generation of the four final files

## 0. Область застосування / Scope

**UA:** Один пункт меню `BF1LocalizationTool.Diagnostic` (категорія
«★★★ ФІНАЛЬНА ЗБІРКА») генерує всі чотири файли, потрібні для копіювання
в `GameData` встановленої гри: `d3d9.dll`, `data\_lvl_pc\core.lvl`,
`data\_lvl_pc\ingame.lvl`, `data\_lvl_pc\shell.lvl`. Кожен файл має
власний підпункт меню, і є додатковий підпункт, який виконує всі чотири
поспіль. Результат складається в теку `final-assembly-output\GameData\...`,
що безпосередньо повторює структуру теки гри — вміст цієї теки
копіюється поверх інсталяції гри без перейменувань.

**EN:** A single `BF1LocalizationTool.Diagnostic` menu category
("★★★ FINAL ASSEMBLY") generates all four files needed for the installed
game's `GameData` folder: `d3d9.dll`, `data\_lvl_pc\core.lvl`,
`data\_lvl_pc\ingame.lvl`, `data\_lvl_pc\shell.lvl`. Each file has its own
menu sub-item, plus one sub-item that runs all four in sequence. The
result is assembled under `final-assembly-output\GameData\...`, which
directly mirrors the game's own folder structure — the folder's contents
are copied straight over the game installation with no renaming.

**UA:** Методи, що виконують кожен пункт (`RunFinalAssemblyCore`,
`RunFinalAssemblyShell`, `RunFinalAssemblyIngame`, `RunFinalAssemblyD3D9`,
`RunFinalAssemblyAll`), лежать у `Program.cs`; кожен внутрішній крок
викликає власний клас-генератор, названий нижче. Усі шляхи в цих методах
обчислюються відносно теки застосунку (`AppContext.BaseDirectory`) —
захардкоджених абсолютних шляхів немає. Проміжні файли кожного
багатокрокового ланцюжка лежать у тимчасових теках поза
`final-assembly-output` і видаляються одразу після використання, тож ця
тека завжди містить лише готові файли гри.

**EN:** The methods behind each item (`RunFinalAssemblyCore`,
`RunFinalAssemblyShell`, `RunFinalAssemblyIngame`, `RunFinalAssemblyD3D9`,
`RunFinalAssemblyAll`) live in `Program.cs`; each internal step calls its
own generator class, named below. Every path in these methods is resolved
relative to the application's own folder (`AppContext.BaseDirectory`) —
there are no hardcoded absolute paths. Each multi-step chain's
intermediate files sit in temporary folders outside
`final-assembly-output` and are deleted right after use, so that folder
always holds only the finished game files.

## 1. core.lvl — три послідовні кроки / three sequential steps

**UA:** Крок 1/3 — збільшення ванільного шрифту ×1.5
(`GenerateEnlargedFontCoreCommand`, тимчасова тека, видаляється одразу
після використання). Крок 2/3 — вбудовування кириличних гліфів (Fira
Sans) прямими Unicode-кодами напряму у вже збільшений атлас
(`GenerateNoDonorCyrillicCoreCommand`), без донорських файлів. Крок 3/3 —
фікс застарілої висоти шрифтів (HEAD[3]) зі звіркою проти ванільного
`core.lvl` (`GenerateFontHeadHeightFixCoreCommand`). Вхід усього
ланцюжка — ванільний `core.lvl`, знайдений під `reference-files\BF2\`
через `FindGameFile` (рекурсивний пошук за іменем файлу — розкладка
всередині `reference-files\<Гра>\` не фіксована, різні файли можуть
лежати як пласко, так і вкладено в повне дерево гри); результат —
`final-assembly-output\GameData\data\_lvl_pc\core.lvl` з кирилицею й
виправленим HEAD, але ще з англійським текстом.

**EN:** Step 1/3 — enlarging the vanilla font ×1.5
(`GenerateEnlargedFontCoreCommand`, a temp folder deleted right after
use). Step 2/3 — embedding Cyrillic glyphs (Fira Sans) with direct
Unicode codes straight into the already-enlarged atlas
(`GenerateNoDonorCyrillicCoreCommand`), with no donor files. Step 3/3 —
fixing the stale font height (HEAD[3]), cross-checked against the vanilla
`core.lvl` (`GenerateFontHeadHeightFixCoreCommand`). The whole chain's
input is the vanilla `core.lvl`, found under `reference-files\BF2\` via
`FindGameFile` (a recursive search by file name — the layout inside
`reference-files\<Game>\` is not fixed, a given file can sit either flat
or nested inside the full game tree); the result is
`final-assembly-output\GameData\data\_lvl_pc\core.lvl` with Cyrillic
glyphs and the corrected HEAD, but still with English text.

**UA:** Переклад рядків застосовується окремо, через GUI Translator,
після цього кроку — GUI змінює лише текст і не чіпає шрифт чи HEAD.

**EN:** String translation is applied separately, via the GUI Translator,
after this step — the GUI changes only the text and does not touch the
font or HEAD.

## 2. shell.lvl

**UA:** Один крок — фікс розкладки (`GenerateAnchorFixShellCommand`), що
включає й фікс фону. Вхід — ванільний `shell.lvl`, знайдений під
`reference-files\BF2\` через `FindGameFile`. `shell.lvl` не містить
чанків Locl (перевірено пошуком за сигнатурою) — текстових рядків для
перекладу тут немає, результат готовий одразу під іменем
`final-assembly-output\GameData\data\_lvl_pc\shell.lvl`.

**UA:** ⚠ Під `reference-files\BF2\` існує кілька файлів з іменем
`shell.lvl` (основний, у `GameData\data\_lvl_pc\`, та окремі — у
вкладених `load\` і `sound\`, а також поза `GameData`). `FindGameFile`
повертає перший знайдений збіг без явної переваги якомусь з них — який
саме файл це буде, залежить від порядку обходу тек файловою системою.

**EN:** One step — the layout fix (`GenerateAnchorFixShellCommand`),
which also includes the background fix. Input — the vanilla `shell.lvl`,
found under `reference-files\BF2\` via `FindGameFile`. `shell.lvl` has
no Locl chunks (verified by a signature search) — there are no text
strings to translate here, the result is ready right away as
`final-assembly-output\GameData\data\_lvl_pc\shell.lvl`.

**EN:** ⚠ Under `reference-files\BF2\` several files are named
`shell.lvl` (the main one, in `GameData\data\_lvl_pc\`, plus separate
ones under the nested `load\` and `sound\`, and one outside `GameData`).
`FindGameFile` returns the first match found with no explicit preference
among them — which exact file that is depends on the file system's own
folder-traversal order.

## 3. ingame.lvl — три послідовні кроки / three sequential steps

**UA:** Крок 1/3 — розкладка в бою (`GenerateAnchorFixIngameCommand`).
Крок 2/3 — зазор «Кількість бійців»
(`GenerateSpawnSelectUnitCountGapFixCommand`), застосований до результату
кроку 1/3. Крок 3/3 — зсув переліку класів
(`GenerateSpawnSelectListTopOffsetFixCommand`), застосований до
результату кроку 2/3. Вхід усього ланцюжка — ванільний
`ingame.lvl`, знайдений під `reference-files\BF2\` через `FindGameFile`;
кожен крок читає результат попереднього, а не окремий ванільний файл
повторно. `ingame.lvl` не містить чанків Locl
(перевірено пошуком за сигнатурою) — перекладу тут не потрібно; результат —
`final-assembly-output\GameData\data\_lvl_pc\ingame.lvl`.

**EN:** Step 1/3 — in-battle layout (`GenerateAnchorFixIngameCommand`).
Step 2/3 — the "Unit Count" gap
(`GenerateSpawnSelectUnitCountGapFixCommand`), applied to step 1/3's
result. Step 3/3 — the class-list offset
(`GenerateSpawnSelectListTopOffsetFixCommand`), applied to step 2/3's
result. The whole chain's input is the vanilla
`ingame.lvl`, found under `reference-files\BF2\` via `FindGameFile`; each
step reads the previous step's result rather than the vanilla file
again. `ingame.lvl` has no Locl
chunks (verified by a signature search) — no translation is needed here;
the result is `final-assembly-output\GameData\data\_lvl_pc\ingame.lvl`.

**UA:** Окремі самостійні пункти меню тієї самої категорії, що будують
проміжні фікси розкладки та зазору поодинці на ванільній основі (для
точкового тестування одного фікса), лишаються без змін і не є частиною
цього ланцюжка — ланцюжок вище виконує власні внутрішні виклики тих самих
класів-генераторів, з проміжними файлами в тимчасовій теці поза
`final-assembly-output`.

**EN:** Separate standalone menu items in the same category that build
the intermediate layout and gap fixes individually from the vanilla base
(for spot-testing one fix at a time) remain unchanged and are not part of
this chain — the chain above makes its own internal calls to the same
generator classes, keeping intermediate files in a temporary folder
outside `final-assembly-output`.

## 4. d3d9.dll

**UA:** Компілюється напряму командою Zig (`GenerateD3D9FixBuildCommand`
→ `MovieSubtitleD3D9FixZigBuilder.CompileAsync`) з джерела, вбудованого в
застосунок (`MovieSubtitleD3D9FixProvenance`, той самий, що й
`tools/bf2_d3d9_widescreen_fix/d3d9_proxy.cpp`), з іменем виводу
`-o zig_d3d9.dll`. Компіляція відбувається в окремій тимчасовій теці;
готовий файл копіюється під кінцевим іменем
`final-assembly-output\GameData\d3d9.dll`, тимчасова тека видаляється
одразу після копіювання.

**EN:** Compiled directly with Zig (`GenerateD3D9FixBuildCommand` →
`MovieSubtitleD3D9FixZigBuilder.CompileAsync`) from the source bundled
with the application (`MovieSubtitleD3D9FixProvenance`, the same source
as `tools/bf2_d3d9_widescreen_fix/d3d9_proxy.cpp`), with the output name
`-o zig_d3d9.dll`. Compilation happens in a separate temporary folder;
the finished file is copied under the final name
`final-assembly-output\GameData\d3d9.dll`, and the temporary folder is
deleted right after the copy.

**UA:** Ім'я, передане лінкеру через `-o`, вбудовується в сам PE-файл,
тож `-o zig_d3d9.dll` і `-o d3d9.dll` дають різні байти з того самого
джерела й компілятора; фінальне ім'я `d3d9.dll` присвоюється лише
копіюванням готового файлу, що не змінює його вміст. Результат цього
кроку збігається байт-у-байт із SHA-256, записаним у
`MovieSubtitleD3D9FixProvenance.RecordedDllSha256` — тим самим значенням,
яке звіряє окремий пункт меню походження d3d9.dll (категорія «Відео та
субтитри», підпункт «★★ ПОХОДЖЕННЯ d3d9.dll»), а також незалежна
публічна перевірка на CI
(`.github/workflows/verify-d3d9-fix.yml`), яка на чистому ubuntu-раннері
збирає d3d9.dll з того самого джерела репозиторію тим самим рецептом і
звіряє результат байт-у-байт із бінарником, вбудованим у застосунок.

**EN:** The name passed to the linker via `-o` is embedded in the PE file
itself, so `-o zig_d3d9.dll` and `-o d3d9.dll` produce different bytes
from the same source and compiler; the final `d3d9.dll` name is assigned
only by copying the finished file, which does not change its contents.
This step's result matches, byte-for-byte, the SHA-256 recorded in
`MovieSubtitleD3D9FixProvenance.RecordedDllSha256` — the same value the
separate d3d9.dll provenance menu item checks against (the "Movies &
subtitles" category, "★★ PROVENANCE of d3d9.dll" sub-item), and the same
value an independent, public CI check
(`.github/workflows/verify-d3d9-fix.yml`) verifies on a clean ubuntu
runner by building d3d9.dll from the same repository source with the same
recipe and comparing the result byte-for-byte against the binary bundled
with the application.
