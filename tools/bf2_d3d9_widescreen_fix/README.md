# tools/bf2_d3d9_widescreen_fix — d3d9-проксі-фікс субтитрів фільмів

> Автор / Author: EMP_UA (https://github.com/EMP-UA) · Ліцензія / License: MIT

## 1. Що це / What this is

**UA:** Джерело (`d3d9_proxy.cpp`, `d3d9_proxy.def`) окремої, самостійної
DLL, яка виправляє зникнення субтитрів фільмів на широких екранах (16:9,
16:10). Повне обґрунтування (законність і межі, механізм дефекту, чому
хук саме на `DrawPrimitive`/`DrawIndexedPrimitive`) — коментар на початку
самого `d3d9_proxy.cpp`; опис дефекту й підтвердження —
`docs/BF2_MOVIE_SUBTITLE_FIX.md` (корінь репозиторію).

**EN:** Source (`d3d9_proxy.cpp`, `d3d9_proxy.def`) for a separate,
standalone DLL that fixes movie subtitles disappearing on wide screens
(16:9, 16:10). Full justification (legality and scope, the defect's
mechanism, why the hook sits on `DrawPrimitive`/`DrawIndexedPrimitive`) is
the comment at the top of `d3d9_proxy.cpp` itself; the defect description
and confirmation are in `docs/BF2_MOVIE_SUBTITLE_FIX.md` (repository
root).

## 2. Як це працює / How it works

**UA:** Одним реченням: гра завантажує цю DLL замість системної `d3d9.dll`
(стандартний порядок пошуку — тека застосунку РАНІШЕ за `System32`), DLL
одразу довантажує СПРАВЖНЮ системну `d3d9.dll` і перенаправляє туди геть
усі виклики без винятку, крім двох місць —
`IDirect3DDevice9::DrawPrimitive` і `IDirect3DDevice9::DrawIndexedPrimitive`
(єдині методи, що ГАРАНТОВАНО спрацьовують — сам сеттер полотна,
`SetVertexShaderConstantF`, у цій грі йде через `IDirect3DStateBlock9`,
минаючи власний vtable-хук повністю) — і перед кожним з них читає регістри
2D-полотна через справжній, ніколи не підмінений `GetVertexShaderConstantF`,
і, лише коли в даних справді присутня доведена асиметрія масштабу ТА
прочитане значення відповідає очікуваному для реальної ширини робочого
столу (`GetSystemMetrics(SM_CXSCREEN)`), виправляє їх через справжній,
ніколи не підмінений `SetVertexShaderConstantF`. **Жодного байта
`BattlefrontII.exe` не змінено — ні на диску, ні в пам'яті.**

**EN:** In one sentence: the game loads this DLL instead of the system
`d3d9.dll` (the standard search order — the application folder is checked
BEFORE `System32`); the DLL immediately loads the REAL system `d3d9.dll`
and forwards every single call to it unchanged, except for two spots —
`IDirect3DDevice9::DrawPrimitive` and `IDirect3DDevice9::DrawIndexedPrimitive`
(the only methods GUARANTEED to fire — the canvas setter itself,
`SetVertexShaderConstantF`, goes through `IDirect3DStateBlock9` in this
game, bypassing its own vtable hook entirely) — and right before each of
them reads the 2D-canvas registers via the real, never-replaced
`GetVertexShaderConstantF`, and, only when the data actually contains the
proven scale asymmetry AND the read value matches what's expected for the
real desktop width (`GetSystemMetrics(SM_CXSCREEN)`), corrects them via
the real, never-replaced `SetVertexShaderConstantF`. **Not one byte of
`BattlefrontII.exe` is changed — neither on disk nor in memory.**

## 3. Уже скомпільовано й вбудовано в BF1LocalizationTool / Already compiled and embedded into BF1LocalizationTool

**UA:** Готовий бінарник лежить як вбудований ресурс
`BF1LocalizationTool.Core/Bf2Widescreen/Data/MovieSubtitleD3D9Fix.dll` і
кладеться в теку гри (`GameData\d3d9.dll`) інсталятором (`installer/`),
як і решта файлів локалізації — не окремим кроком у коді застосунку.
Перезбирати вручну потрібно лише якщо змінюється сам `d3d9_proxy.cpp`.

**EN:** The built binary lives as the embedded resource
`BF1LocalizationTool.Core/Bf2Widescreen/Data/MovieSubtitleD3D9Fix.dll` and
is placed into the game folder (`GameData\d3d9.dll`) by the installer
(`installer/`), like every other localization file — not as a separate
step in the application's own code. Manual rebuilding is only needed if
`d3d9_proxy.cpp` itself changes.

## 4. Перезбірка / Rebuilding

### 4.1. Zig (canonical збірка) / Zig (canonical build)

**UA:** Вбудований у BF1LocalizationTool файл (`Data/MovieSubtitleD3D9Fix.dll`)
зібраний портативним компілятором Zig, самодостатнім (не потребує окремого
встановлення — самозавантажується з перевіркою SHA-256, див.
`MovieSubtitleD3D9FixZigBuilder.cs`, Core):

```bash
zig c++ -target x86-windows-gnu -shared -O2 -s \
  d3d9_proxy.cpp d3d9_proxy.def -o zig_d3d9.dll
```

Вихідний файл навмисно зветься `zig_d3d9.dll`, не `d3d9.dll` — щоб під час
діагностики не плутався зі справжньою системною `d3d9.dll` поруч; у GameData
його кладе під іменем `d3d9.dll` вже `.iss`-інсталятор. Zig-версія `0.16.0`,
офіційний архів `zig-x86_64-windows-0.16.0.zip` з `ziglang.org`.

**EN:** The file bundled into BF1LocalizationTool (`Data/MovieSubtitleD3D9Fix.dll`)
is built with the portable Zig compiler, which is self-contained (no
separate install — it self-downloads with a SHA-256 check, see
`MovieSubtitleD3D9FixZigBuilder.cs`, Core):

```bash
zig c++ -target x86-windows-gnu -shared -O2 -s \
  d3d9_proxy.cpp d3d9_proxy.def -o zig_d3d9.dll
```

The output file is deliberately named `zig_d3d9.dll`, not `d3d9.dll` — so it
isn't confused with the real system `d3d9.dll` sitting next to it during
diagnostics; the `.iss` installer is what places it into GameData as
`d3d9.dll`. Zig version `0.16.0`, official `zig-x86_64-windows-0.16.0.zip`
archive from `ziglang.org`.

### 4.2. Visual Studio (альтернатива) / Visual Studio (alternative)

**UA:** Новий проєкт "Dynamic-Link Library (DLL)", платформа
**Win32 (x86)**, додати `d3d9_proxy.cpp` і вказати `d3d9_proxy.def` як
Module Definition File (Linker → Input → Module Definition File). Лінкувати
з `d3d9.lib` НЕ ТРЕБА — реальна `d3d9.dll` завантажується динамічно
(`LoadLibraryA`/`GetProcAddress`), щоб уникнути конфлікту символів.
Байт-у-байт збіг зі збіркою Zig у цьому разі не очікується (інший
компілятор), лише функціональна еквівалентність.

**EN:** Create a new "Dynamic-Link Library (DLL)" project, platform
**Win32 (x86)**, add `d3d9_proxy.cpp`, and set `d3d9_proxy.def` as the
Module Definition File (Linker → Input → Module Definition File). Do NOT
link against `d3d9.lib` — the real `d3d9.dll` is loaded dynamically
(`LoadLibraryA`/`GetProcAddress`) to avoid a symbol conflict. A
byte-for-byte match with the Zig build isn't expected here either
(a different compiler), only functional equivalence.

### 4.3. Після перезбірки / After rebuilding

**UA:** Скопіювати новий `d3d9.dll` у
`BF1LocalizationTool.Core/Bf2Widescreen/Data/MovieSubtitleD3D9Fix.dll` і
оновити `RecordedDllSha256` у
`BF1LocalizationTool.Core/Bf2Widescreen/MovieSubtitleD3D9FixProvenance.cs`.

**EN:** Copy the new `d3d9.dll` over
`BF1LocalizationTool.Core/Bf2Widescreen/Data/MovieSubtitleD3D9Fix.dll` and
update `RecordedDllSha256` in
`BF1LocalizationTool.Core/Bf2Widescreen/MovieSubtitleD3D9FixProvenance.cs`.

## 5. Перевірено щодо збірки (не щодо поведінки в грі) / Verified about the build (not about in-game behaviour)

**UA:** `objdump -p d3d9.dll` → таблиця експорту містить РІВНО ім'я
`Direct3DCreate9` (без декорацій) — саме те, що імпортує
`BattlefrontII.exe` (перевірено проти PE import table самого `.exe`, він
бере з `d3d9.dll` рівно один символ). Залежності canonical (Zig-) збірки
DLL — `api-ms-win-crt-runtime-l1-1-0.dll`, `api-ms-win-crt-stdio-l1-1-0.dll`,
`api-ms-win-crt-heap-l1-1-0.dll`, `api-ms-win-crt-string-l1-1-0.dll`,
`api-ms-win-crt-private-l1-1-0.dll` (стандартний UCRT, присутній на
Windows 10/11), `KERNEL32.dll` і `USER32.dll` (`USER32.dll` додався у
версії V2 фіксу — потрібен для `GetSystemMetrics`). Формат — PE32
(32-біт), збігається з форматом `BattlefrontII.exe`. Збірка ДЕТЕРМІНОВАНА:
дві незалежні Zig-перезбірки з цього самого `d3d9_proxy.cpp`/`.def` на
реальній машині користувача дали БАЙТ-У-БАЙТ однаковий файл. Поточний
записаний SHA-256:
`d94edea6662a11219c7aa12ead532e762bf51927a5adb4e2af1ef4d7af938cf8`
(той самий, що в `MovieSubtitleD3D9FixProvenance.RecordedDllSha256`).

Ці факти підтверджують коректність і відтворюваність збірки — не поведінку
в грі. Емпірична перевірка в грі — окремий,
обов'язковий крок; для canonical Zig-збірки цей крок пройдено — фікс
підтверджено працюючим у грі (деталі — у `docs/BF2_MOVIE_SUBTITLE_FIX.md`,
розділ "Підтвердження").

**EN:** `objdump -p d3d9.dll` → the export table contains EXACTLY the name
`Direct3DCreate9` (undecorated) — exactly what `BattlefrontII.exe` imports
(verified against the `.exe`'s own PE import table — it takes exactly one
symbol from `d3d9.dll`). The canonical (Zig) build's dependencies are
`api-ms-win-crt-runtime-l1-1-0.dll`, `api-ms-win-crt-stdio-l1-1-0.dll`,
`api-ms-win-crt-heap-l1-1-0.dll`, `api-ms-win-crt-string-l1-1-0.dll`,
`api-ms-win-crt-private-l1-1-0.dll` (the standard UCRT, present on Windows
10/11), `KERNEL32.dll` and `USER32.dll` (`USER32.dll` was added in fix
version V2 — needed for `GetSystemMetrics`). Format — PE32 (32-bit),
matching `BattlefrontII.exe`'s format. The build is DETERMINISTIC: two
independent Zig rebuilds from this same `d3d9_proxy.cpp`/`.def` on the
user's real machine produced a BYTE-FOR-BYTE identical file. Current
recorded SHA-256:
`d94edea6662a11219c7aa12ead532e762bf51927a5adb4e2af1ef4d7af938cf8` (the
same value as `MovieSubtitleD3D9FixProvenance.RecordedDllSha256`).

These facts confirm the build's correctness and reproducibility — not its
in-game behaviour. Empirical in-game verification is a
separate, mandatory step; for the canonical Zig build this step has been
completed — the fix is confirmed working in-game (details in
`docs/BF2_MOVIE_SUBTITLE_FIX.md`, "Confirmation" section).

## 6. Перевірка походження всередині BF1LocalizationTool / Verifying provenance inside BF1LocalizationTool

**UA:** Для модерації (наприклад, NexusMods) або будь-кого, хто хоче
перевірити, звідки взявся вбудований `d3d9.dll`, НЕ потрібно вірити на
слово — є два незалежні способи перевірки.

**EN:** For moderation (e.g. NexusMods) or anyone who wants to check where
the bundled `d3d9.dll` came from, no one has to take it on faith — there
are two independent ways to verify it.

### 6.1. Крок 1: Zig (вбудований інструмент) / Step 1: Zig (built-in tool)

**UA:** У BF1LocalizationTool.Diagnostic є конкретний пункт меню —
"Відео та субтитри" → "★★ ПОХОДЖЕННЯ d3d9.dll" — який розпаковує з власних
вбудованих ресурсів програми і бінарник, і цей самий вихідний код, рахує
SHA-256 наживо, і сам завантажує (з обов'язковою перевіркою SHA-256 перед
розпакуванням, кешує поруч із exe) портативний компілятор Zig з
`ziglang.org` (без MSYS2, без Visual Studio C++ workload, без будь-чого,
що інакше довелося б встановлювати вручну) — і перезбирає код НИМ,
canonical тулчейном вбудованого файла, і звіряє результат байт-у-байт із
тим, що вбудовано. Реалізація: `GenerateD3D9FixProvenanceReportCommand.cs`
(Diagnostic) поверх `MovieSubtitleD3D9FixProvenance.cs` і
`MovieSubtitleD3D9FixZigBuilder.cs` (Core).

**EN:** BF1LocalizationTool.Diagnostic has a concrete menu item — "Movies &
subtitles" → "★★ PROVENANCE of d3d9.dll" — that extracts, from the
program's own embedded resources, both the binary and this exact source
code, hashes them live with SHA-256, and itself downloads (with a
mandatory SHA-256 check before extraction, cached next to the exe) the
portable Zig compiler from `ziglang.org` (no MSYS2, no Visual Studio C++
workload, nothing that would otherwise need installing by hand) — and
rebuilds the code with THAT, the bundled file's canonical toolchain, and
compares the result byte-for-byte against what's bundled. Implementation:
`GenerateD3D9FixProvenanceReportCommand.cs` (Diagnostic) on top of
`MovieSubtitleD3D9FixProvenance.cs` and `MovieSubtitleD3D9FixZigBuilder.cs`
(Core).

### 6.2. Крок 2: GitHub Actions (незалежно від локальної машини) / Step 2: GitHub Actions (independent of the local machine)

**UA:** Є й ДРУГИЙ, повністю незалежний від локальної машини спосіб:
GitHub Actions workflow `.github/workflows/verify-d3d9-fix.yml` — на
чистому `ubuntu-latest`-раннері GitHub САМ завантажує й перевіряє SHA-256
офіційного архіву Zig (той самий довірчий механізм, що й
`MovieSubtitleD3D9FixZigBuilder.cs`), перезбирає `d3d9.dll` з коду
репозиторію й звіряє байт-у-байт із закомміченим бінарником при КОЖНОМУ
push/PR, що чіпає цей код. Це публічний, незалежний від автора результат
(зелена галочка/бейдж на GitHub) — сильніший доказ для модерації, ніж
локальний прогін на машині автора, і не вимагає нічого встановлювати ні
від модератора, ні від користувача.

**EN:** There is also a SECOND, fully machine-independent way: the GitHub
Actions workflow `.github/workflows/verify-d3d9-fix.yml` — on a clean
`ubuntu-latest` runner, GitHub itself downloads and SHA-256-verifies the
official Zig archive (the same trust mechanism as
`MovieSubtitleD3D9FixZigBuilder.cs`), rebuilds `d3d9.dll` from the
repository's own source, and compares it byte-for-byte against the
committed binary on EVERY push/PR that touches this code. That is a
public result independent of the repo's own author (a green check/badge
on GitHub) — stronger evidence for moderation than a local run on the
author's own machine, and it requires nothing to be installed by either
the moderator or the user.

## 7. Встановлення вручну (без BF1LocalizationTool) / Manual install (without BF1LocalizationTool)

**UA:** Скопіювати зібраний `d3d9.dll` у теку гри, поряд із
`BattlefrontII.exe` (туди ж, де лежить сам `.exe`, — НЕ в підтеку
`GameData` окремо, а саме поряд з виконуваним файлом). Видалення файла
повністю знімає фікс.

**EN:** Copy the built `d3d9.dll` into the game folder, next to
`BattlefrontII.exe` (the same folder as the executable itself — NOT a
separate `GameData` subfolder, but right beside the `.exe`). Deleting the
file fully removes the fix.

## 8. Прозорість: лог / Transparency: log file

**UA:** DLL завжди (без «тихого» варіанта збирання) пише лог
`bf2_widescreen_fix.log` у тій самій теці, де лежить сама DLL (поряд із
`BattlefrontII.exe`), у режимі додавання (append) — дані попередніх
запусків не стираються. Це єдиний спосіб діагностувати поведінку на
нестандартному апаратному забезпеченні користувача, тому вимкнути запис
не можна.

Розмір обмежено 25 МБ (`LOG_SIZE_LIMIT_BYTES`): по досягненні межі
записується один підсумковий рядок, і подальший запис у цьому сеансі
призупиняється. Обґрунтування межі: формат логування зі стисненням за
сигнатурою пише по рядку на кожну зміну елемента HUD; кілька хвилин
реального запуску дали 5,5 МБ, а найбільший зафіксований запуск усієї
кампанії — 5,49 МБ, тож 25 МБ лишає запас приблизно в 4,5 рази.

Вміст логу: рядки статусу ініціалізації (успіх/невдача завантаження
справжньої системної `d3d9.dll`, включно з повним шляхом до неї),
підтвердження прив'язки до пристрою рендеру, роздільність робочого
столу (один раз за сеанс) і діагностичні рядки корекції масштабу для
кожного елемента HUD.

DLL не встановлює жодних мережевих з'єднань — вихідний код підключає
лише `<windows.h>`, без жодного мережевого API (перевірено читанням
`d3d9_proxy.cpp`). Файл логу можна видалити в будь-який момент — на
роботу самого фіксу це не впливає.

**EN:** The DLL always writes a `bf2_widescreen_fix.log` log (there is
no "quiet" build variant) in the same folder as the DLL itself (next to
`BattlefrontII.exe`), in append mode — earlier runs' data is not
erased. This is the only way to diagnose behaviour on a user's
non-standard hardware, so logging cannot be turned off.

Size is capped at 25 MB (`LOG_SIZE_LIMIT_BYTES`): once the cap is
reached, one final summary line is written and further logging is
suppressed for that session. Rationale for the cap: the
signature-compressed log format writes one line per HUD-element
change; a few minutes of a real run produced 5.5 MB, and the largest
recorded full-campaign run was 5.49 MB, so 25 MB leaves roughly a
4.5x margin.

Log contents: initialization status lines (success/failure of loading
the real system `d3d9.dll`, including its full path), render-device
hook confirmation, desktop resolution (logged once per session), and
per-HUD-element scale-correction diagnostic lines.

The DLL makes no network connections of any kind — the source only
includes `<windows.h>`, with no networking API whatsoever (verified by
reading `d3d9_proxy.cpp`). The log file can be deleted at any time
without affecting the fix itself.
