// =============================================================================
// BF1LocalizationTool.Core — Bf2Widescreen/BattleIntroSubtitleProbePatcher.cs
// Автор / Author: EMP_UA (https://github.com/EMP-UA)
// Ліцензія / License: MIT
// Тип / Type: ДІАГНОСТИКА (генерує ігровий файл лише для точкових тестів, НЕ production) / DIAGNOSTIC (generates a game file for point-tests only, NOT production)
// =============================================================================
// UA: ДІАГНОСТИЧНИЙ (НЕ фінальний) патч для перевірки гіпотези: чи можна
//     домалювати текст ПОВЕРХ вступного відеоролика кампанії (crawl),
//     використовуючи ЛИШЕ ту саму Lua/IFScreen-систему, якою вже патчиться
//     решта інтерфейсу в цьому проєкті — БЕЗ жодного патчу BattlefrontII.exe
//     (жорстка межа проєкту: файл .exe гри не патчиться).
//
//     КОНТЕКСТ: оригінальні субтитри роликів зникають на будь-якому не-4:3
//     розширенні (виміряно пікселями на 10 реальних знімках — рядок
//     субтитрів присутній ЛИШЕ при 800x600/1024x768/1280x1024, повністю
//     відсутній, а не обрізаний, на решті 7). Реверс-інжиниринг реальних
//     .lvl (НЕ .exe) показав:
//       - екран ролика "ifs_campaign_battle_intro" (shell.lvl) — ЗВИЧАЙНИЙ
//         Lua NewIFShellScreen/AddIFScreen, нічого особливого;
//       - AddIFScreen -> AddIFObjContainer (common.lvl, скрипт
//         "interface_util") РЕКУРСИВНО обходить УСІ табличні поля таблиці
//         екрана і для кожного вкладеного `{type="text", ...}` викликає
//         AddIFText. Тобто досить ДОДАТИ ще одне поле до таблиці екрана ДО
//         виклику AddIFScreen — рушій сам створить і приєднає текстовий
//         об'єкт; викликати AddIFText самим НЕ треба;
//       - жодного власного "хука" в Enter/Update не потрібно: текстовий
//         об'єкт створюється ОДИН РАЗ при завантаженні shell.lvl (як фонове
//         зображення чи кнопки на інших екранах) і показується щоразу, коли
//         екран активний — так само, як сам ролик.
//
//     ЩО РОБИТЬ ЦЕЙ ПАТЧ: вставляє НОВІ інструкції NEWTABLE+SETTABLE у
//     КОРІНЬ (root-прототип, БЕЗ жодного вкладеного замикання) чанка
//     "ifs_campaign_battle_intro", одразу ПЕРЕД викликом
//     NewIFShellScreen(...) — тобто до того, як побудована таблиця-аргумент
//     іде в CALL. Дописується РІВНО одне нове поле таблиці екрана:
//     ProbeTextSpec.ScreenFieldKey = { type="text", string=Text, ... }.
//
//     ЧОМУ БЕЗ ПЕРЕРАХУНКУ ПЕРЕХОДІВ (на відміну від
//     ShellEntryPointPatcher.ApplySplicedInstallerPatch): корінь САМЕ ЦЬОГО
//     скрипту НЕ МІСТИТЬ ЖОДНОЇ інструкції JMP/FORLOOP/TFORPREP/TFORLOOP
//     (підтверджено повним дизасемблюванням цього чанка) — це проста
//     послідовна побудова таблиці
//     без розгалужень. Тому вставка інструкцій усередину НЕ ламає жодного
//     відносного переходу. Метод це ЯВНО ПЕРЕВІРЯЄ (кидає виняток, якщо
//     припущення колись виявиться хибним для іншого екрана), а не мовчки
//     на нього покладається.
//
//     ТЕКСТ ЛИШЕ ASCII: кодування кириличних рядків у екранних Lua-текстах
//     через Locl/UTF-16 (core.lvl, LoclChunkParser) ще НЕ досліджено — це
//     ОКРЕМИЙ наступний крок ПІСЛЯ підтвердження, що текст взагалі
//     малюється поверх ролика. AddIFText/interface_util МАЄ окремий шлях
//     "ustring"/ScriptCB_IFText_SetUString саме для unicode-рядків — цим
//     шляхом варто скористатись для РЕАЛЬНИХ субтитрів пізніше. Латиниця
//     тут — свідомий вибір для мінімального, максимально дешевого тесту
//     гіпотези "чи взагалі щось малюється поверх ролика".
// EN: A DIAGNOSTIC (NOT final) patch to test a hypothesis: can text be
//     drawn ON TOP of the campaign intro movie (crawl) using ONLY the same
//     Lua/IFScreen system this project already patches everywhere else —
//     with NO patch to BattlefrontII.exe whatsoever (a hard project
//     boundary: the game's .exe is never patched).
//
//     CONTEXT: the original movie subtitles disappear at any non-4:3 resolution
//     (pixel-measured on 10 real screenshots — the caption line is present
//     ONLY at 800x600/1024x768/1280x1024, fully absent, not clipped, at the
//     other 7). Reverse engineering the real .lvl files (NOT the .exe)
//     showed:
//       - the movie screen "ifs_campaign_battle_intro" (shell.lvl) is an
//         ORDINARY Lua NewIFShellScreen/AddIFScreen, nothing special;
//       - AddIFScreen -> AddIFObjContainer (common.lvl, the
//         "interface_util" script) RECURSIVELY walks EVERY table field of
//         the screen table and, for each nested `{type="text", ...}`,
//         calls AddIFText. So it's enough to ADD one more field to the
//         screen table BEFORE the AddIFScreen call — the engine itself
//         creates and attaches the text object; the patch never calls
//         AddIFText directly;
//       - no hook in Enter/Update is needed at all: the text object is
//         created ONCE when shell.lvl loads (same as a background image or
//         buttons on other screens) and is shown whenever the screen is
//         active — exactly like the movie itself.
//
//     WHAT THIS PATCH DOES: inserts NEW NEWTABLE+SETTABLE instructions
//     into the ROOT prototype (no nested closure at all) of the
//     "ifs_campaign_battle_intro" chunk, right BEFORE the
//     NewIFShellScreen(...) call — i.e. before the built argument table
//     goes into CALL. Exactly one new field is added to the screen table:
//     ProbeTextSpec.ScreenFieldKey = { type="text", string=Text, ... }.
//
//     WHY NO JUMP REMAPPING (unlike
//     ShellEntryPointPatcher.ApplySplicedInstallerPatch): THIS SPECIFIC
//     script's root contains NO JMP/FORLOOP/TFORPREP/TFORLOOP instructions
//     at all (confirmed by fully disassembling this chunk) — it's a plain
//     sequential table build
//     with no branching. So inserting instructions in the middle breaks no
//     relative jump. The method EXPLICITLY CHECKS this (throws if the
//     assumption ever turns out false for a different screen) rather than
//     silently relying on it.
//
//     TEXT IS ASCII ONLY: how Cyrillic strings are encoded in on-screen Lua
//     text via Locl/UTF-16 (core.lvl, LoclChunkParser) has not been
//     investigated yet — that's a SEPARATE next step AFTER confirming text
//     renders over the movie at all. AddIFText/interface_util HAS a
//     separate "ustring"/ScriptCB_IFText_SetUString path specifically for
//     unicode strings — that path is the one to use for REAL subtitles
//     later. Latin letters here are a deliberate choice for the smallest,
//     cheapest possible test of "does anything render over the movie at
//     all".
// =============================================================================

using System.Text;
using BF1LocalizationTool.Core.Chunks;
using BF1LocalizationTool.Core.Scripts;

namespace BF1LocalizationTool.Core.Bf2Widescreen;

public static class BattleIntroSubtitleProbePatcher
{
    // UA: Ім'я екрана-цілі — вступний ролик кампанії (shell.lvl).
    // EN: Target screen name — the campaign intro movie (shell.lvl).
    public const string TargetScreenName = "ifs_campaign_battle_intro";

    // UA: Опис одного текстового об'єкта-проби. Поля дзеркалять параметри
    //     Lua-обгорток NewIFText/AddIFText (common.lvl, "interface_util") —
    //     див. коментар класу вище.
    // EN: Describes a single probe text object. Fields mirror the
    //     NewIFText/AddIFText Lua wrapper parameters (common.lvl,
    //     "interface_util") — see the class comment above.
    public sealed record ProbeTextSpec
    {
        // UA: Ключ, під яким об'єкт приєднається до таблиці екрана
        //     (AddIFObjContainer обходить УСІ поля — ім'я довільне, аби не
        //     збігалося з наявним полем екрана).
        // EN: The key the object attaches under on the screen table
        //     (AddIFObjContainer walks ALL fields — any name works, as
        //     long as it doesn't collide with an existing screen field).
        public string ScreenFieldKey { get; init; } = "bf1lt_probeText";

        public required string Text { get; init; }
        public string Font { get; init; } = "gamefont_large";
        public float TextWidth { get; init; } = 400f;
        public float TextHeight { get; init; } = 60f;
        public string HAlign { get; init; } = "hcenter";
        public string VAlign { get; init; } = "vcenter";

        // UA: OffsetX/Y — локальний зсув у пікселях (як робить сам
        //     NewIFText для hcenter/vcenter: -textw/2, -texth/2);
        //     ScreenRelativeX/Y — точка прив'язки як частка ширини/висоти
        //     екрана (0..1) — ТОЙ САМИЙ механізм, яким у цьому проєкті вже
        //     прив'язані інші елементи widescreen-фікса.
        // EN: OffsetX/Y — local pixel offset (as NewIFText itself does for
        //     hcenter/vcenter: -textw/2, -texth/2); ScreenRelativeX/Y — the
        //     anchor point as a fraction of screen width/height (0..1) —
        //     the SAME mechanism this project's other widescreen-fix
        //     elements already anchor through.
        public float OffsetX { get; init; } = -200f;
        public float OffsetY { get; init; } = -30f;
        public float ScreenRelativeX { get; init; } = 0.5f;
        public float ScreenRelativeY { get; init; } = 0.5f;
    }

    // -------------------------------------------------------------------------
    // UA: Головна точка входу. Мутує root.Children напряму (той самий
    //     патерн, що й ShellEntryPointPatcher.ApplySplicedInstallerPatch) —
    //     після виклику root готовий для UcfbWriter.WriteFile.
    // EN: Main entry point. Mutates root.Children directly (the same
    //     pattern as ShellEntryPointPatcher.ApplySplicedInstallerPatch) —
    //     after the call, root is ready for UcfbWriter.WriteFile.
    // -------------------------------------------------------------------------
    public static void ApplyProbe(UcfbChunk root, ProbeTextSpec spec, string screenName = TargetScreenName)
    {
        var screenChunk = root.Children.FirstOrDefault(c =>
            c.FourCC == "scr_" &&
            c.Children.FirstOrDefault(n => n.FourCC == "NAME") is { } nameChunk &&
            Encoding.ASCII.GetString(nameChunk.RawData).TrimEnd('\0') == screenName);

        if (screenChunk is null)
            throw new InvalidOperationException(
                $"UA: Екран '{screenName}' не знайдено серед top-level 'scr_'-чанків / " +
                $"EN: Screen '{screenName}' not found among top-level 'scr_' chunks");

        var bodyIndex = screenChunk.Children.FindIndex(c => c.FourCC == "BODY");
        if (bodyIndex < 0)
            throw new InvalidOperationException(
                $"UA: '{screenName}' не має дочірнього чанка BODY / EN: '{screenName}' has no BODY child chunk");

        var bodyChunk = screenChunk.Children[bodyIndex];
        var newBodyBytes = BuildPatchedBody(bodyChunk.RawData, spec);

        screenChunk.Children[bodyIndex] = new UcfbChunk
        {
            Id = bodyChunk.Id,
            FourCC = bodyChunk.FourCC,
            DataSize = (uint)newBodyBytes.Length,
            RawData = newBodyBytes,
        };
    }

    // -------------------------------------------------------------------------
    // UA: Будує пропатчені байти BODY окремо від UcfbChunk-обгортки —
    //     зручно для тестів/діагностики без повного дерева чанків файлу.
    // EN: Builds the patched BODY bytes separately from the UcfbChunk
    //     wrapper — convenient for tests/diagnostics without a full file
    //     chunk tree.
    // -------------------------------------------------------------------------
    public static byte[] BuildPatchedBody(byte[] originalBodyBytes, ProbeTextSpec spec)
    {
        var parseResult = Lua50BytecodeReader.Parse(originalBodyBytes);
        var original = parseResult.Root;

        // UA: Жорсткий запобіжник: цей метод НЕ перераховує відносні
        //     переходи. Якщо цільовий скрипт колись виявиться складнішим
        //     (містить JMP/FORLOOP/TFORPREP/TFORLOOP) — краще впасти з
        //     чіткою помилкою, ніж мовчки згенерувати биту логіку.
        // EN: A hard guard: this method does NOT remap relative jumps. If
        //     the target script ever turns out more complex (contains
        //     JMP/FORLOOP/TFORPREP/TFORLOOP) — better to fail loudly than
        //     to silently generate broken logic.
        var hasJumps = original.Instructions.Any(i =>
            i.Opcode is LuaOpcode.Jmp or LuaOpcode.ForLoop or LuaOpcode.TForPrep or LuaOpcode.TForLoop);
        if (hasJumps)
            throw new NotSupportedException(
                "UA: Корінь цього скрипту містить переходи (JMP/FORLOOP/...) — цей простий " +
                "неремапуючий splice для нього НЕ безпечний; потрібна логіка перерахунку " +
                "переходів (див. ShellEntryPointPatcher.ApplySplicedInstallerPatch). / " +
                "EN: This script's root contains jumps (JMP/FORLOOP/...) — this simple, " +
                "non-remapping splice is NOT safe for it; jump-remapping logic is needed " +
                "(see ShellEntryPointPatcher.ApplySplicedInstallerPatch).");

        // UA: Знайти виклик NewIFShellScreen(...) — саме перед ним
        //     вставляємо нове поле в таблицю-аргумент.
        // EN: Find the NewIFShellScreen(...) call — the new field is
        //     inserted into the argument table right before it.
        var newIfShellScreenConstIndex = FindStringConstantIndex(original.Constants, "NewIFShellScreen");
        if (newIfShellScreenConstIndex < 0)
            throw new InvalidOperationException(
                "UA: Константа 'NewIFShellScreen' не знайдена в пулі констант кореня — це не " +
                "звичайний NewIFShellScreen-екран, або структура інша, ніж очікувалось. / " +
                "EN: 'NewIFShellScreen' constant not found in the root constant pool — this " +
                "isn't an ordinary NewIFShellScreen screen, or the structure differs from what " +
                "was expected.");

        var getGlobalPc = -1;
        var calleeRegister = -1;
        for (var i = 0; i < original.Instructions.Count; i++)
        {
            var ins = original.Instructions[i];
            if (ins.Opcode == LuaOpcode.GetGlobal && ins.Bx == newIfShellScreenConstIndex)
            {
                getGlobalPc = i;
                calleeRegister = ins.A;
                break;
            }
        }
        if (getGlobalPc < 0)
            throw new InvalidOperationException(
                "UA: Не знайдено GETGLOBAL 'NewIFShellScreen' серед інструкцій кореня / " +
                "EN: No GETGLOBAL 'NewIFShellScreen' found among the root's instructions");

        var callPc = -1;
        for (var i = getGlobalPc + 1; i < original.Instructions.Count; i++)
        {
            var ins = original.Instructions[i];
            if (ins.Opcode == LuaOpcode.Call && ins.A == calleeRegister)
            {
                callPc = i;
                break;
            }
        }
        if (callPc < 0)
            throw new InvalidOperationException(
                "UA: Не знайдено CALL, що відповідає GETGLOBAL 'NewIFShellScreen' / " +
                "EN: No CALL matching the GETGLOBAL 'NewIFShellScreen' found");

        // UA: Таблиця-аргумент екрана — регістр ОДРАЗУ після callee (CALL
        //     args починаються з A+1, підтверджено на всіх реальних
        //     ifs_*-екранах, що будуються через NewIFShellScreen(table)).
        // EN: The screen's argument table — the register RIGHT AFTER the
        //     callee (CALL args start at A+1, confirmed across every real
        //     ifs_* screen built via NewIFShellScreen(table)).
        var screenTableRegister = calleeRegister + 1;

        // UA: Проба будується в ПЕРШОМУ НІКОЛИ не використаному регістрі —
        //     той самий прийом, що й installerRegister у
        //     ShellEntryPointPatcher.ApplySplicedInstallerPatch.
        // EN: The probe is built in the FIRST NEVER-used register — the
        //     same trick as installerRegister in
        //     ShellEntryPointPatcher.ApplySplicedInstallerPatch.
        var probeRegister = original.MaxStackSize;

        var (probeInstructions, newConstants) = BuildProbeInstructions(
            original.Constants, screenTableRegister, probeRegister, spec);

        var newInstructions = new List<LuaInstruction>(original.Instructions.Count + probeInstructions.Count);
        newInstructions.AddRange(original.Instructions.Take(callPc));
        newInstructions.AddRange(probeInstructions);
        newInstructions.AddRange(original.Instructions.Skip(callPc));
        for (var i = 0; i < newInstructions.Count; i++)
            newInstructions[i] = newInstructions[i] with { Pc = i };

        var patchedProto = original with
        {
            Instructions = newInstructions,
            Constants = newConstants,
            MaxStackSize = (byte)Math.Max(original.MaxStackSize, probeRegister + 1),
            SizeCode = newInstructions.Count,
        };

        return Lua50BytecodeWriter.Write(patchedProto);
    }

    // -------------------------------------------------------------------------
    // UA: Будує інструкції NEWTABLE + по-SETTABLE на кожне поле + фінальний
    //     SETTABLE, що приєднує готовий об'єкт-пробу до таблиці екрана.
    //     Повертає також РОЗШИРЕНИЙ пул констант (оригінальні + нові, БЕЗ
    //     дедуп — як і в Lua50FunctionBuilder; тут це не критично, лише
    //     жменя нових записів на функцію із заледве 18 оригінальними).
    // EN: Builds the NEWTABLE + per-field SETTABLE + final SETTABLE
    //     instructions that attach the finished probe object to the screen
    //     table. Also returns the EXTENDED constant pool (original + new,
    //     NO dedup — like Lua50FunctionBuilder; not critical here, just a
    //     handful of new entries on a function with a mere 18 original
    //     ones).
    // -------------------------------------------------------------------------
    private static (List<LuaInstruction> Instructions, List<LuaConstant> Constants) BuildProbeInstructions(
        IReadOnlyList<LuaConstant> originalConstants, int screenTableRegister, int probeRegister, ProbeTextSpec spec)
    {
        var constants = new List<LuaConstant>(originalConstants);

        int AddString(string value)
        {
            constants.Add(new LuaConstant { Kind = LuaConstantKind.String, ValueOffset = -1, StringValue = value });
            return constants.Count - 1;
        }

        int AddNumber(float value)
        {
            constants.Add(new LuaConstant { Kind = LuaConstantKind.Number, ValueOffset = -1, NumberValue = value });
            return constants.Count - 1;
        }

        var kType = AddString("type");
        var kTypeText = AddString("text");
        var kScreenFieldKey = AddString(spec.ScreenFieldKey);
        var kString = AddString("string");
        var kTextValue = AddString(spec.Text);
        var kFont = AddString("font");
        var kFontValue = AddString(spec.Font);
        var kTextw = AddString("textw");
        var kTextwValue = AddNumber(spec.TextWidth);
        var kTexth = AddString("texth");
        var kTexthValue = AddNumber(spec.TextHeight);
        var kHalign = AddString("halign");
        var kHalignValue = AddString(spec.HAlign);
        var kValign = AddString("valign");
        var kValignValue = AddString(spec.VAlign);
        var kX = AddString("x");
        var kXValue = AddNumber(spec.OffsetX);
        var kY = AddString("y");
        var kYValue = AddNumber(spec.OffsetY);
        var kScreenRelX = AddString("ScreenRelativeX");
        var kScreenRelXValue = AddNumber(spec.ScreenRelativeX);
        var kScreenRelY = AddString("ScreenRelativeY");
        var kScreenRelYValue = AddNumber(spec.ScreenRelativeY);

        var instr = new List<LuaInstruction>();
        var pc = 0;

        // UA: WordOffset=-1 — сентинел "немає джерела в реальному файлі"
        //     (ці інструкції СИНТЕЗУЮТЬСЯ заново, а не читаються байткодом;
        //     та сама конвенція, що вже використана в Lua50FunctionBuilder).
        // EN: WordOffset=-1 — the "no source in a real file" sentinel
        //     (these instructions are SYNTHESIZED from scratch, not parsed
        //     from bytecode; the same convention already used in
        //     Lua50FunctionBuilder).
        void SetField(int keyConst, int valueConst) =>
            instr.Add(new LuaInstruction
            {
                Pc = pc++,
                Opcode = LuaOpcode.SetTable,
                A = probeRegister,
                B = Lua50FunctionBuilder.Rk(keyConst),
                C = Lua50FunctionBuilder.Rk(valueConst),
                WordOffset = -1,
            });

        // UA: NEWTABLE R(probeRegister) — невеликий хеш-хінт (4), як і в
        //     реальних vanilla-таблицях подібного розміру (недостача
        //     хінту нешкідлива — Lua-таблиця перегешовується сама).
        // EN: NEWTABLE R(probeRegister) — a small hash-size hint (4), as
        //     in real vanilla tables of similar size (an undersized hint
        //     is harmless — a Lua table rehashes itself on demand).
        instr.Add(new LuaInstruction { Pc = pc++, Opcode = LuaOpcode.NewTable, A = probeRegister, B = 0, C = 4, WordOffset = -1 });

        SetField(kType, kTypeText);
        SetField(kString, kTextValue);
        SetField(kFont, kFontValue);
        SetField(kTextw, kTextwValue);
        SetField(kTexth, kTexthValue);
        SetField(kHalign, kHalignValue);
        SetField(kValign, kValignValue);
        SetField(kX, kXValue);
        SetField(kY, kYValue);
        SetField(kScreenRelX, kScreenRelXValue);
        SetField(kScreenRelY, kScreenRelYValue);

        // R(screenTableRegister)[spec.ScreenFieldKey] := R(probeRegister)
        instr.Add(new LuaInstruction
        {
            Pc = pc++,
            Opcode = LuaOpcode.SetTable,
            A = screenTableRegister,
            B = Lua50FunctionBuilder.Rk(kScreenFieldKey),
            C = probeRegister,
            WordOffset = -1,
        });

        return (instr, constants);
    }

    private static int FindStringConstantIndex(IReadOnlyList<LuaConstant> constants, string value)
    {
        for (var i = 0; i < constants.Count; i++)
            if (constants[i].Kind == LuaConstantKind.String && constants[i].StringValue == value)
                return i;
        return -1;
    }
}
