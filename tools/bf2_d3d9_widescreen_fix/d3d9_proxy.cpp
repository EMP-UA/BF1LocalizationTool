// ============================================================================
// BF2 Widescreen Canvas Fix — d3d9.dll proxy
// ============================================================================
//
// UA: ЗАКОННІСТЬ І МЕЖІ (той самий принцип, що й у
//     Bf2Exe/MovieSubtitleAlphaScanner.cs). Цей файл НІКОЛИ не читає, не
//     пише і не завантажує BattlefrontII.exe як файл, і не містить жодного
//     байта коду чи даних гри. Це самостійна DLL, яку гра завантажує ЗАМІСТЬ
//     системної d3d9.dll (стандартний, документований порядок пошуку DLL
//     Windows — тека застосунку перевіряється РАНІШЕ за System32; той самий
//     механізм, яким десятиліттями користуються широко визнані спільноти
//     тонкого моддингу для DirectX-ігор, напр. ReShade чи фікси з WSGF).
//     Проксі одразу довантажує СПРАВЖНЮ системну d3d9.dll за повним шляхом і
//     перенаправляє до неї геть УСІ виклики без винятку — окрім РІВНО ДВОХ
//     методів, IDirect3DDevice9::DrawPrimitive і
//     IDirect3DDevice9::DrawIndexedPrimitive, і то лише щоб ПЕРЕД кожним
//     кадром перевірити (через справжній, ніколи не підмінений
//     GetVertexShaderConstantF) поточні регістри 2D-полотна й, коли в них
//     справді є асиметрія, виправити їх (через справжній, ніколи не
//     підмінений SetVertexShaderConstantF) ПЕРЕД тим, як кадр малюється.
//     Файл BattlefrontII.exe на диску користувача не змінюється НІ НА ОДИН
//     БАЙТ — видалення цієї DLL з теки гри повністю й миттєво прибирає фікс.
//
//     МЕХАНІЗМ ДЕФЕКТУ. Рушій BattlefrontII.exe переводить 2D-інтерфейс у
//     "полотно" 640x480 (пропорція 4:3), але камера, що показує це полотно,
//     має РЕАЛЬНУ пропорцію екрана. На 4:3/5:4 це збігається (або дає
//     навіть більше запасу) — усе видно. На 16:9/16:10 камера бачить МЕНШЕ
//     полотна по вертикалі (наприклад, 640x360 замість 640x480 на 16:9), і
//     елементи, прив'язані до самого верху/низу "безпечної зони" (зокрема
//     субтитри фільмів), опиняються поза кадром.
//
//     Причина — асиметрія в самому масштабуванні полотна: горизонтальний
//     масштаб рахується як 640/W (правильно, узгоджено з тим, що бачить
//     камера), а вертикальний — як 480/H (НЕ узгоджено з камерою, яка
//     насправді показує 640/аспект по вертикалі, а не 480). Мінімальне і
//     геометрично коректне виправлення: зробити вертикальний масштаб РІВНИМ
//     горизонтальному (640/W замість 480/H) для тих самих елементів, де це
//     розходження застосовується.
//
//     ЧОМУ НЕ ПРЯМИЙ ХУК SetVertexShaderConstantF. Перехоплення САМЕ
//     IDirect3DDevice9::SetVertexShaderConstantF напряму не працює: у цій
//     грі BeginScene/EndScene/DrawPrimitive/DrawIndexedPrimitive надійно
//     спрацьовують через vtable-хук, а SetRenderState/SetTexture/
//     SetVertexShader/SetVertexShaderConstantF — НІКОЛИ, жодного разу,
//     навіть під час підтвердженого реального рендерингу. Причина —
//     відомий для D3D9 механізм IDirect3DStateBlock9: гра записує ці
//     виклики в StateBlock один раз і надалі застосовує їх через
//     StateBlock::Apply(), який діє напряму на рівні драйвера, минаючи
//     публічні Set-методи об'єкта повністю — жодний vtable-хук на цих
//     конкретних слотах у принципі не може побачити такий виклик. Тому цей
//     фікс перехоплює НЕ сам сеттер, а лише надійно перехоплювані
//     DrawPrimitive/DrawIndexedPrimitive, і ПЕРЕД кожним із них читає
//     поточний стан регістрів полотна напряму через справжній
//     GetVertexShaderConstantF (слот 95 — ніколи не підмінюється, тож
//     завжди повертає справжнє значення незалежно від того, чи стан було
//     встановлено напряму, чи через StateBlock) і за потреби виправляє його
//     напряму через справжній SetVertexShaderConstantF (слот 94 — теж
//     ніколи не підмінюється).
//
//     ЧОМУ ХУК СТАВИТЬСЯ НА КОЖЕН СТВОРЕНИЙ IDirect3DDevice9 ОКРЕМО. Під
//     час запуску гри IDirect3D9::CreateDevice викликається ТРИЧІ,
//     створюючи три РІЗНІ об'єкти IDirect3DDevice9 за різними адресами в
//     пам'яті, з ВЛАСНИМИ (не спільними) vtable-таблицями — і лише
//     ОСТАННІЙ з них реально використовується для кадрового рендерингу.
//     Хук, встановлений лише на перший створений пристрій, НІКОЛИ не бачить
//     жодного реального виклику. Тому кожен щойно створений
//     IDirect3DDevice9 хукається окремо (з дедублікацією за адресою), до
//     MAX_HOOKED_DEVICES штук.
//
//     ЧОМУ "ОЧІКУВАНИЙ" scaleX РАХУЄТЬСЯ ВІД GetSystemMetrics(SM_CXSCREEN),
//     А НЕ ВІД D3DPRESENT_PARAMETERS::BackBufferWidth. Той самий регістровий
//     "підпис" виклику (StartRegister=12, Vector4fCount=9) у реальній грі
//     використовується НЕ тільки для полотна 2D-інтерфейсу, а й для інших,
//     не пов'язаних з полотном випадків (зокрема анімації "виїзду" кнопки
//     меню, де scaleX завжди точно 1.0). Наївна перевірка "просто асиметрія"
//     (X != Y) хибно спрацьовує на анімації кнопки меню, ламаючи її
//     плавність. D3DPRESENT_PARAMETERS::BackBufferWidth для фільтрації не
//     підходить: у цій грі цей параметр завжди лишається ~800 незалежно від
//     реальної роздільної здатності екрана і незалежно від параметра
//     запуску "-resolution" — гра завжди створює внутрішній D3D9-пристрій із
//     буфером ~800 і розтягує зображення до екрана якимось іншим механізмом.
//     Натомість справжнє значення scaleX полотна точно збігається з 640 /
//     (реальна ширина робочого столу Windows). Тому очікуваний scaleX
//     рахується як 640.0f / GetSystemMetrics(SM_CXSCREEN) (справжня,
//     фіксована ширина робочого столу, а не змінний і невідповідний
//     внутрішній буфер гри), і виправлення застосовується лише коли
//     прочитаний scaleX дійсно близький до цього значення (допуск ±1%) —
//     цей фільтр надійно відсіює всі інші, не пов'язані з полотном випадки.
//
//     Хук встановлюється НЕ інлайн-патчем коду виконуваного файлу гри, а
//     прямим перезаписуванням ОКРЕМИХ записів (по 4 байти) усередині VTABLE
//     самого об'єкта IDirect3DDevice9 — таблиця методів фізично лишається
//     там, де її створив системний d3d9.dll (у пам'яті цього модуля), сам
//     вказівник об'єкта на vtable НІКОЛИ не змінюється; підміняється лише
//     вміст окремих слотів (DrawPrimitive, DrawIndexedPrimitive) у вже
//     наявній таблиці, через тимчасову зміну захисту сторінки пам'яті
//     (VirtualProtect). Підміна ВСЬОГО vtable-вказівника IDirect3DDevice9
//     на нову таблицю (навіть побайтовий клон без жодної зміни) призводить
//     до того, що гра одразу й тихо завершує процес — тому для
//     IDirect3DDevice9 застосовується лише пряме перезаписування окремих
//     слотів усередині оригінальної, незмінно розташованої таблиці. Для
//     IDirect3D9::CreateDevice (малий, одноразовий інтерфейс — потрібен
//     лише раз, щоб отримати вказівник на щойно створений пристрій)
//     підміна всього вказівника на копію таблиці лишається безпечною.
//
//     ЧОМУ САМЕ ТАК, А НЕ ЧЕРЕЗ .lvl/Lua. Проєкт уже має паралельний,
//     чистий data-only шлях (Bf2Widescreen/BattleIntroSubtitleProbePatcher.cs)
//     — накласти НОВИЙ текстовий об'єкт поверх ролика через ту саму
//     Lua/IFScreen-систему, якою патчиться решта інтерфейсу. Але сам віджет
//     підпису (той, що вже є в грі) керується повністю нативним, вшитим у
//     .exe кодом: прямокутник, вирівнювання, відступ до низу, масштаб
//     полотна — усе обчислюється або константами в .exe, або з розмірів
//     екрана; жодного значення з файлу даних. ВИПРАВИТИ САМЕ ЦЕЙ віджет
//     через дані неможливо в принципі. DLL-проксі — єдиний спосіб виправити
//     САМЕ ЦЕЙ конкретний механізм, не займаючи жодного байта .exe.
//
//     ПОТОЧНА УМОВА КОРЕКЦІЇ. CorrectCanvasIfNeeded виправляє полотно, коли
//     scaleY ≈ 480/(висота робочого столу) — єдина умова корекції.
//
//     ПІДТВЕРДЖЕНО В ГРІ. Перевірено реальною грою на 1920x1080: підпис
//     ролика присутній і читається, бойова панель інформації про зброю
//     (шкала ХП/стаміни, іконки зброї) НЕ спотворена. Деталі — у
//     docs/BF2_MOVIE_SUBTITLE_FIX.md, розділ "Підтвердження".
//
// EN: LEGALITY AND SCOPE (the same principle as in
//     Bf2Exe/MovieSubtitleAlphaScanner.cs). This file NEVER reads, writes,
//     or loads BattlefrontII.exe as a file, and contains not one byte of
//     game code or data. It is a standalone DLL the game loads INSTEAD of
//     the system d3d9.dll (the standard, documented Windows DLL search
//     order — the application folder is checked BEFORE System32; the same
//     mechanism widely-recognized fine-modding communities for DirectX
//     games have used for decades, e.g. ReShade or WSGF fixes). The proxy
//     immediately loads the REAL system d3d9.dll by its full path and
//     forwards absolutely every call to it unchanged — except for EXACTLY
//     TWO methods, IDirect3DDevice9::DrawPrimitive and
//     IDirect3DDevice9::DrawIndexedPrimitive, and only to check, right
//     BEFORE each frame (via the real, never-replaced
//     GetVertexShaderConstantF), the current 2D-canvas registers, and, when
//     they truly contain an asymmetry, correct them (via the real,
//     never-replaced SetVertexShaderConstantF) right BEFORE the frame is
//     drawn. BattlefrontII.exe on the user's disk is not changed by ONE
//     SINGLE BYTE — deleting this DLL from the game folder fully and
//     instantly removes the fix.
//
//     THE DEFECT'S MECHANISM. BattlefrontII.exe's engine converts the 2D
//     interface into a 640x480 "canvas" (a 4:3 proportion), but the camera
//     that shows that canvas has the REAL screen proportion. At 4:3/5:4 the
//     two coincide (or even give extra headroom) — everything is visible.
//     At 16:9/16:10 the camera shows LESS of the canvas vertically (e.g.
//     640x360 instead of 640x480 at 16:9), and elements anchored to the
//     very top/bottom of the "safe area" (movie subtitles among them) end
//     up outside the frame.
//
//     The cause is an asymmetry in the canvas scale itself: the horizontal
//     scale is computed as 640/W (correct, consistent with what the camera
//     sees), while the vertical one is computed as 480/H (NOT consistent
//     with the camera, which actually shows 640/aspect vertically, not
//     480). The minimal, geometrically correct fix is to make the vertical
//     scale EQUAL to the horizontal one (640/W instead of 480/H) for the
//     very same elements where this discrepancy applies.
//
//     WHY NOT A DIRECT SetVertexShaderConstantF HOOK. Intercepting
//     IDirect3DDevice9::SetVertexShaderConstantF directly does not work: in
//     this game, BeginScene/EndScene/DrawPrimitive/DrawIndexedPrimitive
//     reliably fire through a vtable hook, while SetRenderState/SetTexture/
//     SetVertexShader/SetVertexShaderConstantF NEVER fire, not even once,
//     even during confirmed real rendering. The cause is the well-known D3D9
//     IDirect3DStateBlock9 mechanism: the game records these calls into a
//     StateBlock once and later applies them via StateBlock::Apply(), which
//     acts directly at the driver level, bypassing the object's public
//     Set-methods entirely — no vtable hook on those specific slots can
//     ever observe such a call, in principle. So this fix intercepts NOT
//     the setter itself, but only the reliably-hookable DrawPrimitive/
//     DrawIndexedPrimitive, and right before each of them reads the current
//     canvas-register state directly via the real GetVertexShaderConstantF
//     (slot 95 — never replaced, so it always returns the true value
//     regardless of whether the state was set directly or via a
//     StateBlock), and corrects it directly via the real
//     SetVertexShaderConstantF (slot 94 — also never replaced) when needed.
//
//     WHY EVERY CREATED IDirect3DDevice9 IS HOOKED SEPARATELY. During game
//     startup, IDirect3D9::CreateDevice is called THREE times, producing
//     three DIFFERENT IDirect3DDevice9 objects at different memory
//     addresses, with their OWN (not shared) vtables — and only the LAST
//     one is actually used for per-frame rendering. A hook installed only
//     on the first created device NEVER sees a single real call. So every
//     newly created IDirect3DDevice9 is hooked separately (deduplicated by
//     address), up to MAX_HOOKED_DEVICES.
//
//     WHY THE "EXPECTED" scaleX IS COMPUTED FROM
//     GetSystemMetrics(SM_CXSCREEN), NOT FROM
//     D3DPRESENT_PARAMETERS::BackBufferWidth. The same call "signature"
//     (StartRegister=12, Vector4fCount=9) is used in the real game for MORE
//     than just the 2D-interface canvas — including unrelated cases such as
//     a menu-button "reveal" animation, where scaleX is always exactly 1.0.
//     A naive "just check asymmetry" filter (X != Y) incorrectly fires on
//     the menu-button animation, breaking its smoothness.
//     D3DPRESENT_PARAMETERS::BackBufferWidth is not a usable filter: in this
//     game that value always stays ~800 regardless of the actual screen
//     resolution and regardless of the "-resolution" launch parameter — the
//     game always creates an internal D3D9 device with an ~800-wide buffer
//     and stretches to the display through some other mechanism. Instead,
//     the genuine canvas scaleX value exactly matches 640 / (the real
//     Windows desktop width). So the expected scaleX is computed as
//     640.0f / GetSystemMetrics(SM_CXSCREEN) (the real, fixed desktop
//     width, not the game's variable and unrelated internal buffer size),
//     and the correction is applied only when the read scaleX is actually
//     close to that value (±1% tolerance) — this filter reliably rejects
//     every other, unrelated case.
//
//     The hook is installed NOT via an inline patch of the game's
//     executable, but by directly overwriting INDIVIDUAL entries (4 bytes
//     each) inside the VTABLE of the IDirect3DDevice9 object itself — the
//     method table physically stays exactly where the system d3d9.dll
//     created it (inside that module's own memory); the object's own
//     vtable pointer is NEVER changed. Only the CONTENT of individual slots
//     (DrawPrimitive, DrawIndexedPrimitive) in the already-existing table is
//     replaced, via a temporary change of that memory page's protection
//     (VirtualProtect). Swapping IDirect3DDevice9's ENTIRE vtable pointer to
//     a new table (even a byte-identical clone with zero redirection) makes
//     the game exit immediately and silently — so for IDirect3DDevice9,
//     only directly overwriting individual slots inside the original,
//     never-relocated table is used. For IDirect3D9::CreateDevice (the
//     small, one-time interface — needed only once, to obtain the pointer
//     to the freshly created device), swapping the whole pointer for a
//     copied table remains safe.
//
//     WHY THIS, NOT .lvl/Lua. The project already has a parallel, clean
//     data-only path (Bf2Widescreen/BattleIntroSubtitleProbePatcher.cs) —
//     overlaying a NEW text object on top of the movie via the same
//     Lua/IFScreen system the rest of the interface is patched through. But
//     the subtitle widget that ALREADY exists in the game is driven
//     entirely by native code baked into the .exe: the rectangle, the
//     alignment, the bottom anchor, the canvas scale — all computed either
//     from literal constants in the .exe or from the screen size; no value
//     comes from a data file. Fixing THIS SPECIFIC widget via data is not
//     possible in principle. The DLL proxy is the only way to fix THIS
//     SPECIFIC mechanism without touching a single byte of the .exe.
//
//     THE CURRENT CORRECTION CONDITION. CorrectCanvasIfNeeded corrects the
//     canvas when scaleY ≈ 480/(desktop height) — the only correction
//     condition.
//
//     CONFIRMED IN-GAME. Verified with a real game session at 1920x1080:
//     the movie caption is present and legible, and the in-combat
//     weapon-info panel (health/stamina bar, weapon icons) is not
//     distorted. See docs/BF2_MOVIE_SUBTITLE_FIX.md, "Confirmation"
//     section, for details.
// ============================================================================

#include <windows.h>
#include <d3d9.h>
#include <cstring>
#include <cmath>
#include <cstdio>

// UA: Текстовий маркер версії — впізнається байтовим пошуком у файлі DLL
//     (без завантаження й виконання), щоб ЦЕЙ d3d9.dll можна було відрізнити
//     від будь-якого стороннього (наприклад, вручну через шістнадцятковий
//     перегляд чи `strings`). Бумпається щоразу, коли логіка корекції
//     змінюється.
// EN: A text version marker — recognizable via a byte search in the DLL
//     file (without loading or executing it), so THIS d3d9.dll can be told
//     apart from any third-party one (for example, manually via a hex
//     viewer or `strings`). Bumped whenever the correction logic changes.
static const char kVersionMarker[] = "BF2WIDESCREENFIX_MARKER_V5_EMPUA";

// ----------------------------------------------------------------------------
// UA: Індекси методів у VTABLE — підтверджені дизасемблюванням і живим
//     тестуванням (стандартний layout
//     IDirect3D9/IDirect3DDevice9: CreateDevice = 16, DrawPrimitive = 81,
//     DrawIndexedPrimitive = 82). GetVertexShaderConstantF (95) і
//     SetVertexShaderConstantF (94) НЕ хукаються в цій версії — до них
//     звертаються напряму через сам COM-інтерфейс (self->...), саме тому,
//     що їхні слоти ніколи не підмінюються (див. врізку вище).
// EN: VTABLE method indices — confirmed by disassembly and live testing
//     (standard IDirect3D9/IDirect3DDevice9
//     layout: CreateDevice = 16, DrawPrimitive = 81, DrawIndexedPrimitive =
//     82). GetVertexShaderConstantF (95) and SetVertexShaderConstantF (94)
//     are NOT hooked in this version — they are called directly through the
//     COM interface itself (self->...), precisely because their slots are
//     never replaced (see the box above).
// ----------------------------------------------------------------------------
static const int VTBL_INDEX_CreateDevice        = 16;  // IDirect3D9
static const int VTBL_INDEX_DrawPrimitive       = 81;  // IDirect3DDevice9
static const int VTBL_INDEX_DrawIndexedPrimitive = 82; // IDirect3DDevice9

// UA: Розмір копії vtable для IDirect3D9 — з запасом (реальний інтерфейс
//     коротший), щоб безпечно скопіювати все, що може знадобитися, і ніколи не
//     читати/писати за межі виділеного масиву. Використовується лише
//     HookVTableSlot (повна підміна вказівника) для IDirect3D9 — для
//     IDirect3DDevice9 повна підміна не застосовується взагалі (див.
//     PatchVTableSlotInPlace нижче).
// EN: Vtable-copy size for IDirect3D9 — generously sized (the real
//     interface is shorter), the size covers everything the copy might need, and
//     no read or write goes past the allocated array. Used only by
//     HookVTableSlot (full pointer swap) for IDirect3D9 — for
//     IDirect3DDevice9 the full swap is not used at all (see
//     PatchVTableSlotInPlace below).
static const int VTBL_SIZE_IDirect3D9 = 32;

// ----------------------------------------------------------------------------
// UA: Запис у bf2_widescreen_fix.log — завжди увімкнений: цей лог є
//     єдиним способом діагностувати дефект на нетиповому обладнанні
//     користувача, тож окремий "тихий" варіант збірки без нього визнано
//     непотрібним.
//
//     Журнал належить ОДНОМУ запуску гри: перший запис після завантаження
//     DLL відкриває файл у режимі "w" (попередній вміст стирається), усі
//     наступні — на дозапис ("a"). Файл лишається на диску до наступного
//     запуску гри, тож його можна переглянути після виходу з гри.
//
//     Межа розміру — LOG_SIZE_LIMIT_BYTES (25 МБ) на один запуск.
//     Причина межі: стиснення за сигнатурою пише рядок на КОЖНУ зміну
//     елемента, а в бою кілька елементів HUD чергуються щокадру — реальний
//     прогін у кілька хвилин дав 5,5 МБ, найбільший зафіксований (повне
//     проходження кампанії) — 5,49 МБ; 25 МБ — ~4.5x запас. Щойно межу
//     досягнуто, пишеться один завершальний рядок, і до кінця запуску лог
//     мовчить. Оскільки кожен запуск починає файл заново, розмір файлу не
//     перевищує межу незалежно від кількості запусків. На саму корекцію
//     полотна це не впливає.
// EN: Writing to bf2_widescreen_fix.log — always on: this exact log is the
//     only way to diagnose a defect on a user's non-standard hardware, so a
//     separate "quiet" build without it was judged unnecessary.
//
//     The log belongs to ONE game launch: the first write after the DLL
//     loads opens the file in "w" mode (previous contents are erased), every
//     later write appends ("a"). The file stays on disk until the next game
//     launch, so it can be inspected after quitting the game.
//
//     Size cap — LOG_SIZE_LIMIT_BYTES (25 MB) per launch. Reason for the
//     cap: signature compression writes a line on EVERY element change, and
//     in combat several HUD elements alternate every frame — a real run of a
//     few minutes produced 5.5 MB, the largest recorded (a full campaign
//     playthrough) 5.49 MB; 25 MB is a ~4.5x margin. Once the cap is
//     reached, one final line is written and the log stays silent for the
//     rest of the launch. Since every launch starts the file afresh, the
//     file never exceeds the cap regardless of how many launches happen.
//     The canvas correction itself is not affected.
// ----------------------------------------------------------------------------
static const long LOG_SIZE_LIMIT_BYTES = 25L * 1024L * 1024L;
static long g_logBytes = 0;            // байтів записано за цей запуск / bytes written this launch
static bool g_logStarted = false;      // файл уже почато в цьому запуску / file already started this launch
static bool g_logLimitReached = false;

// UA: Префікс рядка журналу "ЧЧ:ММ:СС.ммм " з локального часу користувача
//     (GetLocalTime — на відміну від time(), дає мілісекунди). Пише РІВНО
//     13 символів у буфер розміром 16 — переповнення буфера неможливе.
// EN: The log line's "HH:MM:SS.mmm " prefix, from the user's local time
//     (GetLocalTime — unlike time(), has millisecond resolution). Writes
//     EXACTLY 13 characters into a 16-byte buffer — a buffer overflow here
//     is not possible.
static void FormatLogTimestamp(char* buf, size_t bufSize)
{
    SYSTEMTIME t;
    GetLocalTime(&t);
    snprintf(buf, bufSize, "%02u:%02u:%02u.%03u ", t.wHour, t.wMinute, t.wSecond, t.wMilliseconds);
}

static void WriteLog(const char* line)
{
    if (g_logLimitReached)
        return;

    // UA: перший запис за запуск стирає журнал попереднього запуску
    // EN: the first write of a launch erases the previous launch's log
    FILE* f = fopen("bf2_widescreen_fix.log", g_logStarted ? "a" : "w");
    if (!f) return;
    g_logStarted = true;

    if (g_logBytes >= LOG_SIZE_LIMIT_BYTES)
    {
        g_logLimitReached = true;
        fprintf(f, "[log] size limit reached (%ld bytes) - further lines suppressed for this session\n",
            LOG_SIZE_LIMIT_BYTES);
        fclose(f);
        return;
    }

    // UA: локальний час користувача першим у рядку / EN: user's local time first on the line
    char ts[16];
    FormatLogTimestamp(ts, sizeof(ts));

    fprintf(f, "%s%s\n", ts, line);
    g_logBytes += static_cast<long>(strlen(ts)) + static_cast<long>(strlen(line)) + 2; // + "\r\n" у текстовому режимі / in text mode
    fclose(f);
}

// ----------------------------------------------------------------------------
// UA: Універсальна функція підміни одного слоту у vtable COM-об'єкта.
//     Копіює оригінальну таблицю методів у нову (виділену через new[],
//     навмисно НІКОЛИ не звільняється — DLL живе весь час роботи гри),
//     підміняє один слот на переданий хук і перенаправляє перший вказівник
//     об'єкта (вказівник на vtable) на цю копію.
// EN: A generic function that swaps one slot in a COM object's vtable. It
//     copies the original method table into a new one (allocated via
//     new[], intentionally never freed — the DLL lives for the game's
//     whole run), replaces one slot with the given hook, and redirects the
//     object's first pointer (the vtable pointer) to that copy.
// ----------------------------------------------------------------------------
static void* HookVTableSlot(void* comObject, int slotIndex, int vtableSize, void* hookFn)
{
    void*** ppVtable = reinterpret_cast<void***>(comObject);
    void** originalVtable = *ppVtable;
    void* originalFn = originalVtable[slotIndex];

    void** newVtable = new void*[vtableSize];
    memcpy(newVtable, originalVtable, sizeof(void*) * vtableSize);
    newVtable[slotIndex] = hookFn;

    DWORD oldProtect = 0;
    VirtualProtect(ppVtable, sizeof(void*), PAGE_READWRITE, &oldProtect);
    *ppVtable = newVtable;
    VirtualProtect(ppVtable, sizeof(void*), oldProtect, &oldProtect);

    return originalFn;
}

// ----------------------------------------------------------------------------
// UA: Перезаписування ОДНОГО слота ПРЯМО всередині вже наявної vtable, без
//     виділення нової таблиці й без підміни вказівника об'єкта на vtable.
//     Таблиця й далі фізично лежить там, де її розмістив системний
//     d3d9.dll — змінюється лише вміст одного 4-байтового запису в ній.
//     ОБОВ'ЯЗКОВО для методів IDirect3DDevice9 — емпірично підтверджено:
//     підміна ВСЬОГО vtable-вказівника цього конкретного
//     інтерфейсу на нову таблицю (навіть без жодного перенаправлення слота)
//     призводить до негайного тихого завершення гри без вікна, винятку чи
//     запису в Event Viewer. Для IDirect3D9 (малий, одноразовий інтерфейс)
//     підміна всього вказівника (функція HookVTableSlot вище) лишається
//     підтверджено безпечною — тут вона навмисно НЕ замінена.
// EN: Overwrites ONE slot DIRECTLY inside the already-existing vtable, with
//     no new table allocation and no swap of the object's vtable pointer.
//     The table keeps physically living exactly where the system d3d9.dll
//     placed it — only the content of one 4-byte entry in it changes.
//     REQUIRED for IDirect3DDevice9 methods — empirically confirmed:
//     swapping this specific interface's ENTIRE vtable pointer to a new
//     table (even with zero slots redirected) makes the game silently exit
//     immediately, with no window, no exception, no Event Viewer entry. For
//     IDirect3D9 (the small, one-time interface), the whole-pointer swap
//     (the HookVTableSlot function above) remains confirmed safe — it is
//     deliberately NOT replaced here.
// ----------------------------------------------------------------------------
static void* PatchVTableSlotInPlace(void* comObject, int slotIndex, void* hookFn)
{
    void** vtable = *reinterpret_cast<void***>(comObject);
    void* originalFn = vtable[slotIndex];

    DWORD oldProtect = 0;
    VirtualProtect(&vtable[slotIndex], sizeof(void*), PAGE_READWRITE, &oldProtect);
    vtable[slotIndex] = hookFn;
    VirtualProtect(&vtable[slotIndex], sizeof(void*), oldProtect, &oldProtect);

    return originalFn;
}

// ----------------------------------------------------------------------------
// UA: Очікуваний scaleX справжнього полотна — 640 / (реальна ширина
//     робочого столу Windows), рахується один раз і кешується. Див. врізку
//     на початку файлу про те, чому саме ця величина, а не
//     D3DPRESENT_PARAMETERS::BackBufferWidth.
// EN: The expected scaleX of the genuine canvas — 640 / (the real Windows
//     desktop width), computed once and cached. See the box at the top of
//     the file for why this value, not
//     D3DPRESENT_PARAMETERS::BackBufferWidth.
// ----------------------------------------------------------------------------
static float g_expectedScaleX = 0.0f;

// UA: Очікуваний "баґований" scaleY — 480 / (реальна висота робочого
//     столу): саме так гра рахує вертикаль полотна, якщо вважає його 4:3.
//     Елементи поза боєм (crawl, текст екрана завантаження, субтитр відео)
//     мають scaleY_old рівно 0.444444 = 480/1080 (відношення до scaleX —
//     4/3), а бойовий HUD — інші, власні масштаби (0.296296 = 8/9 від
//     scaleX і 0.395062 = 32/27), яких поза боєм не було жодного разу.
//     Бойовий HUD цього бага не має.
// EN: The expected "buggy" scaleY — 480 / (the real desktop height): this
//     is how the game computes the canvas's vertical when it treats it as
//     4:3. Elements outside combat (the crawl, the loading-screen text, the
//     video subtitle) have scaleY_old of exactly 0.444444 = 480/1080
//     (ratio to scaleX: 4/3), while the combat HUD uses different, its own
//     scales (0.296296 = 8/9 of scaleX and 0.395062 = 32/27) that never
//     appear outside combat. The combat HUD does not have this bug.
static float g_expectedBuggyScaleY = 0.0f;
static bool g_desktopResLogged = false;

static void EnsureDesktopResolution()
{
    if (g_expectedScaleX > 0.0f && g_expectedBuggyScaleY > 0.0f)
        return;

    int desktopWidth = GetSystemMetrics(SM_CXSCREEN);
    int desktopHeight = GetSystemMetrics(SM_CYSCREEN);
    if (desktopWidth <= 0 || desktopHeight <= 0)
        return;

    g_expectedScaleX = 640.0f / static_cast<float>(desktopWidth);
    g_expectedBuggyScaleY = 480.0f / static_cast<float>(desktopHeight);

    if (!g_desktopResLogged)
    {
        g_desktopResLogged = true;
        char line[192];
        snprintf(line, sizeof(line),
            "[init] GetSystemMetrics(SM_CXSCREEN)=%d SM_CYSCREEN=%d -> "
            "expectedScaleX=%.6f expectedBuggyScaleY=%.6f",
            desktopWidth, desktopHeight, g_expectedScaleX, g_expectedBuggyScaleY);
        WriteLog(line);
    }
}

// ----------------------------------------------------------------------------
// UA: До MAX_HOOKED_DEVICES екземплярів IDirect3DDevice9, кожен зі своїми
//     справжніми (нехукнутими) DrawPrimitive/DrawIndexedPrimitive — див.
//     врізку на початку файлу про те, чому кожен пристрій хукається окремо.
// EN: Up to MAX_HOOKED_DEVICES IDirect3DDevice9 instances, each with its own
//     real (unhooked) DrawPrimitive/DrawIndexedPrimitive — see the box at
//     the top of the file for why every device is hooked separately.
// ----------------------------------------------------------------------------
static const int MAX_HOOKED_DEVICES = 4;

typedef HRESULT(STDMETHODCALLTYPE* DrawPrimitive_t)(
    IDirect3DDevice9*, D3DPRIMITIVETYPE, UINT, UINT);
typedef HRESULT(STDMETHODCALLTYPE* DrawIndexedPrimitive_t)(
    IDirect3DDevice9*, D3DPRIMITIVETYPE, INT, UINT, UINT, UINT, UINT);

static void* g_hookedDevices[MAX_HOOKED_DEVICES];
static DrawPrimitive_t g_realDrawPrimitive[MAX_HOOKED_DEVICES];
static DrawIndexedPrimitive_t g_realDrawIndexedPrimitive[MAX_HOOKED_DEVICES];
static int g_hookedDeviceCount = 0;

static int FindDeviceIndex(IDirect3DDevice9* self)
{
    void* p = static_cast<void*>(self);
    for (int i = 0; i < g_hookedDeviceCount; ++i)
        if (g_hookedDevices[i] == p)
            return i;
    return -1;
}

// ----------------------------------------------------------------------------
// UA: Викликається ПЕРЕД кожним DrawPrimitive/DrawIndexedPrimitive. Читає
//     поточні регістри полотна c12..c20 напряму через справжній,
//     нехукнутий GetVertexShaderConstantF (SetVertexShaderConstantF/
//     GetVertexShaderConstantF(StartRegister=12, Count=9) охоплює c12..c20;
//     c16=(scaleX,0,0,offX), c17=(0,-scaleY,0,offY)) і, лише
//     коли в них є доведена асиметрія ТА прочитаний scaleX близький до
//     очікуваного значення для справжньої ширини робочого столу (див.
//     врізку на початку файлу), виправляє їх напряму через справжній,
//     нехукнутий SetVertexShaderConstantF.
// EN: Called BEFORE every DrawPrimitive/DrawIndexedPrimitive. Reads the
//     current canvas registers c12..c20 directly via the real, unhooked
//     GetVertexShaderConstantF (SetVertexShaderConstantF/
//     GetVertexShaderConstantF(StartRegister=12, Count=9) spans c12..c20;
//     c16=(scaleX,0,0,offX), c17=(0,-scaleY,0,offY)) and, only
//     when they show the proven asymmetry AND the read scaleX is close to
//     the expected value for the real desktop width (see the box at the top
//     of the file), corrects them directly via the real, unhooked
//     SetVertexShaderConstantF.
// ----------------------------------------------------------------------------
// UA: Лічильник УСІХ спрацювань корекції за сесію (для нумерації `[fix#N]`),
//     без верхньої межі — реальний прогін від заставки до бою не може
//     оминути жодного з проміжних екранів (меню, завантаження), тож
//     штучний ліміт на кількість рядків лише обрізав би лог посеред першого
//     ж екрана, де корекція починає спрацьовувати, і робив би пізніші
//     екрани (зокрема бойовий HUD) невидимими.
// EN: Counter of ALL correction firings this session (used to number
//     `[fix#N]`), with no upper bound — a real playthrough from the intro to
//     combat cannot skip the screens in between (menus, loading), so an
//     artificial line cap would just truncate the log partway through the
//     FIRST screen where the correction starts firing, hiding later screens
//     (the combat HUD in particular) entirely. Оскільки друкується лише
//     перший рядок кожної сигнатури (див. нижче), номери `[fix#N]` у
//     файлі йтимуть із розривами — і це навмисно: розрив показує, скільки
//     всього спрацювань корекції відбулось до цієї зміни, тобто слугує
//     неявним індикатором позиції в часі без окремої мітки часу. / Since
//     only the first line of each signature is printed (see below), the
//     `[fix#N]` numbers in the file will have gaps — intentionally: the gap
//     size shows how many total firings happened before this change,
//     serving as an implicit time-position indicator without a separate
//     timestamp.
static int g_fixLogCount = 0;

// ----------------------------------------------------------------------------
// UA: Стан "останньої відомої сигнатури" спрацювання — для стиснення логу.
//     Один і той самий елемент інтерфейсу перемальовується щокадру (десятки
//     разів на секунду), тож без стиснення необмежений лог (див. вище) за
//     хвилини гри був би непридатний для читання. Тому повний рядок
//     `[fix#N]` пишеться лише при ПЕРШІЙ появі сигнатури й при КОЖНІЙ ЗМІНІ
//     (scaleX/scaleY_old/offY_old/розмір текстури0) — а не на кожному
//     кадрі. Перед новою сигнатурою пишеться розділювач із кількістю
//     повторів попередньої — саме він і дає змогу побачити межі між
//     екранами (заставка/відео/бій/меню), не рахуючи час вручну під час
//     гри.
// EN: "Last known signature" state — for log compression. The same UI
//     element is redrawn every frame (dozens of times per second), so
//     without compression the now-unbounded log (see above) would become
//     unreadable over minutes of play. So a full `[fix#N]` line is written
//     only on the FIRST appearance of a signature and on every CHANGE
//     (scaleX/scaleY_old/offY_old/texture0 size) — not every frame. A
//     separator line with the previous signature's repeat count is printed
//     right before a new one starts — that's what lets screen boundaries
//     (intro/video/combat/menu) show up in the log without manually timing
//     anything during play.
static bool g_hasLastFixSignature = false;
static float g_lastFixScaleX = 0.0f;
static float g_lastFixScaleYOld = 0.0f;
static float g_lastFixOffYOld = 0.0f;
static UINT g_lastFixTexW = 0;
static UINT g_lastFixTexH = 0;
static D3DFORMAT g_lastFixTexFmt = D3DFMT_UNKNOWN;
static bool g_lastFixApplied = false;
static int g_fixSignatureRepeatCount = 0;

// ----------------------------------------------------------------------------
// UA: Читає розміри й формат текстури, прив'язаної до слоту 0, — спільний
//     хелпер для гейта корекції в CorrectCanvasIfNeeded (потрібен ДО
//     прийняття рішення виправляти чи ні) і для LogFixDiagnostics (потрібен
//     для запису в лог). Раніше цей код існував лише всередині
//     LogFixDiagnostics і викликався ПІСЛЯ рішення виправляти — тепер
//     винесений окремо, щоб те саме читання можна було використати ДО
//     рішення.
// EN: Reads the dimensions and format of the texture bound to slot 0 — a
//     shared helper for the correction gate in CorrectCanvasIfNeeded (needed
//     BEFORE deciding whether to correct) and for LogFixDiagnostics (needed
//     for the log entry). This code used to live only inside
//     LogFixDiagnostics, called AFTER the correction decision — now it's
//     factored out so the same read can be used BEFORE that decision too.
// ----------------------------------------------------------------------------
static bool GetTexture0Info(IDirect3DDevice9* self, UINT& outW, UINT& outH, D3DFORMAT& outFmt)
{
    outW = 0;
    outH = 0;
    outFmt = D3DFMT_UNKNOWN;
    if (self == nullptr)
        return false;

    IDirect3DBaseTexture9* tex0 = nullptr;
    if (FAILED(self->GetTexture(0, &tex0)) || tex0 == nullptr)
        return false;

    bool ok = false;
    // UA: Релевантні лише звичайні 2D-текстури (D3DRTYPE_TEXTURE) —
    //     кубічні/об'ємні тут не застосовуються для 2D-інтерфейсу.
    // EN: Only plain 2D textures (D3DRTYPE_TEXTURE) are relevant here —
    //     cube/volume textures don't apply to the 2D interface.
    if (tex0->GetType() == D3DRTYPE_TEXTURE)
    {
        IDirect3DTexture9* tex2d = static_cast<IDirect3DTexture9*>(tex0);
        D3DSURFACE_DESC desc;
        if (SUCCEEDED(tex2d->GetLevelDesc(0, &desc)))
        {
            outW = desc.Width;
            outH = desc.Height;
            outFmt = desc.Format;
            ok = true;
        }
    }
    tex0->Release();
    return ok;
}

// ----------------------------------------------------------------------------
// UA: Розширена діагностика ОДНОГО КАНДИДАТА на корекцію — незалежно від
//     того, чи його врешті виправлено. Функція викликається для КОЖНОГО
//     кандидата, що пройшов базову перевірку асиметрії в
//     CorrectCanvasIfNeeded, включно з тими, яких відсіює додатковий гейт
//     (напр. за форматом текстури) — тож жоден кандидат не лишається без
//     рядка в лозі. Параметр `applied` лише позначає в лозі
//     (`corrected=yes/no`), чи цього разу відбулось реальне виправлення
//     регістрів.
// EN: Extended diagnostics for ONE correction CANDIDATE — regardless of
//     whether it ends up corrected. The function is called for EVERY
//     candidate that passes the base asymmetry check in
//     CorrectCanvasIfNeeded, including ones filtered out by an additional
//     gate (e.g. by texture format) — so no candidate is left without a log
//     line. The `applied` parameter just marks in the log
//     (`corrected=yes/no`) whether the registers were actually rewritten
//     this time.
// ----------------------------------------------------------------------------
static void LogFixDiagnostics(IDirect3DDevice9* self, float scaleX, float scaleYOld,
    float scaleYNew, float ratio, float offYOld, float offYNew,
    bool haveTex0, UINT texW, UINT texH, D3DFORMAT texFmt, bool applied)
{
    g_fixLogCount++;

    D3DVIEWPORT9 vp;
    bool haveViewport = self != nullptr && SUCCEEDED(self->GetViewport(&vp));

    // UA: Сигнатура для виявлення зміни екрана/елемента враховує формат
    //     текстури: різні формати з однаковим розміром текстури вважаються
    //     РІЗНИМИ сигнатурами, що дозволяє відрізнити суміжні елементи з
    //     однаковим розміром текстури, але різним форматом.
    // EN: Signature used to detect a screen/element change accounts for
    //     texture format: different formats sharing the same texture size
    //     count as DIFFERENT signatures, which tells apart adjacent
    //     elements that share a texture size but differ in format.
    const bool signatureChanged = !g_hasLastFixSignature ||
        scaleX != g_lastFixScaleX ||
        scaleYOld != g_lastFixScaleYOld ||
        offYOld != g_lastFixOffYOld ||
        texW != g_lastFixTexW ||
        texH != g_lastFixTexH ||
        texFmt != g_lastFixTexFmt ||
        applied != g_lastFixApplied;

    if (!signatureChanged)
    {
        // UA: Той самий елемент, що й на попередньому кадрі — не пишемо
        //     повторний рядок, лише рахуємо, скільки разів це триває.
        // EN: Same element as the previous frame — don't write a repeat
        //     line, just count how long this has been going on.
        g_fixSignatureRepeatCount++;
        return;
    }

    if (g_hasLastFixSignature)
    {
        char sep[288];
        snprintf(sep, sizeof(sep),
            "=== [fix] signature changed after %d repeat(s): was corrected=%s "
            "scaleX=%.6f scaleY_old=%.6f offY_old=%.6f texture0=%ux%u fmt=%d ===",
            g_fixSignatureRepeatCount, g_lastFixApplied ? "yes" : "no",
            g_lastFixScaleX, g_lastFixScaleYOld,
            g_lastFixOffYOld, g_lastFixTexW, g_lastFixTexH,
            static_cast<int>(g_lastFixTexFmt));
        WriteLog(sep);
    }

    g_lastFixScaleX = scaleX;
    g_lastFixScaleYOld = scaleYOld;
    g_lastFixOffYOld = offYOld;
    g_lastFixTexW = texW;
    g_lastFixTexH = texH;
    g_lastFixTexFmt = texFmt;
    g_lastFixApplied = applied;
    g_hasLastFixSignature = true;
    g_fixSignatureRepeatCount = 1;

    char line[416];
    snprintf(line, sizeof(line),
        "[fix#%d] canvas asymmetry candidate: corrected=%s scaleX=%.6f scaleY_old=%.6f -> "
        "scaleY_new=%.6f ratio=%.6f offY_old=%.6f offY_new=%.6f "
        "viewport=%s(x=%lu,y=%lu,w=%lu,h=%lu) texture0=%s(w=%u,h=%u,fmt=%d)",
        g_fixLogCount, applied ? "yes" : "no", scaleX, scaleYOld, scaleYNew, ratio, offYOld, offYNew,
        haveViewport ? "yes" : "no",
        haveViewport ? static_cast<unsigned long>(vp.X) : 0ul,
        haveViewport ? static_cast<unsigned long>(vp.Y) : 0ul,
        haveViewport ? static_cast<unsigned long>(vp.Width) : 0ul,
        haveViewport ? static_cast<unsigned long>(vp.Height) : 0ul,
        haveTex0 ? "yes" : "no", texW, texH, static_cast<int>(texFmt));
    WriteLog(line);
}

static void CorrectCanvasIfNeeded(IDirect3DDevice9* self)
{
    EnsureDesktopResolution();
    if (g_expectedScaleX <= 0.0f || self == nullptr)
        return;

    float buf[36];
    if (FAILED(self->GetVertexShaderConstantF(12, buf, 9)))
        return;

    const int C16 = (16 - 12) * 4; // c16.x
    const int C17 = (17 - 12) * 4; // c17.x / c17.y / c17.w

    float scaleX = buf[C16 + 0];
    float scaleY = -buf[C17 + 1]; // зберігається з мінусом / stored negated

    const float EPS = 0.0005f;

    // UA: Виправляємо ЛИШЕ доведену асиметрію (полотно увімкнене й
    //     масштаби по X і Y різні), і ЛИШЕ коли scaleX справді відповідає
    //     очікуваному 640/(ширина робочого столу) — без цього фільтра той
    //     самий регістровий "підпис" хибно спрацьовує на анімації кнопки
    //     меню (див. врізку на початку файлу).
    // EN: Correct ONLY the proven asymmetry (canvas enabled, X/Y scales
    //     differ), and ONLY when scaleX truly matches the expected
    //     640/(desktop width) — without this filter the same register
    //     "signature" incorrectly fires on the menu-button animation (see
    //     the box at the top of the file).
    if (scaleY <= EPS || fabsf(scaleX - scaleY) <= EPS)
        return;
    if (fabsf(scaleX - g_expectedScaleX) > g_expectedScaleX * 0.01f)
        return;

    // UA: Кандидат (той, що пройшов перевірки вище) ЗАВЖДИ логується через
    //     LogFixDiagnostics — незалежно від рішення виправляти. Рішення
    //     виправляти — нижче (applyFix).
    // EN: A candidate (one that passed the checks above) is ALWAYS logged via
    //     LogFixDiagnostics — regardless of the correction decision. The
    //     decision to correct is below (applyFix).
    UINT texW = 0, texH = 0;
    D3DFORMAT texFmt = D3DFMT_UNKNOWN;
    bool haveTex0 = GetTexture0Info(self, texW, texH, texFmt);

    float fixedData[36];
    memcpy(fixedData, buf, sizeof(fixedData));

    float ratio = scaleX / scaleY; // напр. 0.333333/0.444444 = 0.75 на 16:9
    fixedData[C17 + 1] = -scaleX;   // новий рівномірний Y-масштаб = X-масштабу
    fixedData[C17 + 3] *= ratio;    // якір (c17.w) масштабується тим самим коефіцієнтом,
                                     // щоб позиція лишилась узгодженою

    // UA: Виправляємо, коли scaleY ≈ 480/(висота робочого столу) — сам баг
    //     (полотно пораховане як 4:3). Це значення мають усі елементи поза
    //     боєм (crawl, текст екрана завантаження, субтитр відео), і НІ ОДИН
    //     елемент бойового HUD (там 0.296296 і 0.395062). Це відтворює
    //     стани, підтверджені як коректні реальною грою: до бою полотно
    //     виправляється, бойовий HUD — ні (див.
    //     docs/BF2_MOVIE_SUBTITLE_FIX.md).
    // EN: Correct when scaleY ≈ 480/(desktop height) — the bug itself (the
    //     canvas computed as 4:3). Every element outside combat (the crawl,
    //     the loading-screen text, the video subtitle) has exactly this
    //     value, and NOT ONE combat-HUD element does (those are 0.296296
    //     and 0.395062). This reproduces the states confirmed correct by a
    //     real game session: the canvas is corrected before combat, and the
    //     combat HUD is not (see docs/BF2_MOVIE_SUBTITLE_FIX.md).
    const bool isAspectBug = g_expectedBuggyScaleY > 0.0f &&
        fabsf(scaleY - g_expectedBuggyScaleY) <= g_expectedBuggyScaleY * 0.01f;
    const bool applyFix = isAspectBug;

    LogFixDiagnostics(self, scaleX, scaleY, scaleX, ratio, buf[C17 + 3], fixedData[C17 + 3],
        haveTex0, texW, texH, texFmt, applyFix);

    if (applyFix)
        self->SetVertexShaderConstantF(12, fixedData, 9);
}

// ----------------------------------------------------------------------------
// UA: Хуки IDirect3DDevice9::DrawPrimitive / DrawIndexedPrimitive — надійно
//     спрацьовують у цій грі (на відміну від SetVertexShaderConstantF, див.
//     врізку на початку файлу), тому саме перед ними виконується перевірка
//     й виправлення полотна.
// EN: The IDirect3DDevice9::DrawPrimitive / DrawIndexedPrimitive hooks —
//     they reliably fire in this game (unlike SetVertexShaderConstantF, see
//     the box at the top of the file), which is why the canvas check and
//     correction happens right before them.
// ----------------------------------------------------------------------------
static HRESULT STDMETHODCALLTYPE HookedDrawPrimitive(
    IDirect3DDevice9* self, D3DPRIMITIVETYPE PrimitiveType, UINT StartVertex, UINT PrimitiveCount)
{
    CorrectCanvasIfNeeded(self);

    int idx = FindDeviceIndex(self);
    if (idx >= 0 && g_realDrawPrimitive[idx])
        return g_realDrawPrimitive[idx](self, PrimitiveType, StartVertex, PrimitiveCount);

    return D3DERR_INVALIDCALL; // UA: не повинно статись за нормальної роботи / EN: should not happen in normal operation
}

static HRESULT STDMETHODCALLTYPE HookedDrawIndexedPrimitive(
    IDirect3DDevice9* self, D3DPRIMITIVETYPE PrimitiveType, INT BaseVertexIndex,
    UINT MinVertexIndex, UINT NumVertices, UINT startIndex, UINT primCount)
{
    CorrectCanvasIfNeeded(self);

    int idx = FindDeviceIndex(self);
    if (idx >= 0 && g_realDrawIndexedPrimitive[idx])
        return g_realDrawIndexedPrimitive[idx](
            self, PrimitiveType, BaseVertexIndex, MinVertexIndex, NumVertices, startIndex, primCount);

    return D3DERR_INVALIDCALL; // UA: не повинно статись за нормальної роботи / EN: should not happen in normal operation
}

// ----------------------------------------------------------------------------
// UA: Хук IDirect3D9::CreateDevice — хукає КОЖЕН щойно створений
//     IDirect3DDevice9 окремо (з дедублікацією за адресою), бо в цій грі
//     створюється кілька екземплярів із власними vtable (див. врізку на
//     початку файлу).
// EN: The IDirect3D9::CreateDevice hook — hooks EVERY newly created
//     IDirect3DDevice9 separately (deduplicated by address), because this
//     game creates several instances with their own vtables (see the box
//     at the top of the file).
// ----------------------------------------------------------------------------
typedef HRESULT(STDMETHODCALLTYPE* CreateDevice_t)(
    IDirect3D9*, UINT, D3DDEVTYPE, HWND, DWORD,
    D3DPRESENT_PARAMETERS*, IDirect3DDevice9**);

static CreateDevice_t g_realCreateDevice = nullptr;

static HRESULT STDMETHODCALLTYPE HookedCreateDevice(
    IDirect3D9* self, UINT Adapter, D3DDEVTYPE DeviceType, HWND hFocusWindow,
    DWORD BehaviorFlags, D3DPRESENT_PARAMETERS* pPresentationParameters,
    IDirect3DDevice9** ppReturnedDeviceInterface)
{
    HRESULT hr = g_realCreateDevice(self, Adapter, DeviceType, hFocusWindow,
        BehaviorFlags, pPresentationParameters, ppReturnedDeviceInterface);

    if (SUCCEEDED(hr) && ppReturnedDeviceInterface && *ppReturnedDeviceInterface)
    {
        void* dev = *ppReturnedDeviceInterface;
        bool already = false;
        for (int i = 0; i < g_hookedDeviceCount; ++i)
        {
            if (g_hookedDevices[i] == dev) { already = true; break; }
        }

        if (!already && g_hookedDeviceCount < MAX_HOOKED_DEVICES)
        {
            int idx = g_hookedDeviceCount;
            g_hookedDevices[idx] = dev;
            g_realDrawPrimitive[idx] = reinterpret_cast<DrawPrimitive_t>(
                PatchVTableSlotInPlace(dev, VTBL_INDEX_DrawPrimitive,
                    reinterpret_cast<void*>(HookedDrawPrimitive)));
            g_realDrawIndexedPrimitive[idx] = reinterpret_cast<DrawIndexedPrimitive_t>(
                PatchVTableSlotInPlace(dev, VTBL_INDEX_DrawIndexedPrimitive,
                    reinterpret_cast<void*>(HookedDrawIndexedPrimitive)));
            g_hookedDeviceCount++;

            char line[160];
            snprintf(line, sizeof(line),
                "[init] IDirect3DDevice9 #%d created and hooked OK "
                "(in-place patch, DrawPrimitive+DrawIndexedPrimitive, device=%p).",
                idx, dev);
            WriteLog(line);
        }

        EnsureDesktopResolution();
    }
    else if (FAILED(hr))
    {
        WriteLog("[init] real CreateDevice FAILED (proxy forwarded the call unchanged).");
    }

    return hr;
}

// ----------------------------------------------------------------------------
// UA: Єдиний імпорт, що його BattlefrontII.exe бере з d3d9.dll (перевірено
//     PE import table гри — інших функцій із d3d9.dll вона не імпортує).
// EN: The only import BattlefrontII.exe takes from d3d9.dll (verified
//     against the game's PE import table — it imports no other function
//     from d3d9.dll).
// ----------------------------------------------------------------------------
typedef IDirect3D9* (WINAPI* Direct3DCreate9_t)(UINT);
static Direct3DCreate9_t g_realDirect3DCreate9 = nullptr;
static HMODULE g_realD3D9Module = nullptr;

static bool EnsureRealD3D9Loaded()
{
    if (g_realDirect3DCreate9)
        return true;

    // UA: Явно завантажуємо СПРАВЖНЮ системну d3d9.dll за повним шляхом, щоб
    //     не було рекурсивного завантаження цієї ж проксі-бібліотеки
    //     (стандартний порядок пошуку DLL спершу перевірив би теку гри).
    // EN: Explicitly load the REAL system d3d9.dll by its full path, so
    //     loading it never recursively loads this same proxy (the standard
    //     DLL search order would otherwise check the game folder first).
    char systemDir[MAX_PATH];
    UINT len = GetSystemDirectoryA(systemDir, MAX_PATH);
    if (len == 0 || len >= MAX_PATH)
    {
        WriteLog("[init] GetSystemDirectoryA failed.");
        return false;
    }

    char realD3D9Path[MAX_PATH];
    lstrcpynA(realD3D9Path, systemDir, MAX_PATH);
    lstrcatA(realD3D9Path, "\\d3d9.dll");

    g_realD3D9Module = LoadLibraryA(realD3D9Path);
    if (!g_realD3D9Module)
    {
        WriteLog("[init] LoadLibraryA(real d3d9.dll) failed.");
        return false;
    }

    g_realDirect3DCreate9 = reinterpret_cast<Direct3DCreate9_t>(
        GetProcAddress(g_realD3D9Module, "Direct3DCreate9"));

    if (!g_realDirect3DCreate9)
    {
        WriteLog("[init] GetProcAddress(Direct3DCreate9) on real d3d9.dll failed.");
        return false;
    }

    char line[256];
    snprintf(line, sizeof(line), "[init] %s loaded. Real d3d9.dll: %s", kVersionMarker, realD3D9Path);
    WriteLog(line);
    return true;
}

extern "C" __declspec(dllexport) IDirect3D9* WINAPI Direct3DCreate9(UINT SDKVersion)
{
    if (!EnsureRealD3D9Loaded())
        return nullptr;

    IDirect3D9* realD3D9 = g_realDirect3DCreate9(SDKVersion);
    if (!realD3D9)
    {
        WriteLog("[init] real Direct3DCreate9 returned NULL.");
        return realD3D9;
    }

    if (!g_realCreateDevice)
    {
        g_realCreateDevice = reinterpret_cast<CreateDevice_t>(
            HookVTableSlot(realD3D9, VTBL_INDEX_CreateDevice,
                VTBL_SIZE_IDirect3D9, reinterpret_cast<void*>(HookedCreateDevice)));
    }

    return realD3D9;
}

BOOL APIENTRY DllMain(HMODULE, DWORD, LPVOID)
{
    return TRUE;
}
