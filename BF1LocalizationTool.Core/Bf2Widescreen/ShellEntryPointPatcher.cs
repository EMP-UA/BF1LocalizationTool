// =============================================================================
// BF1LocalizationTool.Core — Bf2Widescreen/ShellEntryPointPatcher.cs
// Автор / Author: EMP_UA (https://github.com/EMP-UA)
// Ліцензія / License: MIT
// =============================================================================
// UA: Побудова мінімального "bootstrap"-хука для entry-point скрипту
//     shell.lvl ("shell_interface") / ingame.lvl ("game_interface") —
//     той самий патерн, що емпірично підтверджено в SWBF2 Remaster-моді
//     (GT Anakin, moddb.com/mods/star-wars-battlefront-ii-full-hd-interface):
//     рушій BF2 автоматично викликає РІВНО один скрипт із цим іменем із
//     завантаженого .lvl — тому єдиний спосіб автозапустити власний код
//     без ручних дій користувача — підмінити САМЕ цей скрипт.
//
//     На відміну від Remaster-мода (який виносить логіку в ЗОВНІШНІ
//     addon-файли через ReadDataFile з відносним шляхом), наш підхід —
//     САМОДОСТАТНІЙ: усе (backup-копія оригінального entry-point +
//     widescreen-обгортка) пакується як ДОДАТКОВІ "scr_"-чанки всередині
//     ТОГО САМОГО shell.lvl/ingame.lvl. Це не залежить від жодної
//     сторонньої адон-інфраструктури (Remaster-мода чи іншої) і не
//     вимагає домовленостей про відносні шляхи файлів.
//
//     Bootstrap-заглушка (BuildBootstrapStub) робить рівно дві речі:
//       1. ScriptCB_DoFile(wrapperScriptName) — наш widescreen-фікс
//          (обгортає NewIFContainer/AddIFScreen/тощо формулою
//          x*W/800, y*H/600 — див. Lua50FunctionBuilder /
//          WidescreenWrapperBuilder).
//       2. ScriptCB_DoFile(stockScriptName) — попередньо перейменована
//          КОПІЯ оригінального важкого entry-point скрипту (та сама
//          логіка побудови меню, що й у vanilla — просто під іншим
//          іменем, щоб bootstrap міг її викликати).
//
// EN: Builds a minimal "bootstrap" hook for shell.lvl's entry-point
//     script ("shell_interface") / ingame.lvl's ("game_interface") — the
//     exact same pattern empirically confirmed in the SWBF2 Remaster mod
//     (GT Anakin, moddb.com/mods/star-wars-battlefront-ii-full-hd-interface):
//     the BF2 engine automatically calls EXACTLY one script with that
//     name from the loaded .lvl — so the only way to auto-run our own
//     code without any manual user action is to replace THAT SPECIFIC
//     script.
//
//     Unlike the Remaster mod (which pushes logic out to EXTERNAL addon
//     files via ReadDataFile with a relative path), our approach is
//     SELF-CONTAINED: everything (a backup copy of the original
//     entry-point + the widescreen wrapper) is packed as ADDITIONAL
//     "scr_" chunks inside the SAME shell.lvl/ingame.lvl. This has no
//     dependency on any third-party addon infrastructure (the Remaster
//     mod or anything else) and requires no relative-path conventions.
//
//     The bootstrap stub (BuildBootstrapStub) does exactly two things:
//       1. ScriptCB_DoFile(wrapperScriptName) — our widescreen fix
//          (wraps NewIFContainer/AddIFScreen/etc. with the x*W/800,
//          y*H/600 formula — see Lua50FunctionBuilder /
//          WidescreenWrapperBuilder).
//       2. ScriptCB_DoFile(stockScriptName) — a previously renamed COPY
//          of the original heavy entry-point script (the same
//          menu-building logic as vanilla — just under a different name
//          so the bootstrap can call it).
// =============================================================================

using System.Text;
using BF1LocalizationTool.Core.Chunks;
using BF1LocalizationTool.Core.Scripts;

namespace BF1LocalizationTool.Core.Bf2Widescreen;

public static class ShellEntryPointPatcher
{
    // -------------------------------------------------------------------------
    // UA: Будує прототип bootstrap-заглушки. Використовує ЛИШЕ
    //     GETGLOBAL/LOADK/CALL/RETURN — найпростіший можливий набір
    //     опкодів, ідентичний за структурою до реального game_interface
    //     bootstrap-стабу, знайденого в Remaster-моді (305 байт BODY),
    //     але БЕЗ жодного скопійованого байткоду — побудовано з нуля за
    //     формулою через Lua50FunctionBuilder.
    // EN: Builds the bootstrap stub prototype. Uses ONLY GETGLOBAL/
    //     LOADK/CALL/RETURN — the simplest possible opcode set, identical
    //     in structure to the real game_interface bootstrap stub found in
    //     the Remaster mod (305-byte BODY), but with NO copied bytecode
    //     whatsoever — built from scratch via Lua50FunctionBuilder.
    // -------------------------------------------------------------------------
    public static LuaFunctionPrototype BuildBootstrapStub(string wrapperScriptName, string stockScriptName)
    {
        var b = new Lua50FunctionBuilder
        {
            NumParams = 0,
            IsVararg = 0,
            MaxStackSize = 2, // UA: використовуємо лише R(0) і R(1) / EN: only R(0) and R(1) used
        };

        var doFileConst = b.AddStringConstant("ScriptCB_DoFile");
        var wrapperConst = b.AddStringConstant(wrapperScriptName);
        var stockConst = b.AddStringConstant(stockScriptName);

        // ScriptCB_DoFile(wrapperScriptName)
        b.EmitABx(LuaOpcode.GetGlobal, a: 0, bx: doFileConst);
        b.EmitABx(LuaOpcode.LoadK, a: 1, bx: wrapperConst);
        b.Emit(LuaOpcode.Call, a: 0, b: 2, c: 1);

        // ScriptCB_DoFile(stockScriptName)
        b.EmitABx(LuaOpcode.GetGlobal, a: 0, bx: doFileConst);
        b.EmitABx(LuaOpcode.LoadK, a: 1, bx: stockConst);
        b.Emit(LuaOpcode.Call, a: 0, b: 2, c: 1);

        b.Emit(LuaOpcode.Return, a: 0, b: 1);

        return b.Build();
    }

    // -------------------------------------------------------------------------
    // UA: Клонує наявний "scr_"-чанк під НОВИМ іменем (NAME), зберігаючи
    //     INFO/BODY байт-у-байт незмінними. Використовується щоб зберегти
    //     оригінальний "shell_interface"/"game_interface" під новим
    //     іменем (напр. "stock_shell_interface") ПЕРЕД тим, як замінити
    //     оригінальний слот bootstrap-заглушкою.
    // EN: Clones an existing "scr_" chunk under a NEW name (NAME), keeping
    //     INFO/BODY byte-for-byte unchanged. Used to preserve the
    //     original "shell_interface"/"game_interface" under a new name
    //     (e.g. "stock_shell_interface") BEFORE replacing the original
    //     slot with the bootstrap stub.
    // -------------------------------------------------------------------------
    public static UcfbChunk CloneScriptWithNewName(UcfbChunk scrChunk, string newName)
    {
        var nameBytes = Encoding.ASCII.GetBytes(newName + "\0");
        var newChildren = new List<UcfbChunk>(scrChunk.Children.Count);

        foreach (var child in scrChunk.Children)
        {
            newChildren.Add(child.FourCC == "NAME"
                ? new UcfbChunk
                {
                    Id = child.Id,
                    FourCC = child.FourCC,
                    DataSize = (uint)nameBytes.Length,
                    RawData = nameBytes,
                }
                : child); // UA: INFO/BODY — без змін / EN: INFO/BODY — unchanged
        }

        return new UcfbChunk
        {
            Id = scrChunk.Id,
            FourCC = scrChunk.FourCC,
            DataSize = 0, // UA: перераховується UcfbWriter автоматично (HasChildren) / EN: recomputed by UcfbWriter automatically (HasChildren)
            Children = newChildren,
        };
    }

    // -------------------------------------------------------------------------
    // UA: Будує повністю новий "scr_"-чанк (NAME + INFO + BODY) з готових
    //     байтів BODY (напр. результат Lua50BytecodeWriter.Write).
    //     infoByte — значення поля INFO; призначення цього поля НЕ
    //     реверс-інжинирено (див. ScriptChunkLocator), у ВСІХ 99
    //     перевірених реальних чанках воно 1 байт — тому за замовчуванням
    //     пишемо 0 і документуємо це як припущення, а не факт.
    // EN: Builds a brand-new "scr_" chunk (NAME + INFO + BODY) from ready
    //     BODY bytes (e.g. the result of Lua50BytecodeWriter.Write).
    //     infoByte — the INFO field's value; this field's purpose has NOT
    //     been reverse engineered (see ScriptChunkLocator), in ALL 99
    //     checked real chunks it's 1 byte — so we default to 0 and
    //     document this as an ASSUMPTION, not a confirmed fact.
    // -------------------------------------------------------------------------
    public static UcfbChunk BuildScriptChunk(string name, byte[] bodyBytes, byte infoByte = 0)
    {
        var nameBytes = Encoding.ASCII.GetBytes(name + "\0");

        var nameChunk = new UcfbChunk
        {
            Id = UcfbChunk.FourCcToId("NAME"),
            FourCC = "NAME",
            DataSize = (uint)nameBytes.Length,
            RawData = nameBytes,
        };
        var infoChunk = new UcfbChunk
        {
            Id = UcfbChunk.FourCcToId("INFO"),
            FourCC = "INFO",
            DataSize = 1,
            RawData = [infoByte],
        };
        var bodyChunk = new UcfbChunk
        {
            Id = UcfbChunk.FourCcToId("BODY"),
            FourCC = "BODY",
            DataSize = (uint)bodyBytes.Length,
            RawData = bodyBytes,
        };

        return new UcfbChunk
        {
            Id = UcfbChunk.FourCcToId("scr_"),
            FourCC = "scr_",
            DataSize = 0, // UA: перераховується UcfbWriter / EN: recomputed by UcfbWriter
            Children = [nameChunk, infoChunk, bodyChunk],
        };
    }

    // -------------------------------------------------------------------------
    // UA: Головна орхестрація: у корені (root.Children — де живуть усі
    //     top-level "scr_"-чанки, підтверджено емпірично на shell.lvl/
    //     ingame.lvl) знаходить entry-point скрипт за іменем entryName,
    //     (1) клонує його під stockName, (2) замінює ОРИГІНАЛЬНИЙ слот
    //     bootstrap-заглушкою, (3) додає wrapperBody як новий "scr_"-чанк
    //     wrapperName. Мутує root.Children напряму (List — мутабельний
    //     навіть при init-властивості) — після виклику root готовий для
    //     UcfbWriter.WriteFile.
    // EN: Main orchestration: in the root (root.Children — where all
    //     top-level "scr_" chunks live, empirically confirmed for
    //     shell.lvl/ingame.lvl) finds the entry-point script by entryName,
    //     (1) clones it under stockName, (2) replaces the ORIGINAL slot
    //     with the bootstrap stub, (3) adds wrapperBody as a new "scr_"
    //     chunk named wrapperName. Mutates root.Children directly (List —
    //     mutable even with an init property) — after the call, root is
    //     ready for UcfbWriter.WriteFile.
    // -------------------------------------------------------------------------
    public static void ApplyBootstrapPatch(
        UcfbChunk root,
        string entryName,
        string stockName,
        string wrapperName,
        byte[] wrapperBodyBytes)
    {
        var entryIndex = root.Children.FindIndex(c =>
            c.FourCC == "scr_" &&
            c.Children.FirstOrDefault(n => n.FourCC == "NAME") is { } nameChunk &&
            Encoding.ASCII.GetString(nameChunk.RawData).TrimEnd('\0') == entryName);

        if (entryIndex < 0)
            throw new InvalidOperationException(
                $"UA: Entry-point скрипт '{entryName}' не знайдено серед top-level 'scr_'-чанків / " +
                $"EN: Entry-point script '{entryName}' not found among top-level 'scr_' chunks");

        var originalEntryChunk = root.Children[entryIndex];

        // 1. UA: Зберегти оригінал під новим іменем / EN: Preserve the original under a new name
        var stockChunk = CloneScriptWithNewName(originalEntryChunk, stockName);
        root.Children.Add(stockChunk);

        // 2. UA: Замінити оригінальний слот bootstrap-заглушкою / EN: Replace the original slot with the bootstrap stub
        var bootstrapProto = BuildBootstrapStub(wrapperName, stockName);
        var bootstrapBytes = Lua50BytecodeWriter.Write(bootstrapProto);
        root.Children[entryIndex] = BuildScriptChunk(entryName, bootstrapBytes);

        // 3. UA: Додати наш wrapper-скрипт як новий чанк / EN: Add our wrapper script as a new chunk
        root.Children.Add(BuildScriptChunk(wrapperName, wrapperBodyBytes));
    }

    // =========================================================================
    // UA: !!! НЕ ВИКОРИСТОВУВАТИ ApplyBootstrapPatch ВИЩЕ ДЛЯ РЕАЛЬНОГО ФАЙЛУ !!!
    //     Цей метод НЕ ПРАЦЮЄ в грі: рушій, судячи з усього, НЕ шукає
    //     "scr_"-ресурси по всьому файлу довільно за іменем у рантаймі —
    //     набір top-level ресурсів фіксується десь при завантаженні, і
    //     чанки, додані ПІСЛЯ факту (як робить ApplyBootstrapPatch вище),
    //     просто не бачаться — ScriptCB_DoFile не знаходить ні wrapper, ні
    //     stock, і гра миттєво вилітає до головного меню.
    //     ПІДТВЕРДЖЕНО порівнянням топ-рівневих чанків vanilla проти
    //     Remaster shell.lvl: ОДНАКОВА кількість "scr_"-чанків (90 і там, і
    //     там) — тобто РЕАЛЬНО ПРАЦЮЮЧИЙ мод НЕ додає нових top-level
    //     "scr_"-чанків у сам shell.lvl, а лише редагує BODY вже наявного
    //     "shell_interface" НА МІСЦІ; копія оригінальної логіки (stock_*) у
    //     Remaster-моді живе в ОКРЕМОМУ addon-файлі (remaster_hook.lvl), не
    //     всередині shell.lvl.
    //     Використовуйте ApplySplicedInstallerPatch нижче.
    // EN: !!! DO NOT USE ApplyBootstrapPatch ABOVE FOR A REAL FILE !!!
    //     This method does NOT work in game: the engine, apparently, does
    //     NOT search for "scr_" resources across the whole file arbitrarily
    //     by name at runtime — the set of top-level resources is fixed
    //     somewhere at load time, and chunks added AFTER the fact (as
    //     ApplyBootstrapPatch above does) are simply never seen —
    //     ScriptCB_DoFile finds neither the wrapper nor the stock, and the
    //     game crashes to the main menu immediately.
    //     CONFIRMED by comparing top-level chunks of vanilla vs Remaster
    //     shell.lvl: an IDENTICAL "scr_" chunk count (90 in both) — meaning
    //     the ACTUALLY WORKING mod does NOT add new top-level "scr_" chunks
    //     to shell.lvl itself, it only edits the BODY of the
    //     already-existing "shell_interface" IN PLACE; the copy of the
    //     original logic (stock_*) in the Remaster mod lives in a SEPARATE
    //     addon file (remaster_hook.lvl), not inside shell.lvl.
    //     Use ApplySplicedInstallerPatch below instead.
    // =========================================================================

    // -------------------------------------------------------------------------
    // UA: Емпірично обґрунтований підхід: НІЧОГО не додає до дерева чанків.
    //     Замінює BODY-байти вже наявного entry-point-скрипту на нову версію
    //     ТІЄЇ Ж функції, куди вставлено виклик замикання-інсталятора
    //     (installerProto — напр. WidescreenWrapperBuilder.
    //     BuildWidescreenWrapperScript).
    //
    //     ТОЧКА ВСТАВКИ: НЕ pc 0, а перед першим 'ifs_*'-DoFile
    //     (FindFirstScreenDoFileIndex). Причина — детально
    //     в коментарі всередині методу: на pc 0 цільові UI-функції ще nil
    //     (визначаються пізніше через DoFile('ifelem_*')), тому хук на pc 0
    //     тихо перезаписувався. Вставка після примітивів і перед екранами
    //     гарантує, що NewButtonWindow/NewIFContainer уже визначені, а
    //     жоден екран ще не збудований.
    //
    //     Це безпечно, бо в Lua 5.0 ВСІ переходи (JMP/FORLOOP/TFORPREP) —
    //     ВІДНОСНІ (pc += sBx, офіційний lvm.c, dojump). Вставка +2
    //     інструкцій у СЕРЕДИНУ вимагає перерахунку лише тих переходів, що
    //     ПЕРЕТИНАЮТЬ точку вставки — це робить MapIndex/цикл перекодування
    //     нижче (переходи цілком до або цілком після точки — sBx
    //     незмінний). Ціль, що дорівнює точці вставки, лишається на ній
    //     (гілка виконає інсталятор і піде далі). Вкладені прототипи —
    //     інсталятор додається ОСТАННІМ у NestedPrototypes, індекси наявних
    //     CLOSURE лишаються коректними. Регістр інсталятора = MaxStackSize
    //     оригіналу (перший гарантовано НІКОЛИ не використаний регістр).
    // EN: An empirically grounded approach: adds NOTHING to the chunk tree.
    //     Replaces the BODY bytes of the already-existing entry-point
    //     script with a new version of the SAME function, splicing in a
    //     call to an installer closure (installerProto — e.g.
    //     WidescreenWrapperBuilder.BuildWidescreenWrapperScript).
    //
    //     INSERTION POINT: NOT pc 0, but before the first 'ifs_*' DoFile
    //     (FindFirstScreenDoFileIndex). Reason is
    //     detailed in the comment inside the method: at pc 0 the target UI
    //     functions are still nil (defined later via DoFile('ifelem_*')),
    //     so a pc-0 hook was silently overwritten. Inserting after the
    //     primitives and before the screens guarantees NewButtonWindow/
    //     NewIFContainer are already defined and no screen is built yet.
    //
    //     This is safe because in Lua 5.0 ALL jumps (JMP/FORLOOP/TFORPREP)
    //     are RELATIVE (pc += sBx, official lvm.c, dojump). Inserting +2
    //     instructions in the MIDDLE requires recomputing only the jumps
    //     that CROSS the insertion point — done by MapIndex / the remap
    //     loop below (jumps entirely before or entirely after keep their
    //     sBx). A target equal to the insertion point stays there (the
    //     branch runs the installer then continues). Nested prototypes —
    //     the installer is appended LAST in NestedPrototypes, so existing
    //     CLOSURE indices stay valid. Installer register = original's
    //     MaxStackSize (the first guaranteed NEVER-used register).
    // -------------------------------------------------------------------------
    public static void ApplySplicedInstallerPatch(UcfbChunk root, string entryName, LuaFunctionPrototype installerProto)
    {
        var entryChunk = root.Children.FirstOrDefault(c =>
            c.FourCC == "scr_" &&
            c.Children.FirstOrDefault(n => n.FourCC == "NAME") is { } nameChunk &&
            Encoding.ASCII.GetString(nameChunk.RawData).TrimEnd('\0') == entryName);

        if (entryChunk is null)
            throw new InvalidOperationException(
                $"UA: Entry-point скрипт '{entryName}' не знайдено серед top-level 'scr_'-чанків / " +
                $"EN: Entry-point script '{entryName}' not found among top-level 'scr_' chunks");

        var bodyIndex = entryChunk.Children.FindIndex(c => c.FourCC == "BODY");
        if (bodyIndex < 0)
            throw new InvalidOperationException(
                $"UA: '{entryName}' не має дочірнього чанка BODY / EN: '{entryName}' has no BODY child chunk");

        var bodyChunk = entryChunk.Children[bodyIndex];
        var parseResult = Lua50BytecodeReader.Parse(bodyChunk.RawData);
        var original = parseResult.Root;

        const int insertCount = 2; // UA: CLOSURE + CALL / EN: CLOSURE + CALL
        var installerRegister = original.MaxStackSize; // UA: перший НІКОЛИ не використаний регістр (0..MaxStack-1 зайняті) / EN: the first NEVER-used register (0..MaxStack-1 are in use)
        var installerProtoIndex = original.NestedPrototypes.Count; // UA: додаємо ОСТАННІМ / EN: appended LAST

        // UA: Точка вставки — НЕ pc 0.
        //     shell_interface спочатку завантажує примітиви інтерфейсу
        //     (ScriptCB_DoFile('ifelem_*','interface_util',...)), які
        //     ВИЗНАЧАЮТЬ NewButtonWindow/NewIFContainer як Lua-глобали, і
        //     лише ПОТІМ будує екрани (ScriptCB_DoFile('ifs_*')). Якщо
        //     хукати на pc 0 — цільові функції ще nil, а наступний
        //     DoFile('ifelem_buttonwindow') перезапише наш wrapper. Тому
        //     вставляємо ПЕРЕД першим 'ifs_*'-DoFile: примітиви вже
        //     визначені, жоден екран ще не збудований.
        // EN: The insertion point is NOT pc 0.
        //     shell_interface first loads UI primitives
        //     (ScriptCB_DoFile('ifelem_*','interface_util',...)) which
        //     DEFINE NewButtonWindow/NewIFContainer as Lua globals, and
        //     only THEN builds the screens (ScriptCB_DoFile('ifs_*')). If
        //     we hook at pc 0 the target functions are still nil, and the
        //     later DoFile('ifelem_buttonwindow') overwrites our wrapper.
        //     So we insert BEFORE the first 'ifs_*' DoFile: primitives are
        //     already defined, no screen is built yet.
        var insertAt = FindFirstScreenDoFileIndex(original);

        var newNested = new List<LuaFunctionPrototype>(original.NestedPrototypes) { installerProto };

        // UA: MAXARG_sBx для перекодування відносних переходів (excess-K).
        // EN: MAXARG_sBx for re-encoding relative jumps (excess-K).
        const int maxArgSBx = ((1 << 18) - 1) >> 1;

        // UA: Перевідображення старого індексу інструкції на новий після
        //     вставки insertCount інструкцій у позицію insertAt. ВАЖЛИВО:
        //     ціль, що дорівнює insertAt, ЗАЛИШАЄТЬСЯ на insertAt (=початок
        //     нашого інсталятора) — так гілки, що стрибали на першу
        //     екранну інструкцію, тепер спершу виконують інсталятор, а
        //     потім падають у оригінальний код (обидва шляхи — і
        //     fall-through, і стрибок — гарантовано проходять через хук).
        // EN: Maps an old instruction index to the new one after inserting
        //     insertCount instructions at insertAt. IMPORTANT: a target
        //     equal to insertAt STAYS at insertAt (= start of our
        //     installer) — so branches that jumped to the first screen
        //     instruction now run the installer first, then fall through
        //     into the original code (both paths — fall-through and jump —
        //     are guaranteed to pass through the hook).
        int MapIndex(int oldIndex, bool isJumpTarget) =>
            oldIndex < insertAt || (isJumpTarget && oldIndex == insertAt)
                ? oldIndex
                : oldIndex + insertCount;

        var remapped = new List<LuaInstruction>(original.Instructions.Count);
        for (var s = 0; s < original.Instructions.Count; s++)
        {
            var instr = original.Instructions[s];
            var isRelJump = instr.Opcode is LuaOpcode.Jmp or LuaOpcode.ForLoop or LuaOpcode.TForPrep;
            if (isRelJump && instr.SBx.HasValue)
            {
                var oldTarget = s + 1 + instr.SBx.Value;
                var newSource = MapIndex(s, isJumpTarget: false);
                var newTarget = MapIndex(oldTarget, isJumpTarget: true);
                var newSBx = newTarget - (newSource + 1);
                remapped.Add(instr with { Bx = newSBx + maxArgSBx, SBx = newSBx });
            }
            else
            {
                remapped.Add(instr);
            }
        }

        var newInstructions = new List<LuaInstruction>(original.Instructions.Count + insertCount);
        newInstructions.AddRange(remapped.Take(insertAt));
        newInstructions.Add(new LuaInstruction { Pc = 0, Opcode = LuaOpcode.Closure, A = installerRegister, Bx = installerProtoIndex });
        newInstructions.Add(new LuaInstruction { Pc = 0, Opcode = LuaOpcode.Call, A = installerRegister, B = 1, C = 1 }); // 0 args, 0 results
        newInstructions.AddRange(remapped.Skip(insertAt));
        // UA: Перенумерувати Pc (лише для читабельності звітів; Writer Pc не використовує).
        // EN: Renumber Pc (report readability only; the Writer ignores Pc).
        for (var i = 0; i < newInstructions.Count; i++)
            newInstructions[i] = newInstructions[i] with { Pc = i };

        var newLocals = original.LocalVariables
            .Select(l => l with
            {
                StartPc = MapIndex(l.StartPc, isJumpTarget: true),
                EndPc = MapIndex(l.EndPc, isJumpTarget: true),
            })
            .ToList();

        var patchedProto = original with
        {
            Instructions = newInstructions,
            NestedPrototypes = newNested,
            LocalVariables = newLocals,
            MaxStackSize = (byte)Math.Max(original.MaxStackSize, installerRegister + 1),
            SizeCode = newInstructions.Count,
        };

        var newBodyBytes = Lua50BytecodeWriter.Write(patchedProto);

        entryChunk.Children[bodyIndex] = new UcfbChunk
        {
            Id = bodyChunk.Id,
            FourCC = bodyChunk.FourCC,
            DataSize = (uint)newBodyBytes.Length,
            RawData = newBodyBytes,
        };
    }

    // -------------------------------------------------------------------------
    // UA: Знаходить індекс першої інструкції GETGLOBAL 'ScriptCB_DoFile',
    //     за якою одразу йде LOADK з рядком-константою, що починається на
    //     "ifs_" — тобто перший ЕКРАН (на відміну від примітивів
    //     інтерфейсу "ifelem_*"/"interface_util"/"globals", що йдуть
    //     раніше). Саме перед цією точкою треба встановлювати хук. Якщо
    //     патерн не знайдено — повертає 0 як запасний варіант, але для
    //     реального shell_interface він завжди є.
    // EN: Finds the index of the first GETGLOBAL 'ScriptCB_DoFile'
    //     instruction immediately followed by a LOADK with a string
    //     constant starting with "ifs_" — i.e. the first SCREEN (as
    //     opposed to the UI primitives "ifelem_*"/"interface_util"/
    //     "globals" loaded earlier). The hook must be installed right
    //     before this point. If the pattern isn't found — returns 0 as a
    //     fallback, but for a real shell_interface it always exists.
    // -------------------------------------------------------------------------
    private static int FindFirstScreenDoFileIndex(LuaFunctionPrototype proto)
    {
        var instrs = proto.Instructions;
        var consts = proto.Constants;

        static string? StringConst(IReadOnlyList<LuaConstant> pool, int? bx) =>
            bx is int i && i >= 0 && i < pool.Count && pool[i].Kind == LuaConstantKind.String
                ? pool[i].StringValue
                : null;

        for (var i = 0; i + 1 < instrs.Count; i++)
        {
            if (instrs[i].Opcode == LuaOpcode.GetGlobal &&
                StringConst(consts, instrs[i].Bx) == "ScriptCB_DoFile" &&
                instrs[i + 1].Opcode == LuaOpcode.LoadK &&
                StringConst(consts, instrs[i + 1].Bx) is { } arg &&
                arg.StartsWith("ifs_", StringComparison.Ordinal))
            {
                return i;
            }
        }

        return 0;
    }
}
