// =============================================================================
// BF1LocalizationTool.GUI — MainWindow.xaml.cs
// Автор / Author: EMP_UA (https://github.com/EMP-UA)
// Ліцензія / License: MIT
// =============================================================================

using System.IO;
using BF1LocalizationTool.Core.Localization;
using BF1LocalizationTool.Core.Models;
using BF1LocalizationTool.GUI.Services;
using Microsoft.Win32;
using System.Collections.ObjectModel;
using System.ComponentModel;
using System.Text;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Data;
using System.Windows.Media;

namespace BF1LocalizationTool.GUI;

public partial class MainWindow : Window
{
    // =========================================================================
    // UA: ПОЛЯ / EN: FIELDS
    // =========================================================================

    // UA: ДВА окремих сервіси — принципово різні ролі:
    //       _serviceSource — файл-ОРИГІНАЛ. READ-ONLY, ЖОДНОГО SaveAsync
    //         на ньому НІКОЛИ не викликається. Дає колонку "Оригінал".
    //       _serviceTarget — РОБОЧИЙ файл. Саме сюди пишеться переклад
    //         (ApplyRowEditsToService/SaveAsync), null поки не відкрито чи
    //         не створено ("Новий робочий з оригіналу"/"Робочий файл").
    //     Розділення навмисне: якщо перезаписувати той самий файл, звідки
    //     береться "Оригінал", після першого ж збереження колонка
    //     "Оригінал" почне показувати вже перекладений текст замість
    //     справжнього англійського — оригінал має лишатись незмінним
    //     назавжди.
    // EN: TWO separate services — fundamentally different roles:
    //       _serviceSource — the ORIGINAL file. READ-ONLY, SaveAsync is
    //         NEVER called on it. Provides the "Original" column.
    //       _serviceTarget — the WORKING file. This is where the
    //         translation is written (ApplyRowEditsToService/SaveAsync),
    //         null until opened or created ("New working from original"/
    //         "Working file").
    //     The split is deliberate: if the same file both provided
    //     "Original" and got overwritten, after the first save the
    //     "Original" column would start showing already-translated text
    //     instead of the real English — the original must stay unchanged
    //     forever.
    private readonly LvlLocalizationService _serviceSource = new();
    private LvlLocalizationService?         _serviceTarget;
    private string?                         _sourcePath;

    private readonly AutoSaveService        _autoSave = new();
    private ObservableCollection<EntryRow>  _allRows  = [];
    private ICollectionView?                _view;

    // UA: Статуси вичитки, ключ (Hash,Ordinal) — той самий ключ, яким
    //     Import/Export CSV і RefreshGrid уже зіставляють рядки. Живе тут,
    //     а НЕ у _serviceSource/_serviceTarget, бо RefreshGrid ПОВНІСТЮ
    //     перебудовує _allRows з нуля щоразу (зміна розміру шрифту, мови
    //     джерела/цілі тощо) — без окремого сховища живі позначки вичитки
    //     губились би при кожній такій перебудові, хоча жодних "нових
    //     даних" насправді не завантажувалось.
    // EN: Review statuses, keyed by (Hash,Ordinal) — the same key
    //     Import/Export CSV and RefreshGrid already use to match rows.
    //     Lives here, NOT in _serviceSource/_serviceTarget, because
    //     RefreshGrid FULLY rebuilds _allRows from scratch every time (font
    //     size change, source/target language switch, etc.) — without a
    //     separate store, live review marks would be lost on every such
    //     rebuild, even though no "new data" was actually loaded.
    private readonly Dictionary<(uint Hash, int Ordinal), string> _reviewStatuses = new();

    // UA: В грі НІКОЛИ не було української — "uk_english" це British/UK
    //     English, окремий реальний англомовний регіональний варіант
    //     (підтверджено вмістом: інше формулювання рядків, 98.9% "перекладено"
    //     на старті), а не порожній слот під переклад. За аналогією з
    //     попередніми проєктами (SteamWorld, Empire at War) переклад
    //     замінює САМЕ "english" — це мова, яка стоїть за замовчуванням
    //     на ліцензійній копії гри, і саме з неї береться текст для
    //     перекладу.
    // EN: The game NEVER had Ukrainian — "uk_english" is British/UK
    //     English, a real separate English regional variant (confirmed by
    //     content: different string wording, 98.9% "translated" out of the
    //     box), not an empty slot meant for translation. Following the
    //     precedent of prior projects (SteamWorld, Empire at War), the
    //     translation replaces "english" itself — the language that ships
    //     as default on a licensed copy of the game, and the one the
    //     translation is sourced from.
    private string _sourceLang = "english";
    private string _targetLang = "english";
    private bool   _isDarkTheme = true;
    private double _gridFontSize = 13;

    // UA: Властивість для прив'язки розміру шрифту з XAML
    // EN: Property for font size binding from XAML
    public double FontSizeGrid
    {
        get => _gridFontSize;
        set { _gridFontSize = value; SnapshotReviewStatuses(); RefreshGrid(); }
    }

    // =========================================================================
    // UA: ІНІЦІАЛІЗАЦІЯ / EN: INITIALIZATION
    // =========================================================================

    public MainWindow()
    {
        InitializeComponent();
        DataContext = this;

        _autoSave.AutoSaved += path =>
            Dispatcher.InvokeAsync(() =>
                TxtAutoSaveStatus.Text = $"✓ autosave {DateTime.Now:HH:mm}");

        // UA: Три теки біля .exe — "original" (вхід), "output" (готовий мод),
        //     "review" (робочий CSV з перекладом+вичиткою) — усі створюються
        //     ОДРАЗУ при першому запуску, і ВСІ ТРИ одразу отримують УСІ ТРИ
        //     ігрові підтеки (relativeGamePath: BF1, BF2, і BF1-Tat3-аддон),
        //     а не лише корінь.
        //
        //     Чому всі підтеки одразу, а не лінькво/по одній — те саме
        //     міркування для всіх трьох тек (воно не специфічне для
        //     "original", а є загальним правилом):
        //     core.lvl в BF1 і BF2 мають ІДЕНТИЧНІ імена, тож "плаский"
        //     корінь без підтек — це завжди прихована пастка з перезаписом,
        //     щойно в грі зʼявляється робота з ОБОМА іграми в одному
        //     сеансі (а RefreshGrid/EntryRow/BuildCsvContent це якраз і
        //     підтримують). Лінькве створення підтеки лише в момент, коли
        //     версія гри вже відома, не масштабується на "original"
        //     (версія гри ще невідома — файл не відкрито) і не застосовне
        //     до "review" взагалі (інакше робочий CSV писався б ПЛАСКО в
        //     корінь review\ — та сама пастка). Тому всі три теки
        //     поводяться ОДНАКОВО: підтеки готові заздалегідь, породжуючи ту
        //     саму структуру, що й реальна інсталяція Steam.
        //
        //     ТРЕТЯ підтека —
        //     аддон Tat3 (BF1): його core.lvl має ТЕ САМЕ ім'я, що й базовий
        //     BF1 core.lvl, але зовсім інший вміст (985 КБ, окрема таблиця
        //     Locl із 22 унікальними рядками) — той самий колізійний ризик,
        //     що змусив розділити BF1/BF2, тепер УСЕРЕДИНІ самого BF1.
        //     GetRelativeGamePath визначає Tat3 за "AddOn" у шляху ВІДКРИТОГО
        //     файлу (_sourcePath) — так само, як GameVersion визначається за
        //     "Battlefront II".
        //
        //     Побічний, але важливий ефект (стосується "original"):
        //     LvlLocalizationService.LoadAsync визначає GameVersion за
        //     наявністю "Battlefront II" У ШЛЯХУ файлу. Поклавши BF2-
        //     оригінал саме в підтеку "...Battlefront II Classic\...",
        //     автовизначення гри спрацює коректно; у корені "original\"
        //     напряму — програма мовчки вважатиме його BF1.
        // EN: Three folders next to the .exe — "original" (input), "output"
        //     (finished mod), "review" (working CSV with translation +
        //     review status) — ALL created IMMEDIATELY on first launch, and
        //     ALL THREE immediately get ALL THREE per-game subfolders
        //     (relativeGamePath: BF1, BF2, and the BF1-Tat3 add-on), not
        //     just the root.
        //
        //     Why all subfolders up front rather than lazily/one-at-a-time
        //     — the same reasoning applies to all three folders (it isn't
        //     specific to "original", it's a general rule): BF1's and
        //     BF2's core.lvl share IDENTICAL
        //     filenames, so a "flat" root with no subfolders is always a
        //     hidden overwrite trap the moment BOTH games are worked with
        //     in one session (which RefreshGrid/EntryRow/BuildCsvContent
        //     already support). Creating a subfolder lazily only once the
        //     game version becomes known doesn't scale to "original"
        //     (game version still unknown — file not opened yet) and
        //     doesn't apply to "review" at all (otherwise the working CSV
        //     would be written FLAT into review\'s root — the same trap).
        //     So all three folders behave THE SAME way: subfolders are
        //     ready up front, mirroring the exact structure of a real
        //     Steam install.
        //
        //     THIRD subfolder — the
        //     Tat3 (BF1) add-on: its core.lvl shares the SAME filename as
        //     the base BF1 core.lvl but has entirely different content
        //     (985 KB, its own Locl table with 22 unique strings) — the
        //     same collision risk that forced splitting BF1/BF2, now
        //     INSIDE BF1 itself. GetRelativeGamePath detects Tat3 from
        //     "AddOn" in the OPENED file's path (_sourcePath) — the same
        //     way GameVersion is detected from "Battlefront II".
        //
        //     A side effect, but an important one (applies to "original"):
        //     LvlLocalizationService.LoadAsync determines GameVersion from
        //     whether "Battlefront II" appears IN THE FILE'S PATH. Dropping
        //     a BF2 original into the "...Battlefront II Classic\..."
        //     subfolder makes auto-detection work correctly; dropping it
        //     straight into "original\"'s root would make the program
        //     silently assume BF1.
        EnsureRootAndGameSubfolders(GetOriginalRootPath());
        EnsureRootAndGameSubfolders(GetOutputRootPath());
        EnsureRootAndGameSubfolders(GetReviewRootPath());

        // UA: Підказка про три теки біля .exe — за зразком SWH.LocEditor
        //     (той самий текст-принцип: "поклади файл сюди, результат
        //     з'явиться там"), з явним застереженням про підтеки
        //     "original\": саме тут ховається реальна проблема з
        //     ідентичними іменами core.lvl. CheckForAutosave() нижче
        //     може ПЕРЕБИТИ цей текст (якщо знайдено автозбереження й
        //     користувач погодився відновити) — це навмисно: підказка про
        //     автозбереження важливіша й актуальніша в цей момент.
        // EN: A hint about the three folders next to the .exe — mirroring
        //     SWH.LocEditor (same text principle: "drop the file here, the
        //     result will appear there"), with an explicit note about the
        //     "original\" subfolders, since that's exactly where a real
        //     problem with identical core.lvl filenames hides.
        //     CheckForAutosave() below may OVERRIDE this text (if an
        //     autosave was found and the user agreed to restore it) —
        //     that's intentional: the autosave hint is more important and
        //     timely at that moment.
        SetStatus("UA: Поклади оригінал core.lvl у «original\\{Назва гри}\\...\\» (ПІДТЕКА гри, не корінь — " +
                   "у BF1 і BF2 однакове ім'я файлу!). Звідти типово відкриється \"Оригінал\". " +
                   "Збережений мод з'явиться в «output\\», робочий CSV з вичиткою — у «review\\». / " +
                   "EN: Drop the original core.lvl into \"original\\{Game name}\\...\\\" (the game's SUBFOLDER, " +
                   "not the root — BF1 and BF2 share the same filename!). \"Open original\" will default there. " +
                   "The saved mod appears in \"output\\\", the working CSV with review status — in \"review\\\".");

        SimpleLogger.Info("BF1LocalizationTool started");
        CheckForAutosave();
    }

    // =========================================================================
    // UA: ТЕКА ВИВОДУ / EN: OUTPUT FOLDER
    //     Той самий "підтеки шляху як у грі" підхід, що вже використовується
    //     в Diagnostic (GenerateNoDonorCyrillicCoreCommand.cs,
    //     GenerateLocalizedCoreCommand.cs, Program.cs: relativeGamePath) —
    //     свідомо повторно використовуємо ту саму пару шляхів, а не
    //     вигадуємо нову.
    // EN: The same "subfolders mirroring the in-game path" approach already
    //     used in Diagnostic (GenerateNoDonorCyrillicCoreCommand.cs,
    //     GenerateLocalizedCoreCommand.cs, Program.cs: relativeGamePath) —
    //     deliberately reusing the exact same path pair instead of inventing
    //     a new one.
    // =========================================================================

    private static string GetOutputRootPath() =>
        Path.Combine(AppContext.BaseDirectory, "output");

    private static string GetReviewRootPath() =>
        Path.Combine(AppContext.BaseDirectory, "review");

    private static string GetOriginalRootPath() =>
        Path.Combine(AppContext.BaseDirectory, "original");

    // UA: Аддон Tat3 (BF1, "Jabba's Palace") має ВЛАСНИЙ core.lvl — та сама
    //     назва файлу, що й у базової гри, але окремий вміст (перевірено
    //     байт-в-байт: 985 КБ, 6 Locl-чанків, з яких 2391 запис — точний
    //     дубль базової таблиці й лише 22 справді нові рядки; шрифтів
    //     усередині НЕМАЄ — Tat3 користується вже завантаженим атласом
    //     базової гри). Без окремої підтеки Tat3-файл колізує з базовим
    //     BF1 core.lvl у original\/output\/review\ — та сама пастка, що
    //     й для BF1/BF2 (див. коментар у конструкторі), лише тепер УСЕРЕДИНІ
    //     самого BF1. Реальний шлях узятий 1-в-1 зі steam-game-structure.txt:
    //     GameData\AddOn\Tat3\Data\_lvl_pc\core.lvl.
    // EN: The Tat3 add-on (BF1, "Jabba's Palace") ships its OWN core.lvl —
    //     same filename as the base game, but different content (verified
    //     byte-for-byte: 985 KB, 6 Locl chunks, of which 2391 entries are
    //     an exact duplicate of the base table and only 22 are genuinely
    //     new; NO fonts inside — Tat3 reuses the base game's already-loaded
    //     atlas). Without its own subfolder, the Tat3 file collides with
    //     the base BF1 core.lvl in original\/output\/review\ — the same
    //     trap as BF1 vs BF2 (see constructor comment), just now INSIDE
    //     BF1 itself. The real path is taken 1:1 from steam-game-structure.txt:
    //     GameData\AddOn\Tat3\Data\_lvl_pc\core.lvl.
    private static readonly string Tat3RelativeGamePath =
        Path.Combine("Star Wars Battlefront (Classic 2004)", "GameData", "AddOn", "Tat3", "Data", "_lvl_pc");

    // UA: Чи файл походить з аддону Tat3 — визначається за наявністю
    //     "AddOn" у шляху файлу, тим самим прийомом, яким
    //     LvlLocalizationService.LoadAsync визначає GameVersion за
    //     "Battlefront II" у шляху. Це ГУІ-локальна евристика (не чіпає
    //     Core.GameVersion, який лишається лише BF1/BF2 — Tat3 усе одно
    //     BF1-кодування, це виключно питання "у яку підтеку писати файли",
    //     не "яким кодеком читати").
    // EN: Whether a file comes from the Tat3 add-on — detected by "AddOn"
    //     appearing in the file's path, the same trick
    //     LvlLocalizationService.LoadAsync uses to detect GameVersion from
    //     "Battlefront II" in the path. This is a GUI-local heuristic (does
    //     not touch Core.GameVersion, which stays BF1/BF2 only — Tat3 is
    //     still BF1 encoding, this is purely "which subfolder to write
    //     into", not "which codec to read with").
    private static bool IsTat3AddOn(string? filePath) =>
        filePath is not null && filePath.Contains("AddOn", StringComparison.OrdinalIgnoreCase);

    // UA: Захист від конкретної реальної помилки користувача:
    //     вибір .csv-файлу (перекладу) у діалозі "Відкрити Оригінал/Робочий"
    //     — там очікується СПРАВЖНІЙ core.lvl (бінарний ucfb-контейнер), а
    //     CSV — лише "міст" перекладу (LocalizationCsvIo/Import CSV), не
    //     самостійний носій локалізації. Без цієї перевірки
    //     LvlLocalizationService.LoadAsync падає з малозрозумілим
    //     "Невірна магія файлу 0x48BFBBEF, очікується 'ucfb'" — 0x48BFBBEF
    //     це насправді байти UTF-8 BOM (EF BB BF) + 'H' з "Hash,Ordinal,..."
    //     — тобто буквально сам CSV-заголовок, прочитаний як бінарні дані.
    //     Перевірка ЗА РОЗШИРЕННЯМ (не за вмістом) — достатньо для типової
    //     помилки "не той файл у діалозі", повний UcfbReader.ReadFile
    //     лишається єдиним джерелом істини для дійсно зіпсованих .lvl.
    // EN: Guards against a specific real user mistake: picking
    //     a .csv (translation) file in the "Open Original/Working" dialog —
    //     that dialog expects a REAL core.lvl (binary ucfb container); CSV
    //     is only a translation "bridge" (LocalizationCsvIo/Import CSV),
    //     not a standalone localization carrier. Without this check,
    //     LvlLocalizationService.LoadAsync fails with an opaque "Invalid
    //     file magic 0x48BFBBEF, expected 'ucfb'" — 0x48BFBBEF is actually
    //     the UTF-8 BOM bytes (EF BB BF) + 'H' from "Hash,Ordinal,..." —
    //     i.e. literally the CSV header read as binary data. Checked BY
    //     EXTENSION (not content) — enough for the typical "wrong file in
    //     the dialog" slip; the full UcfbReader.ReadFile stays the single
    //     source of truth for genuinely corrupt .lvl files.
    private bool WarnAndAbortIfCsvPicked(string filePath)
    {
        if (!filePath.EndsWith(".csv", StringComparison.OrdinalIgnoreCase))
            return false;

        System.Windows.MessageBox.Show(
            "UA: Це CSV-файл перекладу, а не core.lvl. Спочатку відкрийте СПРАВЖНІЙ core.lvl " +
            "як Оригінал і Робочий файл, а тоді скористайтесь кнопкою «Імпорт CSV» — вона " +
            "підтягне переклад із цього файлу за хешем у вже відкриту сесію.\n\n" +
            "EN: This is a CSV translation file, not core.lvl. First open the ACTUAL core.lvl " +
            "as Original and Working file, then use the \"Import CSV\" button — it pulls " +
            "translations from this file by hash into the already-open session.",
            "UA: Не той тип файлу / EN: Wrong file type",
            MessageBoxButton.OK, MessageBoxImage.Warning);
        return true;
    }

    // UA: sourcePath — шлях файлу, що визначає СЕСІЮ (типово _sourcePath,
    //     тобто відкритий Оригінал). Якщо null або не з Tat3 — звичайна
    //     поведінка за GameVersion, як і раніше.
    // EN: sourcePath — the file path that determines the SESSION (normally
    //     _sourcePath, i.e. the opened Original). If null or not from
    //     Tat3 — normal GameVersion-based behavior, unchanged.
    private static string GetRelativeGamePath(GameVersion game, string? sourcePath = null)
    {
        if (game == GameVersion.BF1 && IsTat3AddOn(sourcePath))
            return Tat3RelativeGamePath;

        return game switch
        {
            GameVersion.BF2 => Path.Combine("Star Wars Battlefront II Classic", "GameData", "data", "_lvl_pc"),
            _                => Path.Combine("Star Wars Battlefront (Classic 2004)", "GameData", "Data", "_LVL_PC"),
        };
    }

    // UA: Створює корінь + УСІ ТРИ ігрові підтеки (BF1, BF2, BF1-Tat3-аддон)
    //     під ним. Спільний хелпер для "original"/"output"/"review"
    //     (конструктор) — без нього довелось би повторювати той самий
    //     виклик Directory.CreateDirectory для кожної підтеки окремо.
    //     Директорія, що вже існує, — не помилка (Directory.CreateDirectory
    //     ідемпотентний).
    // EN: Creates the root + ALL THREE per-game subfolders (BF1, BF2,
    //     BF1-Tat3-add-on) under it. Shared helper for
    //     "original"/"output"/"review" (constructor) — without it the same
    //     Directory.CreateDirectory call would have to be repeated for each
    //     subfolder separately. An already-existing directory is not an
    //     error (Directory.CreateDirectory is idempotent).
    private static void EnsureRootAndGameSubfolders(string rootPath)
    {
        Directory.CreateDirectory(rootPath);
        Directory.CreateDirectory(Path.Combine(rootPath, GetRelativeGamePath(GameVersion.BF1)));
        Directory.CreateDirectory(Path.Combine(rootPath, GetRelativeGamePath(GameVersion.BF2)));
        Directory.CreateDirectory(Path.Combine(rootPath, Tat3RelativeGamePath));
    }

    // UA: Формат мітки часу — "core 260729 2013"
    //     (core {yyMMdd} {HHmm}.lvl) — гарантує, що повторне збереження
    //     НІКОЛИ не перезапише попередній файл.
    // EN: Timestamp format — "core 260729 2013"
    //     (core {yyMMdd} {HHmm}.lvl) — guarantees a repeat save NEVER
    //     overwrites the previous file.
    private static string BuildTimestampedCoreFileName() =>
        $"core {DateTime.Now:yyMMdd HHmm}.lvl";

    private void CheckForAutosave()
    {
        var latest = _autoSave.FindLatestAutosave();
        if (latest is null) return;

        var result = System.Windows.MessageBox.Show(
            $"UA: Знайдено автозбереження:\n{latest}\n\nВідновити?\n\n" +
            $"EN: Autosave found:\n{latest}\n\nRestore?",
            "UA: Автозбереження / EN: Autosave",
            MessageBoxButton.YesNo, MessageBoxImage.Question);

        if (result == MessageBoxResult.Yes)
            TxtStatus.Text = $"UA: Відкрийте оригінал і робочий файл, тоді імпортуйте: {latest} / " +
                              $"EN: Open the original and working file, then import: {latest}";
    }

    // =========================================================================
    // UA: ВІДКРИТИ ОРИГІНАЛ (READ-ONLY) / EN: OPEN ORIGINAL (READ-ONLY)
    //     Завжди дає колонку "Оригінал". SaveAsync на _serviceSource
    //     НІКОЛИ не викликається — файл лишається незмінним назавжди.
    //     Always provides the "Original" column. SaveAsync is NEVER
    //     called on _serviceSource — the file stays unchanged forever.
    // =========================================================================

    private async void BtnOpenOriginal_Click(object sender, RoutedEventArgs e)
    {
        // UA: Типово відкриється тека "original\" біля .exe (створена в
        //     конструкторі) — користувач кладе туди свій core.lvl ОДИН РАЗ
        //     і надалі не мусить щоразу лізти в теку інсталяції гри. За
        //     потреби можна обрати інший файл в іншому місці просто в
        //     цьому самому діалозі — нічого не форсується.
        // EN: Defaults to opening the "original\" folder next to the .exe
        //     (created in the constructor) — the user drops their core.lvl
        //     there ONCE and no longer has to browse into the game's
        //     install folder every time. A different file elsewhere can
        //     still be picked right in this same dialog — nothing is forced.
        var dlg = new OpenFileDialog
        {
            Title            = "UA: Відкрити ОРИГІНАЛ (read-only) / EN: Open ORIGINAL (read-only)",
            Filter           = "LVL files (*.lvl)|*.lvl|All files (*.*)|*.*",
            InitialDirectory = GetOriginalRootPath(),
            FileName         = "core.lvl"
        };
        if (dlg.ShowDialog() != true) return;
        if (WarnAndAbortIfCsvPicked(dlg.FileName)) return;

        SetBusy(true, "UA: Завантаження оригіналу... / EN: Loading original...");
        SimpleLogger.Info($"Opening original: {dlg.FileName}");

        try
        {
            await _serviceSource.LoadAsync(dlg.FileName);
            _sourcePath = dlg.FileName;

            // UA: Новий документ — старі позначки вичитки (від, можливо,
            //     геть іншого файлу/гри) тут неактуальні й потенційно
            //     оманливі (той самий (Hash,Ordinal) теоретично може
            //     збігтись між різними core.lvl). Реальний спосіб
            //     повернути позначки вичитки в нову сесію — Import CSV із
            //     раніше збереженого робочого файлу (review\...csv).
            // EN: A new document — old review marks (possibly from an
            //     entirely different file/game) are stale here and
            //     potentially misleading (the same (Hash,Ordinal) could in
            //     theory collide across different core.lvl files). The
            //     real way to bring review marks back into a new session
            //     is Import CSV from a previously saved working file
            //     (review\...csv).
            _reviewStatuses.Clear();

            await Dispatcher.InvokeAsync(() =>
            {
                PopulateSourceLanguageSelector();
                RefreshGrid();

                BtnNewWorking.IsEnabled  = true;
                BtnOpenWorking.IsEnabled = true;
                BtnExportCsv.IsEnabled   = _allRows.Count > 0;

                var gameLabel = _serviceSource.GameVersion == GameVersion.BF1
                    ? "BF1 (2004)" : "BF2 (2005)";
                Title = $"BF1 Localization Tool — EMP_UA [{gameLabel}]";
                SetStatus($"UA: Оригінал завантажено [{gameLabel}]: {dlg.FileName} / EN: Original loaded [{gameLabel}]: {dlg.FileName}");
            });

            SimpleLogger.Info($"Original loaded OK: {dlg.FileName}");
        }
        catch (Exception ex)
        {
            SimpleLogger.Error("Load original failed", ex);
            System.Windows.MessageBox.Show(
                $"UA: Помилка завантаження оригіналу:\n{ex.Message}\n\nEN: Original load error:\n{ex.Message}",
                "Error", MessageBoxButton.OK, MessageBoxImage.Error);
            SetStatus("UA: Помилка завантаження оригіналу / EN: Original load error");
        }
        finally { SetBusy(false); }
    }

    // =========================================================================
    // UA: НОВИЙ РОБОЧИЙ ФАЙЛ З ОРИГІНАЛУ / EN: NEW WORKING FILE FROM ORIGINAL
    //     Клонує оригінал у пам'яті (повторне LoadAsync з ТОГО САМОГО
    //     шляху — незалежний екземпляр, оригінал на диску не чіпається)
    //     — переклад "з нуля". Шлях на диску визначиться лише при
    //     першому Save (SaveFileDialog).
    // EN: Clones the original in memory (re-runs LoadAsync from the SAME
    //     path — an independent instance, the on-disk original is
    //     untouched) — "from scratch" translation. The disk path is only
    //     decided on the first Save (SaveFileDialog).
    // =========================================================================

    private async void BtnNewWorking_Click(object sender, RoutedEventArgs e)
    {
        if (_sourcePath is null) return;

        SetBusy(true, "UA: Створення робочого файлу... / EN: Creating working file...");
        try
        {
            var target = new LvlLocalizationService();
            await target.LoadAsync(_sourcePath);
            _serviceTarget = target;

            await Dispatcher.InvokeAsync(() =>
            {
                PopulateTargetLanguageSelector();
                ApplyCyrillicCodeTableToTargetIfPresent(_sourcePath);
                // UA: Оригінал (і тому Hash/Ordinal) той самий — лише
                //     ЦІЛЬОВИЙ файл змінюється, тож збережені позначки
                //     вичитки лишаються осмисленими й НЕ повинні губитись.
                // EN: The original (and thus Hash/Ordinal) is unchanged —
                //     only the TARGET file changes, so saved review marks
                //     stay meaningful and must NOT be lost.
                SnapshotReviewStatuses();
                RefreshGrid();
                SetButtonsEnabled(true);
                SetStatus("UA: Новий робочий файл створено з оригіналу (у пам'яті) — шлях на диску визначиться при збереженні / " +
                          "EN: New working file created from the original (in memory) — disk path decided on save");
            });

            SimpleLogger.Info("New working file created from original (in-memory clone)");
        }
        catch (Exception ex)
        {
            SimpleLogger.Error("New working file failed", ex);
            System.Windows.MessageBox.Show(
                $"UA: Помилка створення робочого файлу:\n{ex.Message}\n\nEN: Working file creation error:\n{ex.Message}",
                "Error", MessageBoxButton.OK, MessageBoxImage.Error);
        }
        finally { SetBusy(false); }
    }

    // =========================================================================
    // UA: ВІДКРИТИ ІСНУЮЧИЙ РОБОЧИЙ ФАЙЛ / EN: OPEN EXISTING WORKING FILE
    //     Типовий випадок — згенерований шрифто-пропатчений core.lvl з
    //     output-теки (Diagnostic → 7 → 1), або раніше збережений мод-
    //     файл із частковим перекладом.
    // EN: Typical case — the font-patched core.lvl generated in the
    //     output folder (Diagnostic → 7 → 1), or a previously saved mod
    //     file with a partial translation.
    // =========================================================================

    private async void BtnOpenWorking_Click(object sender, RoutedEventArgs e)
    {
        // UA: Типово відкриється output\{Гра}\... — та сама тека, куди
        //     "Зберегти" вже пише (BtnSave_Click) — бо саме туди й
        //     потрапляють раніше збережені робочі файли. Кнопка активна
        //     лише ПІСЛЯ відкриття оригіналу, тож _serviceSource.GameVersion
        //     тут уже відомий. Створюємо теку заздалегідь — вона може ще
        //     не існувати, якщо збереження ще не відбувалось цього сеансу.
        // EN: Defaults to opening output\{Game}\... — the same folder
        //     "Save" already writes into (BtnSave_Click) — since that's
        //     exactly where previously saved working files end up. The
        //     button is only enabled AFTER the original is opened, so
        //     _serviceSource.GameVersion is already known here. The folder
        //     is created up front — it may not exist yet if no save
        //     happened this session.
        var defaultWorkingDir = Path.Combine(GetOutputRootPath(), GetRelativeGamePath(_serviceSource.GameVersion, _sourcePath));
        Directory.CreateDirectory(defaultWorkingDir);

        var dlg = new OpenFileDialog
        {
            Title            = "UA: Відкрити РОБОЧИЙ файл (буде перезаписано при збереженні) / " +
                                "EN: Open WORKING file (will be overwritten on save)",
            Filter           = "LVL files (*.lvl)|*.lvl|All files (*.*)|*.*",
            InitialDirectory = defaultWorkingDir,
            FileName         = "core.lvl"
        };
        if (dlg.ShowDialog() != true) return;
        if (WarnAndAbortIfCsvPicked(dlg.FileName)) return;

        SetBusy(true, "UA: Завантаження робочого файлу... / EN: Loading working file...");
        SimpleLogger.Info($"Opening working file: {dlg.FileName}");

        try
        {
            // UA: АРХІТЕКТУРНО: структурний майстер — ЗАВЖДИ ОРИГІНАЛ. Якщо
            //     замість цього обраний тут файл ставав би _serviceTarget
            //     (структурним носієм усього, що піде в гру — SaveAsync
            //     серіалізує дерево робочого файлу цілком), чужий або
            //     застарілий робочий файл тягнув би у збереження свої
            //     шрифти, свою таблицю рядків і свій розмір — а правки з
            //     таблиці мовчки губились би там, де його рядків не існує.
            //     Саме такий збій стався реально: у теку аддону Tat3
            //     зберігся файл базової гри (4,9 МБ, 6 шрифтів, без 22
            //     рядків Джабби).
            //
            //     Робочий сервіс будується з _sourcePath (той самий шлях,
            //     що і в "Новий робочий з оригіналу"), а обраний файл
            //     виступає ЛИШЕ ДОНОРОМ ТЕКСТУ: з нього переносяться
            //     переклади за (Hash, Ordinal) — рівно як це вже робить
            //     "Імпорт CSV", тільки джерело .lvl замість .csv.
            //
            //     Наслідок: неможливо зберегти "чужий" документ під
            //     виглядом свого. Все, що не є текстом (шрифти, структура,
            //     розмір), береться з оригіналу за побудовою, а не за
            //     домовленістю — тож і AdoptFontsFrom нижче стає
            //     страхувальником, а не єдиним бар'єром.
            // EN: ARCHITECTURE: the structural master is ALWAYS THE
            //     ORIGINAL. If the file picked here instead became
            //     _serviceTarget (the STRUCTURAL carrier of everything
            //     shipped to the game — SaveAsync serializes the working
            //     file's whole tree), a foreign or stale working file
            //     would drag its own fonts, its own string table and its
            //     own size into the save — while grid edits were silently
            //     lost wherever its strings didn't exist. That exact
            //     failure happened for real: the base game's file got
            //     saved into the Tat3 add-on folder (4.9 MB, 6 fonts, no
            //     Jabba strings).
            //
            //     The working service is built from _sourcePath (the same
            //     path "New working from original" uses), and the picked
            //     file acts ONLY AS A TEXT DONOR: translations are
            //     carried over by (Hash, Ordinal) — exactly as "Import
            //     CSV" already does, just sourced from a .lvl instead of
            //     a .csv.
            //
            //     Consequence: it is not possible to save a "foreign"
            //     document under your own file's name. Everything that
            //     isn't text (fonts, structure, size) comes from the
            //     original BY CONSTRUCTION rather than by convention —
            //     which makes AdoptFontsFrom below a safety net rather
            //     than the only barrier.
            if (_sourcePath is null)
                throw new InvalidOperationException(
                    "UA: Спочатку відкрийте оригінал / EN: Open the original first");

            var target = new LvlLocalizationService();
            await target.LoadAsync(_sourcePath);

            var donor = new LvlLocalizationService();
            await donor.LoadAsync(dlg.FileName);

            _serviceTarget = target;

            var merged = 0;
            await Dispatcher.InvokeAsync(() =>
            {
                PopulateTargetLanguageSelector();
                // UA: Кодек кирилиці беремо за ОРИГІНАЛОМ — саме його дерево
                //     тепер зберігається; сайдкар біля донора стосувався б
                //     файлу, що більше не є структурним носієм.
                // EN: The Cyrillic codec follows the ORIGINAL — its tree is
                //     what gets saved now; a sidecar next to the donor would
                //     describe a file that is no longer the structural carrier.
                var codeTable = ApplyCyrillicCodeTableToTargetIfPresent(_sourcePath);

                merged = MergeTranslationsFromDonor(donor, target);

                // UA: Той самий оригінал — позначки вичитки лишаються
                //     осмисленими, не втрачаємо їх (див. коментар у
                //     BtnNewWorking_Click).
                // EN: Same original — review marks stay meaningful, don't
                //     lose them (see the comment in BtnNewWorking_Click).
                SnapshotReviewStatuses();
                RefreshGrid();
                SetButtonsEnabled(true);

                var codecNote = codeTable is not null
                    ? " · UA: кодек кирилиці активний / EN: Cyrillic codec active"
                    : "";
                SetStatus(
                    $"UA: Переклад узято з {dlg.FileName} ({merged} рядків), структура й шрифти — з оригіналу{codecNote} / " +
                    $"EN: Translation taken from {dlg.FileName} ({merged} strings), structure and fonts — from the original{codecNote}");
            });

            _autoSave.Start(BuildCsvContent);
            SimpleLogger.Info($"Working file loaded OK: {dlg.FileName}");
        }
        catch (Exception ex)
        {
            SimpleLogger.Error("Load working file failed", ex);
            System.Windows.MessageBox.Show(
                $"UA: Помилка завантаження робочого файлу:\n{ex.Message}\n\nEN: Working file load error:\n{ex.Message}",
                "Error", MessageBoxButton.OK, MessageBoxImage.Error);
            SetStatus("UA: Помилка завантаження робочого файлу / EN: Working file load error");
        }
        finally { SetBusy(false); }
    }

    // UA: Файл-супутник cyrillic-code-table.json поруч із файлом за
    //     lvlFilePath (не обов'язково є — лише файли, що пройшли через
    //     GenerateLocalizedCoreCommand, його мають). Якщо знайдено —
    //     кодек кирилиці підключається ЯВНО до _serviceTarget
    //     (LvlLocalizationService.ApplyCyrillicCodeTable) для поточної
    //     _targetLang, і Original цієї мови одразу декодується для показу.
    // EN: cyrillic-code-table.json sidecar next to the file at
    //     lvlFilePath (not guaranteed to exist — only files that went
    //     through GenerateLocalizedCoreCommand have one). If found — the
    //     Cyrillic codec is wired up EXPLICITLY on _serviceTarget
    //     (LvlLocalizationService.ApplyCyrillicCodeTable) for the current
    //     _targetLang, and that language's Original is decoded right away
    //     for display.
    // UA: Переносить ЛИШЕ ТЕКСТ із файлу-донора (раніше збережений .lvl) у
    //     робочий сервіс, зіставляючи за (Hash, Ordinal) відносно ОРИГІНАЛУ.
    //     Повертає кількість перенесених рядків.
    //
    //     Правило "де в донорі лежить переклад" — те саме, що вже діє в
    //     RefreshGrid: у щойно завантаженому файлі перекладу немає в полі
    //     Translation (воно заповнюється лише GUI-редагуванням цієї сесії),
    //     натомість він фізично лежить в Original — бо саме туди його записав
    //     SaveAsync минулого разу. Тому переклад = Original донора, якщо він
    //     ВІДРІЗНЯЄТЬСЯ від Original оригіналу. Свідомо не дублюємо це
    //     правило новою логікою, а повторюємо наявне.
    // EN: Carries ONLY TEXT from a donor file (a previously saved .lvl) into
    //     the working service, matching by (Hash, Ordinal) against the
    //     ORIGINAL. Returns how many strings were carried over.
    //
    //     The rule for "where the translation lives in the donor" is the one
    //     already used by RefreshGrid: in a freshly loaded file there is no
    //     Translation (that field is only filled by this session's GUI
    //     edits); the translation physically sits in Original, because that's
    //     where SaveAsync wrote it last time. So the translation = the
    //     donor's Original whenever it DIFFERS from the original's Original.
    //     Deliberately reusing the existing rule instead of duplicating it.
    private int MergeTranslationsFromDonor(LvlLocalizationService donor, LvlLocalizationService target)
    {
        var sourceFile = _serviceSource.GetLanguageFile(_sourceLang);
        var donorFile  = donor.GetLanguageFile(_targetLang);
        var targetFile = target.GetLanguageFile(_targetLang);
        if (sourceFile is null || donorFile is null || targetFile is null) return 0;

        var merged = 0;
        var occurrence = new Dictionary<uint, int>();

        foreach (var e in sourceFile.Entries)
        {
            var ordinal = occurrence.TryGetValue(e.Hash, out var n) ? n : 0;
            occurrence[e.Hash] = ordinal + 1;

            var donorEntry = donorFile.GetByHash(e.Hash, ordinal);
            if (donorEntry is null) continue;

            var text = donorEntry.Translation
                       ?? (donorEntry.Original != e.Original ? donorEntry.Original : null);
            if (string.IsNullOrWhiteSpace(text)) continue;

            var targetEntry = targetFile.GetByHash(e.Hash, ordinal);
            if (targetEntry is null) continue;

            targetEntry.Translation = text;
            merged++;
        }

        return merged;
    }

    // UA: Порівнює ОРИГІНАЛ і РОБОЧИЙ файл як ДОКУМЕНТИ (а не як переклади)
    //     і повертає готовий текст попередження, або null якщо все гаразд.
    //     Детальний опис причини — над місцем виклику в BtnSave_Click.
    //     Навмисно повертає ТЕКСТ, а не bool: користувач має бачити КОНКРЕТНІ
    //     числа (скільки рядків розійшлось, скільки шрифтів), бо саме
    //     мовчазність попередніх кроків і дала збій, який це ловить.
    // EN: Compares the ORIGINAL and the WORKING file as DOCUMENTS (not as
    //     translations) and returns ready-made warning text, or null if all
    //     is well. Full rationale sits above the call site in BtnSave_Click.
    //     Deliberately returns TEXT rather than a bool: the user must see the
    //     CONCRETE numbers (how many strings diverged, how many fonts),
    //     because it was precisely the silence of earlier steps that produced
    //     the failure this catches.
    private string? BuildSourceTargetMismatchReport(LvlLocalizationService target)
    {
        var problems = new List<string>();

        // UA: 1) Розбіжність множин хешів — різні документи.
        // EN: 1) Hash-set divergence — different documents.
        var languages = _serviceSource.AvailableLanguages
            .Intersect(target.AvailableLanguages, StringComparer.OrdinalIgnoreCase)
            .ToList();

        foreach (var lang in languages)
        {
            var sourceHashes = _serviceSource.GetHashSet(lang);
            var targetHashes = target.GetHashSet(lang);
            if (sourceHashes.Count == 0 && targetHashes.Count == 0) continue;

            var missingInTarget = sourceHashes.Except(targetHashes).Count();
            var extraInTarget   = targetHashes.Except(sourceHashes).Count();
            if (missingInTarget == 0 && extraInTarget == 0) continue;

            problems.Add(
                $"  • [{lang}] UA: в оригіналі є {missingInTarget} рядків, яких НЕМАЄ в робочому; " +
                $"у робочому є {extraInTarget} зайвих / EN: original has {missingInTarget} strings " +
                $"MISSING from the working file; the working file has {extraInTarget} extra");
        }

        // UA: 2) Шрифти в аддоні — окремий, значно небезпечніший симптом.
        // EN: 2) Fonts in an add-on — a separate, far more dangerous symptom.
        var sourceFonts = _serviceSource.FontResourceCount();
        var targetFonts = target.FontResourceCount();
        if (sourceFonts == 0 && targetFonts > 0)
        {
            problems.Add(
                $"  • UA: оригінал не містить шрифтів (це ознака файлу-АДДОНУ), а робочий містить " +
                $"{targetFonts} — вони потраплять у збережений файл і зламають ВЕСЬ текст на мапі аддону / " +
                $"EN: the original carries no fonts (the signature of an ADD-ON file) while the working " +
                $"file carries {targetFonts} — they would ship into the saved file and break ALL text on the add-on's map");
        }

        if (problems.Count == 0) return null;

        return
            "UA: Оригінал і робочий файл — це РІЗНІ файли, а не дві версії одного:\n\n" +
            string.Join("\n", problems) +
            "\n\nЗбереження запише вміст РОБОЧОГО файлу під іменем/текою, обраними для цього сеансу.\n" +
            "Правильний порядок: відкрити потрібний core.lvl як «Оригінал», далі «Новий робочий з оригіналу»,\n" +
            "і аж тоді «Імпорт CSV» для перенесення вже готового перекладу.\n\n" +
            "EN: The Original and the Working file are DIFFERENT files, not two versions of one:\n\n" +
            string.Join("\n", problems) +
            "\n\nSaving writes the WORKING file's content under the name/folder chosen for this session.\n" +
            "Correct order: open the intended core.lvl as \"Original\", then \"New working from original\",\n" +
            "and only then \"Import CSV\" to carry over an existing translation.\n\n" +
            "UA: Все одно зберегти? / EN: Save anyway?";
    }

    private CyrillicCodeTable? ApplyCyrillicCodeTableToTargetIfPresent(string lvlFilePath)
    {
        if (_serviceTarget is null) return null;

        var codeTable = CyrillicCodeTable.TryLoadNextTo(lvlFilePath);
        if (codeTable is not null)
        {
            _serviceTarget.ApplyCyrillicCodeTable(codeTable, _targetLang);
            SimpleLogger.Info(
                $"Cyrillic code table applied to working file: {codeTable.Letters.Count} letters, target='{_targetLang}'");
        }
        return codeTable;
    }

    // =========================================================================
    // UA: ЗБЕРЕГТИ / EN: SAVE
    //     Пише ЛИШЕ в _serviceTarget — _serviceSource ніколи не чіпається.
    //     Writes ONLY into _serviceTarget — _serviceSource is never touched.
    // =========================================================================

    private async void BtnSave_Click(object sender, RoutedEventArgs e)
    {
        // UA: Захоплюємо в локальну змінну одразу після перевірки на null —
        //     так компілятор гарантовано не втратить звуження non-null
        //     через await/виклики методів далі в цьому методі.
        // EN: Capture into a local right after the null check — this way
        //     the compiler reliably keeps the non-null narrowing across
        //     awaits/method calls later in this method.
        if (_serviceTarget is not { } target)
        {
            System.Windows.MessageBox.Show(
                "UA: Спочатку створіть або відкрийте робочий файл ('Новий робочий' / 'Робочий файл').\n\n" +
                "EN: First create or open a working file ('New working' / 'Working file').",
                "UA: Немає робочого файлу / EN: No working file",
                MessageBoxButton.OK, MessageBoxImage.Warning);
            return;
        }

        // UA: Автотека + мітка часу + гра-специфічна підтека — рахуємо
        //     ЛИШЕ тут, бо GameVersion відомий тільки
        //     після відкриття оригіналу/робочого файлу (_serviceSource.
        //     GameVersion, визначається в LvlLocalizationService.LoadAsync
        //     за наявністю "Battlefront II" у шляху джерела). Тека
        //     створюється ЗАЗДАЛЕГІДЬ (до показу діалогу), щоб діалог одразу
        //     відкрився саме в ній — це і є "генерування тек", а не просто
        //     підказка в текстовому полі.
        // EN: Auto-folder + timestamp + game-specific subfolder — computed
        //     ONLY here because GameVersion is
        //     only known after the original/working file was opened
        //     (_serviceSource.GameVersion, determined in
        //     LvlLocalizationService.LoadAsync from "Battlefront II" in the
        //     source path). The folder is created UP FRONT (before the
        //     dialog is shown) so the dialog opens directly inside it — this
        //     is the actual "folder generation", not just a text-field hint.
        var defaultDir = Path.Combine(GetOutputRootPath(), GetRelativeGamePath(_serviceSource.GameVersion, _sourcePath));
        Directory.CreateDirectory(defaultDir);
        var defaultFileName = BuildTimestampedCoreFileName();

        var dlg = new SaveFileDialog
        {
            Title            = "UA: Зберегти core.lvl — за потреби оберіть іншу теку (типово: output\\...\\{Гра}, з міткою часу) / " +
                                "EN: Save core.lvl — change the folder if needed (default: output\\...\\{Game}, timestamped)",
            Filter           = "LVL files (*.lvl)|*.lvl",
            InitialDirectory = defaultDir,
            FileName         = defaultFileName
        };
        if (dlg.ShowDialog() != true) return;

        SetBusy(true, "UA: Збереження... / EN: Saving...");
        GridEntries.CommitEdit(DataGridEditingUnit.Row, true);

        // UA: Якщо бодай один введений вручну переклад не має куди
        //     записатись — це ВТРАТА РОБОТИ, і мовчати про неї не можна
        //     (саме таке мовчання здатне непомітно викинути десятки вручну
        //     введених рядків — наприклад, усі 22 рядки аддону Tat3 — без
        //     жодного повідомлення).
        //     Після переходу на "структурний майстер = оригінал" це стало
        //     майже недосяжним станом — тим більше про нього треба кричати,
        //     якщо він усе ж настав.
        // EN: If even one hand-typed translation has nowhere to be written,
        //     that is LOST WORK and must not be silent (this exact silence
        //     can silently discard dozens of hand-typed rows unnoticed — for
        //     example, all 22 Tat3 add-on strings — with no message at all).
        //     After moving to "structural master =
        //     the original" this state became nearly unreachable — all the
        //     more reason to shout if it still happens.
        var droppedEdits = ApplyRowEditsToService();
        if (droppedEdits > 0)
        {
            var proceedAnyway = System.Windows.MessageBox.Show(
                $"UA: {droppedEdits} перекладів НЕ мають відповідних рядків у робочому файлі й будуть " +
                $"ВТРАЧЕНІ при збереженні.\nЦе означає, що таблиця побудована з іншого документа, ніж робочий файл.\n\n" +
                $"EN: {droppedEdits} translations have no matching strings in the working file and WILL BE LOST " +
                $"on save.\nThis means the grid was built from a different document than the working file.\n\n" +
                "UA: Все одно зберегти? / EN: Save anyway?",
                "UA: Частина перекладу буде втрачена / EN: Part of the translation will be lost",
                MessageBoxButton.YesNo, MessageBoxImage.Error);
            if (proceedAnyway != MessageBoxResult.Yes) { SetBusy(false); return; }
        }

        // UA: Попередження про кирилицю — ОДНАКОВО для BF1 і BF2 (обидві
        //     гри зберігають текст через один і той самий бінарний
        //     Locl/UTF-16LE механізм — емпірично підтверджено, тож ризик
        //     "обрізання в Latin1" однаковий; давнє припущення "лише BF1"
        //     ґрунтувалось на хибній тезі, що BF1 нібито зберігає текст
        //     як Latin1). Умова — ЛИШЕ
        //     HasCyrillicCodeTable: якщо файл-супутник
        //     cyrillic-code-table.json не знайдено при відкритті, шрифти
        //     цього core.lvl НЕ пройшли через GenerateLocalizedCoreCommand
        //     і фізично не мають кириличних гліфів (байти запишуться
        //     коректно як UTF-16LE, але гра їх намалює як "нема гліфа").
        //     Якщо кодек підключено — SaveAsync сам підмінить кожну
        //     кириличну літеру на її байт-код перед записом
        //     (LvlLocalizationService.BuildGameEncodedClone), тож
        //     попередження було б хибним.
        // EN: Cyrillic warning — SAME for BF1 and BF2 (both games store
        //     text via the same binary Locl/UTF-16LE mechanism —
        //     empirically confirmed, so the "Latin1 truncation" risk is
        //     identical; the old "BF1-only" assumption rested on the
        //     false claim that BF1 stores text as Latin1). Condition —
        //     ONLY HasCyrillicCodeTable: if no
        //     cyrillic-code-table.json sidecar was found on open, this
        //     core.lvl's fonts did NOT go through
        //     GenerateLocalizedCoreCommand and physically have no Cyrillic
        //     glyphs (bytes will write correctly as UTF-16LE, but the game
        //     will render them as "no glyph"). If the codec IS wired up —
        //     SaveAsync will substitute each Cyrillic letter for its
        //     byte-code before writing
        //     (LvlLocalizationService.BuildGameEncodedClone), so the
        //     warning would be false.
        // UA: Попередження показуємо ЛИШЕ якщо шрифт справді не має чим
        //     намалювати кирилицю. Два валідні способи, що вона намалюється:
        //       - HasCyrillicCodeTable — ЗАСТАРІЛИЙ донорський підхід
        //         (кирилиця як перепризначені байт-коди + файл-таблиця);
        //       - FontHasCyrillicGlyphs() — НОВИЙ підхід БЕЗ донорів: гліфи
        //         під СВОЇМИ Unicode-кодами прямо в core.lvl (без таблиці).
        //     Попередження спрацьовує лише коли НЕМАЄ ЖОДНОГО з двох
        //     способів — перевірка ЛИШЕ HasCyrillicCodeTable була б
        //     неповною: коректний no-donor білд (лише
        //     FontHasCyrillicGlyphs) дав би ХИБНЕ попередження.
        // EN: Show the warning ONLY if the font genuinely has no way to draw
        //     Cyrillic. Two valid ways it will render:
        //       - HasCyrillicCodeTable — the LEGACY donor approach (Cyrillic
        //         as remapped byte-codes + a sidecar table);
        //       - FontHasCyrillicGlyphs() — the NEW no-donor approach: glyphs
        //         at their REAL Unicode codes right in core.lvl (no table).
        //     The warning fires only when NEITHER way is present —
        //     checking HasCyrillicCodeTable ALONE would be incomplete: a
        //     correct no-donor build (FontHasCyrillicGlyphs only) would
        //     raise a FALSE warning.
        // UA: Гліфи перевіряються в _serviceSource
        //     (ОРИГІНАЛ), бо саме ЙОГО шрифти тепер їдуть у збережений
        //     файл (AdoptFontsFrom нижче). Перевіряти тут робочий файл
        //     стало б прямою брехнею: він міг бути старим, без гліфів, —
        //     і навпаки, попередження спрацювало б на файлі, у якому
        //     гліфи насправді будуть.
        // EN: Glyphs are checked on _serviceSource
        //     (the ORIGINAL), because ITS fonts are what now ship into the
        //     saved file (AdoptFontsFrom below). Checking the working file
        //     here would be an outright lie: it could be an old one with
        //     no glyphs — and conversely the warning would fire on a file
        //     that will in fact have them.
        // UA: ЗАХИСТ ВІД ЗМІШУВАННЯ ДВОХ РІЗНИХ ДОКУМЕНТІВ.
        //
        //     РИЗИК: якщо "Оригінал" відкрито з core.lvl АДДОНУ
        //     (GameData\AddOn\Tat3\...), а "Робочий файл" — з core.lvl
        //     БАЗОВОЇ гри, SaveAsync серіалізує дерево РОБОЧОГО файлу
        //     цілком, тож у теку аддону збереглася б насправді БАЗОВА
        //     гра: 2457 записів замість 2466, ЖОДНОГО з 22 унікальних
        //     рядків Tat3 (KEEPERS CHAMBERS, GAMORREANS, CP1-CP7...) і —
        //     найгірше — повний комплект із 6 шрифтів, яких у файлі
        //     аддону не повинно бути ВЗАГАЛІ (vanilla Tat3 core.lvl:
        //     0 FBOD / 0 FTEX, 1 МБ проти 4,9 МБ у робочому файлі).
        //     Наслідки в грі: мапа Джабби лишається англійською (її
        //     рядків у файлі просто немає) і всі текстові поля на ній
        //     стають порожні (другий комплект gamefont_* поверх уже
        //     завантаженого базового).
        //
        //     AdoptFontsFrom тут НЕ рятує: він переносить шрифти за
        //     збігом імен, а в джерела-аддона шрифтів 0 → переносити
        //     нічого, і шрифти робочого (базового) файлу лишаються
        //     недоторканими.
        //
        //     Перевіряються ДВА незалежні сигнали:
        //       1) множини хешів рядків не збігаються — це РІЗНІ
        //          документи (хеш прив'язаний до ключа, не до перекладу,
        //          тож переклад на порівняння не впливає);
        //       2) оригінал не несе шрифтів, а робочий несе — тобто
        //          зараз буде записано шрифти у файл аддону.
        // EN: GUARD AGAINST MIXING TWO DIFFERENT DOCUMENTS.
        //
        //     RISK: if "Original" is opened from the ADD-ON's core.lvl
        //     (GameData\AddOn\Tat3\...) while "Working file" is opened
        //     from the BASE game's core.lvl, SaveAsync serializes the
        //     WORKING file's whole tree, so what would get saved into
        //     the add-on's folder is actually the BASE game: 2457
        //     entries instead of 2466, NONE of Tat3's 22 unique strings
        //     (KEEPERS CHAMBERS, GAMORREANS, CP1-CP7...) and — worst of
        //     all — a full set of 6 fonts, which an add-on file must NOT
        //     contain AT ALL (vanilla Tat3 core.lvl: 0 FBOD / 0 FTEX,
        //     1 MB against a working file's 4.9 MB). In-game result:
        //     Jabba's map stays English (its strings simply aren't in
        //     the file) and every text field on it goes blank (a second
        //     gamefont_* set on top of the already-loaded base one).
        //
        //     AdoptFontsFrom does not prevent this: it transplants fonts
        //     by name match, and an add-on source has 0 fonts → nothing
        //     to transplant, so the working (base) file's fonts stay
        //     put.
        //
        //     TWO independent signals are checked:
        //       1) the string-hash sets differ — these are DIFFERENT
        //          documents (the hash is tied to the key, not the
        //          translation, so translating doesn't affect the compare);
        //       2) the original carries no fonts while the working file
        //          does — i.e. fonts are about to be written into an
        //          add-on.
        var mismatchReport = BuildSourceTargetMismatchReport(target);
        if (mismatchReport is not null)
        {
            var proceed = System.Windows.MessageBox.Show(
                mismatchReport,
                "UA: Оригінал і робочий файл не збігаються / EN: Original and working file do not match",
                MessageBoxButton.YesNo, MessageBoxImage.Error);
            if (proceed != MessageBoxResult.Yes) { SetBusy(false); return; }
        }

        if (!target.HasCyrillicCodeTable && !_serviceSource.FontHasCyrillicGlyphs())
        {
            var hasCyrillic = _allRows.Any(r =>
                r.Translation != null && ValidationService.ContainsCyrillic(r.Translation));
            if (hasCyrillic)
            {
                var warn = System.Windows.MessageBox.Show(
    """
    UA: Увага! Шрифти цього core.lvl не мають кириличних гліфів (файл-
    супутник cyrillic-code-table.json не знайдено поруч). Байти
    перекладу запишуться коректно, але гра НЕ намалює ці літери —
    покаже порожнє місце або "квадратик". Щоб кирилиця відображалась,
    відкрий core.lvl, згенерований через Diagnostic → 7 → 1.
    Все одно зберегти?

    EN: Warning! This core.lvl's fonts have no Cyrillic glyphs (no
    cyrillic-code-table.json sidecar found nearby). The translation
    bytes will be written correctly, but the game will NOT render
    these letters — it will show blank space or a "tofu" box. For
    Cyrillic to display, open a core.lvl generated via
    Diagnostic → 7 → 1.
    Save anyway?
    """,
    "UA: Увага / EN: Warning",
    MessageBoxButton.YesNo, MessageBoxImage.Warning);
                if (warn != MessageBoxResult.Yes) { SetBusy(false); return; }
            }
        }

        try
        {
            // UA: ШРИФТИ — ЗАВЖДИ З ОРИГІНАЛУ, текст — з робочого файлу.
            //     ПРИЧИНА: SaveAsync серіалізує дерево РОБОЧОГО файлу
            //     цілком, тож без цього шрифти в гру їхали б саме з
            //     нього. А "Відкрити робочий" за замовчуванням веде в
            //     output\ — теку раніше збережених файлів, — тому
            //     природний шлях кліків мовчки підсовує СТАРІ шрифти, і
            //     кожна перегенерація шрифтів просто відкидалась би при
            //     збереженні.
            //
            //     Робочий файл — носій ЛИШЕ перекладу, як і має бути;
            //     шрифти беруться з _serviceSource щоразу. Ідемпотентно:
            //     якщо шрифти вже ті самі, пересадка нічого не змінює.
            // EN: FONTS — ALWAYS FROM THE ORIGINAL, text from the working
            //     file. REASON: SaveAsync serializes the WORKING file's
            //     whole tree, so without this the fonts shipped to the
            //     game would come from there. And "Open working" defaults
            //     to output\ — the folder of previously saved files — so
            //     the natural click-path silently supplies OLD fonts, and
            //     every font regeneration would simply be discarded on
            //     save.
            //
            //     The working file carries ONLY the translation, as it
            //     should; fonts are taken from _serviceSource every time.
            //     Idempotent: if the fonts already match, the transplant
            //     changes nothing.
            var adoptedFonts = target.AdoptFontsFrom(_serviceSource);

            await target.SaveAsync(dlg.FileName, _targetLang);
            _autoSave.ClearAutosaves();

            // UA: Парний РОБОЧИЙ CSV (переклад + статус вичитки) пишеться
            //     одразу поруч зі збереженням core.lvl — за зразком
            //     SWH.LocEditor (там .csv.z для гри й review\...tsv
            //     завжди пишуться разом, з тим самим ім'ям-міткою часу).
            //     ПРИНЦИПОВО: пишеться в review\, НЕ в output\ — сам
            //     core.lvl (те, що йде в гру) НІКОЛИ не містить
            //     ReviewStatus, лише ЦЕЙ окремий файл. Ім'я узгоджене з
            //     core.lvl (та сама мітка часу) — видно одразу, який CSV
            //     якому збереженню відповідає.
            // EN: The paired WORKING CSV (translation + review status) is
            //     written immediately alongside the core.lvl save —
            //     mirroring SWH.LocEditor (there the game's .csv.z and
            //     review\...tsv are always written together, sharing the
            //     same timestamped name). CRUCIAL: written into review\,
            //     NOT output\ — core.lvl itself (what goes into the game)
            //     NEVER contains ReviewStatus, only this separate file.
            //     Its name matches core.lvl's (same timestamp) — so it's
            //     immediately obvious which CSV belongs to which save.
            // UA: Той самий "не в корінь, а в ігрову підтеку" принцип, що й
            //     для "output" (і тепер "original") — інакше робочий CSV
            //     від BF1- і BF2-сеансу міг би плутатись/затиратись у
            //     плоскому review\ (навіть попри мітку часу — двома різними
            //     сеансами теоретично можна зберегти в ту саму хвилину).
            // EN: The same "into the game's subfolder, not the root"
            //     principle as "output" (and now "original") — otherwise
            //     the working CSV from a BF1 vs BF2 session could clash in
            //     a flat review\ (even with a timestamp — two different
            //     sessions could in theory save within the same minute).
            var reviewDir = Path.Combine(GetReviewRootPath(), GetRelativeGamePath(_serviceSource.GameVersion, _sourcePath));
            Directory.CreateDirectory(reviewDir);
            var workingCsvName = Path.GetFileNameWithoutExtension(dlg.FileName) + ".csv";
            var workingCsvPath = Path.Combine(reviewDir, workingCsvName);
            var csvContent = BuildCsvContent();
            if (csvContent is not null)
                await File.WriteAllTextAsync(workingCsvPath, csvContent, Encoding.UTF8);

            var reviewRelativeNote = $"review\\{GetRelativeGamePath(_serviceSource.GameVersion, _sourcePath)}\\{workingCsvName}";
            // UA: Кількість пересаджених шрифтів показуємо ЯВНО — саме
            //     мовчання цього кроку й приховувало те, що в гру їхали
            //     старі шрифти.
            // EN: The transplanted-font count is surfaced EXPLICITLY — it
            //     was precisely the silence of this step that hid old
            //     fonts shipping to the game.
            SetStatus($"UA: Збережено [{_targetLang}]: {dlg.FileName} (шрифтів з оригіналу: {adoptedFonts}) + робочий CSV: {reviewRelativeNote} / " +
                       $"EN: Saved [{_targetLang}]: {dlg.FileName} (fonts from original: {adoptedFonts}) + working CSV: {reviewRelativeNote}");
            SimpleLogger.Info($"Saved [{_targetLang}]: {dlg.FileName}; fonts adopted from original: {adoptedFonts}; paired working CSV: {workingCsvPath}");
        }
        catch (Exception ex)
        {
            SimpleLogger.Error("Save failed", ex);
            System.Windows.MessageBox.Show(
                $"UA: Помилка збереження:\n{ex.Message}\n\nEN: Save error:\n{ex.Message}",
                "Error", MessageBoxButton.OK, MessageBoxImage.Error);
        }
        finally { SetBusy(false); }
    }

    // =========================================================================
    // UA: ЕКСПОРТ / ІМПОРТ CSV / EN: EXPORT / IMPORT CSV
    // =========================================================================

    // UA: Експорт бере дані НАПРЯМУ з _allRows (стан GUI: Original із
    //     джерела + Translation з поточних правок/робочого файлу), а не
    //     через LvlLocalizationService.ExportCsvAsync — так CSV завжди
    //     відображає РЕАЛЬНИЙ поточний стан таблиці, незалежно від того,
    //     чи є взагалі відкритий робочий файл. Той самий будівельник
    //     (BuildCsvContent), що й в автозбереженні — без дублювання логіки.
    // EN: Export reads data DIRECTLY from _allRows (GUI state: Original
    //     from the source + Translation from current edits/working file),
    //     not via LvlLocalizationService.ExportCsvAsync — so the CSV
    //     always reflects the ACTUAL current grid state, regardless of
    //     whether a working file is even open. Same builder
    //     (BuildCsvContent) as autosave — no duplicated logic.
    private async void BtnExportCsv_Click(object sender, RoutedEventArgs e)
    {
        var dlg = new SaveFileDialog
        {
            Title    = "UA: Експорт CSV / EN: Export CSV",
            Filter   = "CSV (*.csv)|*.csv",
            FileName = $"bf1_{_sourceLang}.csv"
        };
        if (dlg.ShowDialog() != true) return;

        try
        {
            GridEntries.CommitEdit(DataGridEditingUnit.Row, true);
            var csv = BuildCsvContent();
            if (csv is null)
            {
                SetStatus("UA: Немає даних для експорту / EN: No data to export");
                return;
            }

            await File.WriteAllTextAsync(dlg.FileName, csv, Encoding.UTF8);
            SetStatus($"UA: Експортовано: {dlg.FileName} / EN: Exported: {dlg.FileName}");
        }
        catch (Exception ex)
        {
            SimpleLogger.Error("Export failed", ex);
            System.Windows.MessageBox.Show(ex.Message, "Error",
                MessageBoxButton.OK, MessageBoxImage.Error);
        }
    }

    private async void BtnImportCsv_Click(object sender, RoutedEventArgs e)
    {
        if (_serviceTarget is not { } target)
        {
            System.Windows.MessageBox.Show(
                "UA: Спочатку створіть або відкрийте робочий файл ('Новий робочий' / 'Робочий файл').\n\n" +
                "EN: First create or open a working file ('New working' / 'Working file').",
                "UA: Немає робочого файлу / EN: No working file",
                MessageBoxButton.OK, MessageBoxImage.Warning);
            return;
        }

        var dlg = new OpenFileDialog
        {
            Title  = "UA: Імпорт CSV / EN: Import CSV",
            Filter = "CSV (*.csv)|*.csv"
        };
        if (dlg.ShowDialog() != true) return;

        SetBusy(true, "UA: Імпорт... / EN: Importing...");
        try
        {
            await target.ImportCsvAsync(_targetLang, dlg.FileName);

            // UA: Статус вичитки не проходить крізь ImportCsvAsync
            //     (LvlLocalizationService/LocalizationEntry про нього
            //     нічого не знає — свідомо, див. коментар над
            //     LocalizationCsvRow.ReviewStatus) — тому 5-ту колонку
            //     читаємо тут ОКРЕМО, напряму з того самого файлу, і
            //     зливаємо в _reviewStatuses ПЕРЕД RefreshGrid, щоб
            //     новозбудовані рядки одразу підхопили позначки. На
            //     відміну від SnapshotReviewStatuses — тут свідомо
            //     ПЕРЕЗАПИСУЄМО (файл — джерело істини при імпорті, так
            //     само як і для перекладу).
            // EN: Review status doesn't flow through ImportCsvAsync
            //     (LvlLocalizationService/LocalizationEntry deliberately
            //     knows nothing about it — see the comment above
            //     LocalizationCsvRow.ReviewStatus) — so the 5th column is
            //     read here SEPARATELY, directly from the same file, and
            //     merged into _reviewStatuses BEFORE RefreshGrid, so the
            //     freshly built rows pick the marks up immediately. Unlike
            //     SnapshotReviewStatuses — this deliberately OVERWRITES
            //     (the file is the source of truth on import, same as for
            //     the translation).
            var importedRows = await LocalizationCsvIo.ReadAsync(dlg.FileName);
            var mergedReviewCount = 0;
            foreach (var importedRow in importedRows)
            {
                if (string.IsNullOrWhiteSpace(importedRow.ReviewStatus)) continue;
                _reviewStatuses[(importedRow.Hash, importedRow.Ordinal)] = importedRow.ReviewStatus;
                mergedReviewCount++;
            }

            RefreshGrid();
            SetStatus($"UA: Імпортовано в [{_targetLang}] (статусів вичитки: {mergedReviewCount}) / " +
                       $"EN: Imported into [{_targetLang}] (review statuses: {mergedReviewCount})");
        }
        catch (Exception ex)
        {
            SimpleLogger.Error("Import failed", ex);
            System.Windows.MessageBox.Show(ex.Message, "Error",
                MessageBoxButton.OK, MessageBoxImage.Error);
        }
        finally { SetBusy(false); }
    }

    // =========================================================================
    // UA: ПОРІВНЯННЯ ФАЙЛІВ — окреме, незалежне від поточного завантаженого
    //     файлу вікно (своя пара LvlLocalizationService всередині
    //     CompareWindow). Не модальне — можна лишити відкритим і
    //     продовжувати редагувати переклад у головному вікні.
    // EN: FILE COMPARISON — a separate window, independent of the currently
    //     loaded file (its own pair of LvlLocalizationService instances
    //     inside CompareWindow). Non-modal — can stay open while editing
    //     translation in the main window.
    // =========================================================================
    private void BtnCompare_Click(object sender, RoutedEventArgs e)
    {
        var window = new CompareWindow { Owner = this };
        window.Show();
    }

    // =========================================================================
    // UA: ПЕРЕНЕСЕННЯ ПЕРЕКЛАДУ З ІНШОЇ ГРИ / EN: CROSS-GAME TRANSLATION TRANSFER
    //     Зіставлення — за текстом англійського оригіналу (не за Hash, який у
    //     BF1 і BF2 належить різним просторам ключів — див. заголовок
    //     CrossGameTranslationTransfer.cs). Донор — файл ІНШОЇ (або тієї ж)
    //     гри, обраний у діалозі; ЦІЛЬ — уже відкритий у сесії _serviceTarget
    //     (кнопка активна лише коли він є).
    //
    //     ЗАХИСТ — ЗА ВИЧИТКОЮ, НЕ ЗА НАЯВНІСТЮ ПЕРЕКЛАДУ: після пакетного
    //     перекладу через Gemini Translation має практично кожен рядок, тож
    //     критерій "не чіпати вже перекладене" не переносив би нічого.
    //     Натомість не перезаписуються лише ВИЧИТАНІ записи (непорожній
    //     ReviewStatus у _reviewStatuses) — решта, включно з сирим
    //     Gemini-перекладом, вважається кандидатом на заміну збігом з
    //     донора.
    // EN: Matching is done on the English original TEXT (not Hash, which
    //     belongs to different key namespaces in BF1 vs BF2 — see the header
    //     of CrossGameTranslationTransfer.cs). The donor is a file from
    //     ANOTHER (or the same) game, picked in a dialog; the TARGET is the
    //     already-open _serviceTarget (the button is only enabled when it
    //     exists).
    //
    //     PROTECTION IS BY REVIEW STATUS, NOT BY PRESENCE OF A TRANSLATION:
    //     after a batch translation pass through Gemini, practically every
    //     row already has a Translation, so a "don't touch already
    //     translated" rule would transfer nothing. Instead, only REVIEWED
    //     entries (a non-empty ReviewStatus in _reviewStatuses) are left
    //     alone — everything else, including raw Gemini output, is a
    //     candidate for replacement by a donor match.
    // =========================================================================
    private async void BtnTransferFromOtherGame_Click(object sender, RoutedEventArgs e)
    {
        if (_serviceTarget is null)
        {
            System.Windows.MessageBox.Show(
                "UA: Спочатку відкрийте робочий файл / EN: Open the working file first",
                "Error", MessageBoxButton.OK, MessageBoxImage.Warning);
            return;
        }

        var dlg = new OpenFileDialog
        {
            Title            = "UA: Обрати файл-донор перекладу (інша гра — напр. BF1 для BF2) / " +
                                "EN: Choose the donor translation file (another game — e.g. BF1 for BF2)",
            Filter           = "LVL/CSV files (*.lvl;*.csv)|*.lvl;*.csv|LVL files (*.lvl)|*.lvl|CSV files (*.csv)|*.csv|All files (*.*)|*.*",
            InitialDirectory = GetOutputRootPath()
        };
        if (dlg.ShowDialog() != true) return;

        SetBusy(true, "UA: Аналіз донора... / EN: Analyzing donor...");
        SimpleLogger.Info($"Cross-game transfer: donor = {dlg.FileName}");

        try
        {
            IReadOnlyDictionary<string, IReadOnlyList<string>> donorIndex;

            if (dlg.FileName.EndsWith(".csv", StringComparison.OrdinalIgnoreCase))
            {
                var rows = await LocalizationCsvIo.ReadAsync(dlg.FileName);
                donorIndex = CrossGameTranslationTransfer.BuildIndex(rows);
            }
            else
            {
                var donor = new LvlLocalizationService();
                await donor.LoadAsync(dlg.FileName);

                var donorSourceFile     = donor.GetLanguageFile(_sourceLang);
                var donorTranslatedFile = donor.GetLanguageFile(_targetLang);
                if (donorSourceFile is null || donorTranslatedFile is null)
                {
                    System.Windows.MessageBox.Show(
                        $"UA: У донорі немає мов «{_sourceLang}»/«{_targetLang}» / " +
                        $"EN: The donor has no «{_sourceLang}»/«{_targetLang}» languages",
                        "Error", MessageBoxButton.OK, MessageBoxImage.Warning);
                    return;
                }

                donorIndex = CrossGameTranslationTransfer.BuildIndex(donorSourceFile, donorTranslatedFile);
            }

            var plan = CrossGameTranslationTransfer.BuildPlan(donorIndex);

            IReadOnlyDictionary<string, string> conflictResolutions = new Dictionary<string, string>();
            if (plan.Conflicts.Count > 0)
            {
                var conflictWindow = new TranslationConflictWindow(plan.Conflicts) { Owner = this };
                var confirmed = conflictWindow.ShowDialog();
                if (confirmed != true)
                {
                    SetStatus("UA: Перенесення скасовано / EN: Transfer cancelled");
                    return;
                }
                conflictResolutions = conflictWindow.Resolutions;
            }

            var targetSourceFile     = _serviceTarget.GetLanguageFile(_sourceLang);
            var targetTranslatedFile = _serviceTarget.GetLanguageFile(_targetLang);
            if (targetSourceFile is null || targetTranslatedFile is null)
            {
                System.Windows.MessageBox.Show(
                    $"UA: У поточному файлі немає мов «{_sourceLang}»/«{_targetLang}» / " +
                    $"EN: The current file has no «{_sourceLang}»/«{_targetLang}» languages",
                    "Error", MessageBoxButton.OK, MessageBoxImage.Warning);
                return;
            }

            // UA: Захист від перезапису — за ПОЗНАЧКОЮ ВИЧИТКИ (ReviewStatus), а
            //     не за фактом наявності перекладу: після пакетного перекладу
            //     через Gemini Translation має практично кожен рядок, тож
            //     критерій "не чіпати перекладене" не переносив би нічого.
            //     Вичитаним вважається запис із непорожнім ReviewStatus у
            //     _reviewStatuses (той самий словник, що керує колонкою
            //     "Вичитка" в гріді) — Core про ReviewStatus нічого не знає
            //     (це GUI/CSV-метадані), тому критерій передається сюди як
            //     делегат.
            // EN: Overwrite protection is based on the REVIEW MARK
            //     (ReviewStatus), not on whether a translation is present:
            //     after a batch translation pass through Gemini, practically
            //     every row already has a Translation, so a "don't touch
            //     translated rows" rule would transfer nothing. An entry
            //     counts as reviewed when it has a non-empty ReviewStatus in
            //     _reviewStatuses (the same dictionary that drives the
            //     "Review" column in the grid) — Core knows nothing about
            //     ReviewStatus (GUI/CSV metadata only), so the criterion is
            //     passed in here as a delegate.
            var result = CrossGameTranslationTransfer.Apply(
                targetSourceFile, targetTranslatedFile, plan.AutoFill, conflictResolutions,
                isProtected: (hash, ordinal) =>
                    _reviewStatuses.TryGetValue((hash, ordinal), out var rs) && !string.IsNullOrWhiteSpace(rs));

            SnapshotReviewStatuses();
            RefreshGrid();

            SetStatus(
                $"UA: Перенесено {result.AutoFilled} однозначних + {result.ConflictResolved} вирішених конфліктів " +
                $"(пропущено {result.ProtectedSkipped} уже вичитаних) / " +
                $"EN: Transferred {result.AutoFilled} unambiguous + {result.ConflictResolved} resolved conflicts " +
                $"(skipped {result.ProtectedSkipped} already reviewed)");
            SimpleLogger.Info(
                $"Cross-game transfer done: autoFilled={result.AutoFilled}, conflictResolved={result.ConflictResolved}, " +
                $"protectedSkipped={result.ProtectedSkipped}");
        }
        catch (Exception ex)
        {
            SimpleLogger.Error("Cross-game transfer failed", ex);
            System.Windows.MessageBox.Show(
                $"UA: Помилка перенесення перекладу:\n{ex.Message}\n\nEN: Translation transfer error:\n{ex.Message}",
                "Error", MessageBoxButton.OK, MessageBoxImage.Error);
            SetStatus("UA: Помилка перенесення перекладу / EN: Translation transfer error");
        }
        finally { SetBusy(false); }
    }

    // =========================================================================
    // UA: МОВНІ СЕЛЕКТОРИ / EN: LANGUAGE SELECTORS
    // =========================================================================

    private void CmbSourceLang_SelectionChanged(object sender, SelectionChangedEventArgs e)
    {
        if (CmbSourceLang.SelectedItem is string lang)
        { _sourceLang = lang; SnapshotReviewStatuses(); RefreshGrid(); }
    }

    private void CmbTargetLang_SelectionChanged(object sender, SelectionChangedEventArgs e)
    {
        if (CmbTargetLang.SelectedItem is string lang)
        { _targetLang = lang; SnapshotReviewStatuses(); RefreshGrid(); }
    }

    // =========================================================================
    // UA: ФІЛЬТРИ / EN: FILTERS
    // =========================================================================

    private void TxtSearch_TextChanged(object sender, TextChangedEventArgs e)
    {
        RefreshView();
        UpdateProgressDisplay();
    }

    private void FilterChanged(object sender, RoutedEventArgs e)
    {
        RefreshView();
        UpdateProgressDisplay();
    }

    // -------------------------------------------------------------------------
    // UA: ⚙ Поріг довжини перекладу (див. EntryRow.LengthRatioThreshold/
    //     LengthMarginThreshold — там і калібрування на реальних кейсах).
    //     Попап відкривається/закривається кнопкою; поля показують ПОТОЧНІ
    //     (статичні) значення при відкритті.
    // EN: ⚙ Translation length threshold (see EntryRow.LengthRatioThreshold/
    //     LengthMarginThreshold — calibration against real cases is there).
    //     The popup opens/closes via the button; fields show the CURRENT
    //     (static) values when opened.
    // -------------------------------------------------------------------------
    private void BtnLengthSettings_Click(object sender, RoutedEventArgs e)
    {
        if (PopupLengthSettings.IsOpen)
        {
            PopupLengthSettings.IsOpen = false;
            return;
        }

        TxtLengthRatio.Text  = EntryRow.LengthRatioThreshold.ToString(System.Globalization.CultureInfo.InvariantCulture);
        TxtLengthMargin.Text = EntryRow.LengthMarginThreshold.ToString(System.Globalization.CultureInfo.InvariantCulture);
        PopupLengthSettings.IsOpen = true;
    }

    // UA: Живе застосування — щойно поле валідне, одразу перерахувати
    //     Validate() для ВСІХ рядків (дешево, звичайний CSV на кілька тисяч
    //     рядків) і оновити фільтр/лічильники, щоб зміна порогу була видна
    //     миттєво, без окремої кнопки "Застосувати".
    // EN: Live apply — as soon as the field is valid, immediately recompute
    //     Validate() for ALL rows (cheap, a typical CSV is a few thousand
    //     rows) and refresh the filter/counters, so a threshold change is
    //     visible instantly, no separate "Apply" button.
    private void LengthThreshold_Changed(object sender, TextChangedEventArgs e)
    {
        if (TxtLengthPreview is null) return; // UA: guard до InitializeComponent / EN: guard before InitializeComponent

        var ratioOk  = double.TryParse(TxtLengthRatio?.Text,  System.Globalization.NumberStyles.Float, System.Globalization.CultureInfo.InvariantCulture, out var ratio) && ratio > 0;
        var marginOk = int.TryParse(TxtLengthMargin?.Text, out var margin) && margin >= 0;

        if (!ratioOk || !marginOk)
        {
            TxtLengthPreview.Text = "UA: Некоректне значення / EN: Invalid value";
            return;
        }

        EntryRow.LengthRatioThreshold  = ratio;
        EntryRow.LengthMarginThreshold = margin;

        TxtLengthPreview.Text =
            $"UA: Приклад: оригінал 12 симв. → поріг {(int)Math.Round(12 * ratio + margin)} симв. / " +
            $"EN: Example: 12-char original → {(int)Math.Round(12 * ratio + margin)}-char threshold";

        if (_allRows is null) return;
        foreach (var row in _allRows) row.Validate();
        RefreshView();
        UpdateProgressDisplay();
    }

    private bool FilterRow(object obj)
    {
        if (obj is not EntryRow row) return false;

        if (RbUntranslated.IsChecked == true && (row.IsTranslated || row.IsTechnical)) return false;
        // UA: "Перекладено" ховає технічні — три статуси взаємовиключні (див. IsTranslated).
        // EN: "Translated" hides technical — the three statuses are mutually exclusive (see IsTranslated).
        if (RbTranslated.IsChecked   == true && (!row.IsTranslated || row.IsTechnical)) return false;
        if (RbTechnical.IsChecked    == true && !row.IsTechnical)    return false;
        if (RbInvalid.IsChecked      == true && row.IsValid)         return false;

        var search = TxtSearch.Text?.Trim();
        if (string.IsNullOrEmpty(search)) return true;

        return row.Original?.Contains(search,    StringComparison.OrdinalIgnoreCase) == true
            || row.Translation?.Contains(search, StringComparison.OrdinalIgnoreCase) == true
            || row.HashDisplay.Contains(search,  StringComparison.OrdinalIgnoreCase);
    }

    // UA: Обгортка над _view.Refresh() — ЗАВЖДИ спочатку фіксує
    //     незавершене редагування комірки/рядка. Без цього:
    //     System.InvalidOperationException: 'Refresh' is not allowed
    //     during an AddNew or EditItem transaction — виникає щоразу, коли
    //     Refresh() викликається, поки грід ще технічно "в режимі
    //     редагування" (типово: правий клік по комірці відкриває
    //     контекстне меню, не завершивши edit-транзакцію попереднього
    //     кліку). Використовуй ЦЕЙ метод замість "_view?.Refresh()" будь-де в класі.
    // EN: Wrapper over _view.Refresh() — ALWAYS commits any pending
    //     cell/row edit first. Without this:
    //     System.InvalidOperationException: 'Refresh' is not allowed
    //     during an AddNew or EditItem transaction — happens whenever
    //     Refresh() is called while the grid is still technically "in
    //     edit mode" (typically: right-clicking a cell opens the context
    //     menu without having finished the previous click's edit
    //     transaction). Use THIS method instead of "_view?.Refresh()"
    //     anywhere in this class.
    private void RefreshView()
    {
        // UA: Guard — RbAll IsChecked="True" в XAML викликає Checked→
        //     FilterChanged→RefreshView() ЩЕ ПІД ЧАС InitializeComponent(),
        //     до того як поле GridEntries (оголошене нижче в дереві XAML)
        //     встигає прив'язатись — той самий відомий гачок, що вже
        //     вирішено в UpdateProgressDisplay і CompareWindow.UpdateSummary.
        // EN: Guard — RbAll IsChecked="True" in XAML fires Checked→
        //     FilterChanged→RefreshView() DURING InitializeComponent(),
        //     before the GridEntries field (declared later in the XAML
        //     tree) gets wired — the same known gotcha already solved in
        //     UpdateProgressDisplay and CompareWindow.UpdateSummary.
        if (GridEntries is null) return;

        GridEntries.CommitEdit(DataGridEditingUnit.Cell, true);
        GridEntries.CommitEdit(DataGridEditingUnit.Row, true);

        // UA: Переклад/техн.-позначка щойно могли змінитись — дублі-групи
        //     й "розбіжний переклад" рахуємо тут же, на кожен виклик
        //     RefreshView (усі місця, де рядок редагується: клітинка,
        //     контекстне меню, авто-техн.), а не лише при RefreshGrid.
        // EN: Translation/technical flag may have just changed — recompute
        //     duplicate groups and "inconsistent translation" right here,
        //     on every RefreshView call (every place a row gets edited:
        //     cell, context menu, auto-technical), not only on RefreshGrid.
        RecomputeDuplicates();

        _view?.Refresh();
    }

    // UA: Перераховує групи дублікатів — рядки з ІДЕНТИЧНИМ оригіналом
    //     (точне порівняння, ordinal), за зразком SWH.LocEditor
    //     (CsvLocDocument.RecomputeDuplicates, див. коментар над
    //     EntryRow.DuplicateGroupSize). Технічні рядки НЕ беруть участі в
    //     групуванні — їхній "оригінал" часто коди/плейсхолдери, що
    //     природно повторюються і не є значущим текстовим дублем.
    // EN: Recomputes duplicate groups — rows with an IDENTICAL original
    //     (exact, ordinal comparison), modeled after SWH.LocEditor
    //     (CsvLocDocument.RecomputeDuplicates, see the comment above
    //     EntryRow.DuplicateGroupSize). Technical rows do NOT participate in
    //     grouping — their "original" is often codes/placeholders that
    //     naturally repeat and aren't a meaningful text duplicate.
    private void RecomputeDuplicates()
    {
        if (_allRows is null) return;

        foreach (var group in _allRows.Where(r => !r.IsTechnical)
                                       .GroupBy(r => r.Original, StringComparer.Ordinal))
        {
            var members = group.ToList();
            var size = members.Count;
            var inconsistent = size > 1 &&
                members.Select(r => r.Translation ?? string.Empty).Distinct().Count() > 1;

            foreach (var row in members)
                row.SetDuplicateInfo(size, inconsistent);
        }

        // UA: Технічні рядки завжди поза групами дублів
        // EN: Technical rows are always outside duplicate groups
        foreach (var row in _allRows.Where(r => r.IsTechnical))
            row.SetDuplicateInfo(1, false);
    }

    // =========================================================================
    // UA: КНОПКИ РЕДАГУВАННЯ / EN: EDIT BUTTONS
    // =========================================================================

    private void BtnCopySourceToTarget_Click(object sender, RoutedEventArgs e)
    {
        if (GridEntries.SelectedItem is EntryRow row)
        {
            row.Translation = row.Original;
            RefreshView();
            UpdateProgressDisplay();
        }
    }

    private void BtnMarkTechnical_Click(object sender, RoutedEventArgs e)
    {
        if (GridEntries.SelectedItem is EntryRow row)
        {
            row.IsTechnical = !row.IsTechnical;
            RefreshView();
            UpdateProgressDisplay();
        }
    }

    private void BtnAutoMarkTechnical_Click(object sender, RoutedEventArgs e)
    {
        var count = 0;
        foreach (var row in _allRows.Where(r => !r.IsTechnical))
        {
            if (TechnicalStringService.IsTechnical(row.Original))
            { row.IsTechnical = true; count++; }
        }
        RefreshView();
        UpdateProgressDisplay();
        SetStatus($"UA: Позначено технічних: {count} / EN: Marked technical: {count}");
    }

    // =========================================================================
    // UA: ТЕМА / ШРИФТ / EN: THEME / FONT
    // =========================================================================

    private void BtnTheme_Click(object sender, RoutedEventArgs e)
    {
        _isDarkTheme = !_isDarkTheme;
        ApplyTheme();
        BtnTheme.Content = _isDarkTheme
            ? "☾ UA: Темна / EN: Dark"
            : "☀ UA: Світла / EN: Light";
    }

    private void ApplyTheme()
    {
        // UA: Замінюємо brush-об'єкти цілком — {DynamicResource} підхопить нові значення.
        //     Стилі DataGrid/ComboBox/TextBox використовують {DynamicResource} тому теж оновляться.
        //     ВАЖЛИВО: саме заміна об'єкта (не зміна Color) спрацьовує з DynamicResource.
        // EN: Replace brush objects entirely — {DynamicResource} will pick up new values.
        //     DataGrid/ComboBox/TextBox styles use {DynamicResource} so they update too.
        //     IMPORTANT: object replacement (not Color change) works with DynamicResource.
        var res = Application.Current.Resources;
        if (_isDarkTheme)
        {
            res["BgWindowBrush"]      = new SolidColorBrush(Color.FromRgb(0x0D, 0x0A, 0x14));
            res["BgPanelBrush"]       = new SolidColorBrush(Color.FromRgb(0x1A, 0x15, 0x25));
            res["BgRowAltBrush"]      = new SolidColorBrush(Color.FromRgb(0x22, 0x1C, 0x30));
            res["TextPrimaryBrush"]   = new SolidColorBrush(Color.FromRgb(0xE8, 0xE0, 0xF0));
            res["TextSecondaryBrush"] = new SolidColorBrush(Color.FromRgb(0x90, 0x80, 0xA8));
            // UA: Темна тема — яскравий бірюзовий, чітко виділяється на темному фоні
            // EN: Dark theme — bright cyan, clearly stands out on dark background
            res["TechnicalBrush"]     = new SolidColorBrush(Color.FromRgb(0x4F, 0xD9, 0xE8));

            // UA: Заливка ЦІЛОГО РЯДКА за статусом (RowStyle, MainWindow.xaml)
            // EN: WHOLE-ROW fill by status (RowStyle, MainWindow.xaml)
            res["RowUntranslatedBg"] = new SolidColorBrush(Color.FromRgb(0x3A, 0x16, 0x20));
            res["RowTranslatedBg"]   = new SolidColorBrush(Color.FromRgb(0x12, 0x3A, 0x22));
            res["RowIssueBg"]        = new SolidColorBrush(Color.FromRgb(0x4A, 0x34, 0x10));
            res["RowDupWarnBg"]      = new SolidColorBrush(Color.FromRgb(0x2E, 0x1A, 0x3E));
            res["RowTechnicalBg"]    = new SolidColorBrush(Color.FromRgb(0x0F, 0x2A, 0x30));
            res["RowModifiedBg"]     = new SolidColorBrush(Color.FromRgb(0x3A, 0x2E, 0x0C));
            res["RowHoverBg"]        = new SolidColorBrush(Color.FromRgb(0x24, 0x1A, 0x30));
        }
        else
        {
            res["BgWindowBrush"]      = new SolidColorBrush(Color.FromRgb(0xF0, 0xEB, 0xF8));
            res["BgPanelBrush"]       = new SolidColorBrush(Color.FromRgb(0xE0, 0xD8, 0xEE));
            res["BgRowAltBrush"]      = new SolidColorBrush(Color.FromRgb(0xD0, 0xC8, 0xE0));
            res["TextPrimaryBrush"]   = new SolidColorBrush(Color.FromRgb(0x1A, 0x0A, 0x2E));
            res["TextSecondaryBrush"] = new SolidColorBrush(Color.FromRgb(0x50, 0x40, 0xA0));
            // UA: Світла тема — насичений темно-бірюзовий, чітко виділяється на світлому фоні
            // EN: Light theme — saturated dark teal, clearly stands out on light background
            res["TechnicalBrush"]     = new SolidColorBrush(Color.FromRgb(0x0A, 0x6E, 0x7D));

            // UA: Заливка ЦІЛОГО РЯДКА за статусом — світлі, пастельні відтінки
            // EN: WHOLE-ROW fill by status — light, pastel shades
            res["RowUntranslatedBg"] = new SolidColorBrush(Color.FromRgb(0xFB, 0xDE, 0xDE));
            res["RowTranslatedBg"]   = new SolidColorBrush(Color.FromRgb(0xDC, 0xF5, 0xE3));
            res["RowIssueBg"]        = new SolidColorBrush(Color.FromRgb(0xFB, 0xEA, 0xCB));
            res["RowDupWarnBg"]      = new SolidColorBrush(Color.FromRgb(0xED, 0xE0, 0xF5));
            res["RowTechnicalBg"]    = new SolidColorBrush(Color.FromRgb(0xDF, 0xF6, 0xFA));
            res["RowModifiedBg"]     = new SolidColorBrush(Color.FromRgb(0xFB, 0xF0, 0xC8));
            res["RowHoverBg"]        = new SolidColorBrush(Color.FromRgb(0xED, 0xE0, 0xF8));
        }
    }

    private void BtnFontDecrease_Click(object sender, RoutedEventArgs e)
    {
        if (_gridFontSize > 9) { _gridFontSize -= 1; GridEntries.FontSize = _gridFontSize; }
    }

    private void BtnFontIncrease_Click(object sender, RoutedEventArgs e)
    {
        if (_gridFontSize < 24) { _gridFontSize += 1; GridEntries.FontSize = _gridFontSize; }
    }

    // =========================================================================
    // UA: DATAGRID ПОДІЇ / EN: DATAGRID EVENTS
    // =========================================================================

    // UA: Пишемо в Translation ЛИШЕ якщо редагувалась саме колонка
    //     "Переклад" (ColTranslation, x:Name у XAML) — явна перевірка
    //     колонки, а не лише типу елемента: якби колонка "Вичитка" теж
    //     редагувалась через TextBox, без цієї перевірки обробник міг би
    //     переплутати їх (обидва дали б e.EditingElement is TextBox).
    //     Наразі "Вичитка" редагується через ComboBox (див. XAML), тож на
    //     практиці колізії немає — але перевірка колонки лишається як
    //     явний, самодокументований захист, а не випадковий побічний
    //     ефект типу елемента. ReviewStatus так чи інакше приходить через
    //     звичайний двосторонній Binding (UpdateSourceTrigger=PropertyChanged)
    //     без участі цього обробника.
    // EN: Write to Translation ONLY if the "Translation" column itself was
    //     being edited (ColTranslation, x:Name in XAML) — an explicit
    //     column check, not just an element-type check: if the "Review"
    //     column were also edited via a TextBox, without this check the
    //     handler could confuse the two (both would give
    //     e.EditingElement is TextBox). "Review" is currently edited via
    //     a ComboBox (see XAML), so there's no collision in practice —
    //     but the column check stays as an explicit, self-documenting
    //     guard rather than an accidental side effect of the element type.
    //     ReviewStatus arrives via a plain two-way Binding
    //     (UpdateSourceTrigger=PropertyChanged) regardless, without this
    //     handler's involvement.
    private void GridEntries_CellEditEnding(object sender, DataGridCellEditEndingEventArgs e)
    {
        if (e.Row.Item is EntryRow row && e.EditingElement is TextBox tb && ReferenceEquals(e.Column, ColTranslation))
        {
            row.Translation = string.IsNullOrWhiteSpace(tb.Text) ? null : tb.Text;
            row.Validate();
        }
        UpdateProgressDisplay();
    }

    private void GridEntries_SelectionChanged(object sender, SelectionChangedEventArgs e)
    {
        var hasSelection = GridEntries.SelectedItem is EntryRow;
        BtnCopySourceToTarget.IsEnabled = hasSelection;
        BtnMarkTechnical.IsEnabled      = hasSelection;
    }

    // =========================================================================
    // UA: КОНТЕКСТНЕ МЕНЮ / EN: CONTEXT MENU
    // =========================================================================

    // UA: Виділяємо рядок при правому кліку перед показом меню
    // EN: Select row on right-click before showing context menu
    private void GridEntries_PreviewMouseRightButtonDown(object sender,
        System.Windows.Input.MouseButtonEventArgs e)
    {
        var row = FindVisualParent<DataGridRow>((DependencyObject)e.OriginalSource);
        if (row?.Item is EntryRow)
        {
            row.IsSelected = true;
            GridEntries.CurrentItem = row.Item;
        }
    }

    private void CtxCopyOriginal_Click(object sender, RoutedEventArgs e)
    {
        if (GridEntries.SelectedItem is EntryRow row)
            Clipboard.SetText(row.Original);
    }

    private void CtxPasteAsTranslation_Click(object sender, RoutedEventArgs e)
    {
        if (GridEntries.SelectedItem is EntryRow row)
        {
            row.Translation = row.Original;
            RefreshView();
            UpdateProgressDisplay();
        }
    }

    private void CtxClearTranslation_Click(object sender, RoutedEventArgs e)
    {
        if (GridEntries.SelectedItem is EntryRow row)
        {
            row.Translation = null;
            RefreshView();
            UpdateProgressDisplay();
        }
    }

    // UA: Розповсюджує переклад ВИДІЛЕНОГО рядка на всі рядки з ІДЕНТИЧНИМ
    //     оригіналом (технічні — виключено, вони не в групах, див.
    //     RecomputeDuplicates) — за зразком SWH.LocEditor
    //     (CsvLocDocument.ApplyTranslationToDuplicates). Найкорисніше саме
    //     для рядків із HasInconsistentDuplicateTranslation=true (позначені
    //     кольором рядка), але працює для будь-якої групи дублів.
    // EN: Propagates the SELECTED row's translation to all rows with an
    //     IDENTICAL original (technical rows excluded — they aren't in
    //     groups, see RecomputeDuplicates) — modeled after SWH.LocEditor
    //     (CsvLocDocument.ApplyTranslationToDuplicates). Most useful for
    //     rows with HasInconsistentDuplicateTranslation=true (flagged by row
    //     color), but works for any duplicate group.
    private void CtxApplyToDuplicates_Click(object sender, RoutedEventArgs e)
    {
        if (GridEntries.SelectedItem is not EntryRow source) return;
        if (!source.IsDuplicateOriginal) return;

        var count = 0;
        foreach (var row in _allRows.Where(r =>
                     !r.IsTechnical &&
                     !ReferenceEquals(r, source) &&
                     string.Equals(r.Original, source.Original, StringComparison.Ordinal)))
        {
            row.Translation = source.Translation;
            count++;
        }

        RefreshView();
        UpdateProgressDisplay();
        SetStatus($"UA: Переклад застосовано до {count} дублікатів / EN: Translation applied to {count} duplicates");
    }

    private void CtxCopyHash_Click(object sender, RoutedEventArgs e)
    {
        if (GridEntries.SelectedItem is EntryRow row)
            Clipboard.SetText(row.HashDisplay);
    }

    // UA: Знаходить батьківський елемент заданого типу у візуальному дереві
    // EN: Finds parent element of given type in the visual tree
    private static T? FindVisualParent<T>(DependencyObject child)
        where T : DependencyObject
    {
        var current = child;
        while (current != null)
        {
            if (current is T result) return result;
            current = VisualTreeHelper.GetParent(current);
        }
        return null;
    }

    // =========================================================================
    // UA: ЗАКРИТТЯ ВІКНА / EN: WINDOW CLOSING
    // =========================================================================

    private void Window_Closing(object sender, CancelEventArgs e)
    {
        _autoSave.Stop();
        _autoSave.Dispose();
        SimpleLogger.Info("BF1LocalizationTool closed");
    }

    // =========================================================================
    // UA: ВНУТРІШНІ МЕТОДИ / EN: INTERNAL METHODS
    // =========================================================================

    // UA: Знаходить "english" АБО "english_loc" у списку мов — реальний
    //     ключ має суфікс "_loc" для БІНАРНИХ Locl-джерел
    //     (LvlLocalizationService.LoadAsync, file.LanguageName + "_loc"),
    //     а BF1's core.lvl, як емпірично підтверджено через GUI, зберігає
    //     ВСІ мови саме так.
    // EN: Finds "english" OR "english_loc" in the language list — the
    //     real key carries a "_loc" suffix for BINARY Locl sources
    //     (LvlLocalizationService.LoadAsync, file.LanguageName + "_loc"),
    //     and BF1's core.lvl, as empirically confirmed via the GUI, stores
    //     ALL languages this way.
    private static int FindEnglishIndex(IReadOnlyList<string> langs) =>
        langs.ToList().FindIndex(l =>
            l.Equals("english",     StringComparison.OrdinalIgnoreCase) ||
            l.Equals("english_loc", StringComparison.OrdinalIgnoreCase));

    // UA: Мова ОРИГІНАЛУ (_serviceSource) — за замовчуванням "english"/
    //     "english_loc". У грі ніколи не було українського мовного слоту
    //     (перевірено вмістом "uk_english" — це British/UK English,
    //     реальний заповнений англомовний варіант, а не порожнє місце під
    //     переклад).
    // EN: ORIGINAL language (_serviceSource) — defaults to "english"/
    //     "english_loc". The game never had a Ukrainian language slot
    //     (confirmed by content — "uk_english" is British/UK English, a
    //     real filled-in English variant, not an empty translation
    //     placeholder).
    private void PopulateSourceLanguageSelector()
    {
        CmbSourceLang.SelectionChanged -= CmbSourceLang_SelectionChanged;

        var langs = _serviceSource.AvailableLanguages;
        CmbSourceLang.ItemsSource = null;
        CmbSourceLang.ItemsSource = langs;

        var idx = FindEnglishIndex(langs);
        CmbSourceLang.SelectedIndex = idx >= 0 ? idx : 0;
        if (CmbSourceLang.SelectedItem is string src) _sourceLang = src;

        CmbSourceLang.IsEnabled = true;
        CmbSourceLang.SelectionChanged += CmbSourceLang_SelectionChanged;
    }

    // UA: Мова РОБОЧОГО ФАЙЛУ (_serviceTarget) — за аналогією з
    //     попередніми проєктами (SteamWorld, Empire at War) переклад
    //     пишеться ПОВЕРХ "english" — мови за замовчуванням на
    //     ліцензійній копії гри.
    // EN: WORKING FILE language (_serviceTarget) — following the
    //     precedent of prior projects (SteamWorld, Empire at War), the
    //     translation is written OVER "english" — the default language on
    //     a licensed copy of the game.
    private void PopulateTargetLanguageSelector()
    {
        if (_serviceTarget is null) return;

        CmbTargetLang.SelectionChanged -= CmbTargetLang_SelectionChanged;

        var langs = _serviceTarget.AvailableLanguages;
        CmbTargetLang.ItemsSource = null;
        CmbTargetLang.ItemsSource = langs;

        var idx = FindEnglishIndex(langs);
        CmbTargetLang.SelectedIndex = idx >= 0 ? idx : 0;
        if (CmbTargetLang.SelectedItem is string tgt) _targetLang = tgt;

        CmbTargetLang.IsEnabled = true;
        CmbTargetLang.SelectionChanged += CmbTargetLang_SelectionChanged;
    }

    // UA: КРИТИЧНО (перевірено побайтовим парсингом обох ігор): один
    //     32-бітний хеш МОЖЕ належати ДВОМ РІЗНИМ рядкам тексту в тій
    //     самій мові (напр. BF1 english 0xf2e2b10d = і "SQUAD COMMANDS",
    //     і "SQUAD\r\nCOMMANDS" — короткий/переносний варіант того самого
    //     UI-елемента). Тому зіставлення джерело↔робочий файл ведеться НЕ
    //     просто за хешем, а за (хеш, порядковий номер серед записів З
    //     ТИМ САМИМ хешем) — LocalizationFile.GetByHash(hash, ordinal).
    //     Без ordinal обидва рядки з однаковим хешем отримали б ОДИН і
    //     той самий targetEntry, і один з них показував би "чужий"
    //     переклад.
    // EN: CRITICAL (verified via byte-level parsing of both games): one
    //     32-bit hash CAN belong to TWO DIFFERENT strings within the same
    //     language (e.g. BF1 english 0xf2e2b10d = both "SQUAD COMMANDS"
    //     and "SQUAD\r\nCOMMANDS" — short/wrapped variant of the same UI
    //     element). So source↔working-file matching is NOT by hash alone,
    //     but by (hash, ordinal position among entries sharing that same
    //     hash) — LocalizationFile.GetByHash(hash, ordinal). Without
    //     ordinal, both same-hash strings would get the SAME targetEntry,
    //     and one of them would show someone else's translation.
    private void RefreshGrid()
    {
        var sourceFile = _serviceSource.GetLanguageFile(_sourceLang);
        if (sourceFile is null) return;

        var targetFile = _serviceTarget?.GetLanguageFile(_targetLang);
        var hashOccurrence = new Dictionary<uint, int>();

        _allRows = new ObservableCollection<EntryRow>(
            sourceFile.Entries.Select(e =>
            {
                var ordinal = hashOccurrence.TryGetValue(e.Hash, out var n) ? n : 0;
                hashOccurrence[e.Hash] = ordinal + 1;

                var targetEntry = targetFile?.GetByHash(e.Hash, ordinal);

                // UA: "Переклад" бере GUI-редагування цієї сесії
                //     (targetEntry.Translation), а якщо його ще нема —
                //     перевіряє, чи робочий файл УЖЕ має ІНШИЙ текст, ніж
                //     оригінал (ознака перекладу, збереженого в
                //     ПОПЕРЕДНІЙ сесії — тоді він фізично лежить в
                //     Original робочого файлу, бо саме туди пишеться
                //     переклад). Якщо текст робочого файлу ще збігається з
                //     оригіналом — це НЕ переклад, рядок лишається
                //     "не перекладеним".
                // EN: "Translation" takes this session's GUI edit
                //     (targetEntry.Translation), and if there isn't one
                //     yet — checks whether the working file's text
                //     ALREADY differs from the original (a sign of a
                //     translation saved in a PREVIOUS session — it then
                //     physically sits in the working file's Original,
                //     since that's where the translation is written).
                //     If the working file's text still matches the
                //     original — this is NOT a translation, the row stays
                //     "untranslated".
                string? translation = targetEntry?.Translation;
                if (translation is null && targetEntry is not null && targetEntry.Original != e.Original)
                    translation = targetEntry.Original;

                // UA: ReviewStatus сюди НЕ приходить із сервісів (core.lvl
                //     про нього нічого не знає) — лише зі сховища
                //     _reviewStatuses, яке переживає перебудову _allRows
                //     (див. коментар над полем і SnapshotReviewStatuses).
                // EN: ReviewStatus does NOT come from the services here
                //     (core.lvl knows nothing about it) — only from the
                //     _reviewStatuses store, which survives _allRows being
                //     rebuilt (see the comment above the field and
                //     SnapshotReviewStatuses).
                var reviewStatus = _reviewStatuses.TryGetValue((e.Hash, ordinal), out var rs) ? rs : string.Empty;

                var row = new EntryRow
                {
                    Hash         = e.Hash,
                    Ordinal      = ordinal,
                    Original     = e.Original,
                    IsTechnical  = TechnicalStringService.IsTechnical(e.Original),
                    ReviewStatus = reviewStatus
                };
                row.SetLoadedTranslation(translation);
                return row;
            }));

        RecomputeDuplicates();

        _view = CollectionViewSource.GetDefaultView(_allRows);
        _view.Filter = FilterRow;
        GridEntries.ItemsSource = _view;
        GridEntries.FontSize    = _gridFontSize;

        UpdateProgressDisplay();
        BtnAutoMarkTechnical.IsEnabled = true;
    }

    // UA: Переносить правки з таблиці в РОБОЧИЙ сервіс. Повертає кількість
    //     рядків, для яких у робочому файлі НЕ ЗНАЙШЛОСЬ куди записати.
    //
    //     ПРИЧИНА, чому це повертає ЧИСЛО, а НЕ void: таблиця будується з
    //     ОРИГІНАЛУ (RefreshGrid: sourceFile.Entries), а запис іде в
    //     РОБОЧИЙ файл. Якщо це різні документи (наприклад оригінал —
    //     аддон Tat3 на 2466 рядків, робочий — базова гра на 2457), то
    //     для 22 унікальних рядків Tat3 GetByHash повертає null, і рядок
    //     `if (entry is not null)` МОВЧКИ відкидає щойно введений вручну
    //     переклад — користувач бачить свій текст у таблиці, зберігає —
    //     і у файл не потрапляє НІЧОГО, без жодного повідомлення.
    //     Мовчазний `continue` тут — та сама вада, що і в ImportCsvAsync,
    //     лише дорожча: там втрачається імпорт, тут — ручна робота.
    // EN: Pushes grid edits into the WORKING service. Returns how many rows
    //     had NOWHERE to be written in the working file.
    //
    //     REASON this returns a COUNT INSTEAD OF void: the grid is built
    //     from the ORIGINAL (RefreshGrid: sourceFile.Entries) while
    //     writes go into the WORKING file. If those are different
    //     documents (e.g. original — the Tat3 add-on with 2466 strings,
    //     working — the base game with 2457), then for Tat3's 22 unique
    //     strings GetByHash returns null and the `if (entry is not null)`
    //     line SILENTLY discards the translation just typed by hand — the
    //     user sees their text in the grid, hits save, and nothing
    //     reaches the file, with no message at all. The silent skip here
    //     is the same flaw as in ImportCsvAsync, only costlier: there an
    //     import is lost, here it's manual work.
    private int ApplyRowEditsToService()
    {
        var targetFile = _serviceTarget?.GetLanguageFile(_targetLang);
        if (targetFile is null) return 0;

        var unmatched = 0;
        foreach (var row in _allRows)
        {
            var entry = targetFile.GetByHash(row.Hash, row.Ordinal);
            if (entry is null)
            {
                // UA: Рахуємо ЛИШЕ ті, де є що втрачати — порожній рядок
                //     без перекладу нікуди й не мав потрапити.
                // EN: Count ONLY those with something to lose — an empty
                //     row with no translation had nowhere to go anyway.
                if (!string.IsNullOrWhiteSpace(row.Translation)) unmatched++;
                continue;
            }

            entry.Translation = string.IsNullOrWhiteSpace(row.Translation)
                ? null : row.Translation;
        }
        return unmatched;
    }

    // UA: Копіює поточні позначки вичитки з _allRows у постійне сховище
    //     _reviewStatuses ПЕРЕД викликом RefreshGrid — інакше перебудова
    //     _allRows "з нуля" (зміна розміру шрифту, мови джерела/цілі)
    //     мовчки стерла б усе, що користувач щойно позначив у клітинках
    //     вичитки. НЕ викликається перед Import CSV (там навпаки — файл
    //     має свідомо ПЕРЕЗАПИСАТИ поточні позначки, так само як він уже
    //     перезаписує переклад), і НЕ перед відкриттям нового документа
    //     (Open Original/New Working/Open Working) — там _reviewStatuses
    //     свідомо очищується як для нового файлу.
    // EN: Copies current review marks from _allRows into the persistent
    //     _reviewStatuses store BEFORE calling RefreshGrid — otherwise
    //     rebuilding _allRows "from scratch" (font size change,
    //     source/target language switch) would silently wipe out whatever
    //     the user just marked in the review cells. NOT called before
    //     Import CSV (there, the file is meant to OVERWRITE current marks,
    //     same as it already overwrites the translation), and NOT before
    //     opening a new document (Open Original/New Working/Open Working)
    //     — there _reviewStatuses is deliberately cleared for the new file.
    private void SnapshotReviewStatuses()
    {
        foreach (var row in _allRows)
        {
            if (string.IsNullOrWhiteSpace(row.ReviewStatus)) continue;
            _reviewStatuses[(row.Hash, row.Ordinal)] = row.ReviewStatus;
        }
    }

    // UA: Ordinal — друга колонка, ОБОВ'ЯЗКОВА (не косметика): без неї
    //     Import не міг би відрізнити два рядки з однаковим хешем (див.
    //     коментар над RefreshGrid). Формат узгоджений з
    //     LvlLocalizationService.ExportCsvAsync/ImportCsvAsync — через
    //     СПІЛЬНИЙ LocalizationCsvIo.BuildCsvText (5 колонок, +
    //     ReviewStatus), а не власне ручне екранування.
    // EN: Ordinal — the second column, REQUIRED (not cosmetic): without
    //     it, Import couldn't tell apart two rows sharing the same hash
    //     (see comment above RefreshGrid). Format matches
    //     LvlLocalizationService.ExportCsvAsync/ImportCsvAsync — via
    //     the SHARED LocalizationCsvIo.BuildCsvText (5 columns, +
    //     ReviewStatus), rather than hand-rolled escaping of its own.
    private string? BuildCsvContent()
    {
        if (_allRows.Count == 0) return null;
        var rows = _allRows.Select(r =>
            new LocalizationCsvRow(r.Hash, r.Ordinal, r.Original, r.Translation, r.ReviewStatus));
        return LocalizationCsvIo.BuildCsvText(rows);
    }

    private void UpdateProgressDisplay()
    {
        // UA: Guard — метод може викликатись до завершення InitializeComponent()
        // EN: Guard — method may be called before InitializeComponent() completes
        if (TxtProgress is null || TxtValidationWarnings is null || TxtFilterCount is null)
            return;

        var total       = _allRows.Count;
        var technical   = _allRows.Count(r => r.IsTechnical);
        var toTranslate = total - technical;
        var translated  = _allRows.Count(r => r.IsTranslated && !r.IsTechnical);
        var edits       = _allRows.Count(r => r.IsModified);
        var warnings    = _allRows.Count(r => !r.IsValid && !r.IsTechnical);
        var pct         = toTranslate == 0 ? 0.0 : (double)translated / toTranslate * 100;

        // UA: Лічильники у кнопках фільтрів — як в EaW
        // EN: Counts in filter buttons — like EaW
        var untranslated = _allRows.Count(r => !r.IsTranslated && !r.IsTechnical);
        RbAll.Content          = $"UA: Усі / EN: All · {total}";
        RbUntranslated.Content = $"UA: Без пер. / EN: Untranslated · {untranslated}";
        RbTranslated.Content   = $"UA: Перекл. / EN: Translated · {translated}";
        RbTechnical.Content    = $"⚙ UA: Техн. / EN: Technical · {technical}";
        RbInvalid.Content      = $"⚠ UA: Проблемні / EN: Issues · {warnings}";

        // UA: Основний лічильник у статус-барі
        // EN: Main counter in status bar
        TxtProgress.Text = $"{translated}/{toTranslate} · {pct:F1}%  |  UA:{edits} правок / EN:{edits} edits";

        if (warnings > 0)
        {
            TxtValidationWarnings.Text       = $"⚠ {warnings}";
            TxtValidationWarnings.Visibility = Visibility.Visible;
        }
        else
            TxtValidationWarnings.Visibility = Visibility.Collapsed;

        var visible = (_view as ListCollectionView)?.Count ?? _allRows.Count;
        TxtFilterCount.Text = $"UA: Показано / EN: Showing: {visible}";
    }

    private void SetStatus(string msg) => TxtStatus.Text = msg;

    private void SetBusy(bool busy, string? statusMsg = null)
    {
        IsEnabled = !busy;
        Cursor    = busy ? System.Windows.Input.Cursors.Wait : null;
        if (statusMsg is not null) SetStatus(statusMsg);
    }

    // UA: BtnExportCsv НЕ тут — він залежить лише від оригіналу (наявності
    //     рядків у _allRows), вмикається окремо в BtnOpenOriginal_Click.
    //     Save/Import/Перенесення з іншої гри залежать від РОБОЧОГО файлу —
    //     саме їх торкається цей метод (викликається після
    //     створення/відкриття _serviceTarget).
    // EN: BtnExportCsv is NOT here — it only depends on the original
    //     (having rows in _allRows), enabled separately in
    //     BtnOpenOriginal_Click. Save/Import/Transfer from another game
    //     depend on the WORKING file — that's what this method touches
    //     (called after creating/opening _serviceTarget).
    private void SetButtonsEnabled(bool enabled)
    {
        BtnSave.IsEnabled                  = enabled;
        BtnImportCsv.IsEnabled             = enabled;
        BtnTransferFromOtherGame.IsEnabled = enabled;
    }
}

// =============================================================================
// UA: Рядок DataGrid
// EN: DataGrid row
// =============================================================================
public class EntryRow : INotifyPropertyChanged
{
    public uint   Hash        { get; init; }

    // UA: Порядковий номер серед записів з ТИМ САМИМ хешем (0 = перший).
    //     ОБОВ'ЯЗКОВИЙ для коректного зіставлення з робочим файлом і для
    //     CSV — див. коментар над MainWindow.RefreshGrid.
    // EN: Position among entries sharing the SAME hash (0 = first).
    //     REQUIRED for correct matching against the working file and for
    //     CSV — see the comment above MainWindow.RefreshGrid.
    public int    Ordinal     { get; init; }
    public string HashDisplay => $"0x{Hash:x8}";
    public string Original    { get; init; } = string.Empty;

    private string? _translation;
    private string? _originalTranslation; // UA: значення при завантаженні / EN: value at load time

    public string? Translation
    {
        get => _translation;
        set
        {
            if (_translation == value) return;
            _translation = value;
            OnPropertyChanged(nameof(Translation));
            OnPropertyChanged(nameof(TranslationDisplay));
            OnPropertyChanged(nameof(IsTranslated));
            OnPropertyChanged(nameof(IsModified));
            OnPropertyChanged(nameof(StatusColor));
            OnPropertyChanged(nameof(StatusTooltip));
            Validate();
        }
    }

    // UA: Текст що відображається в колонці перекладу:
    //     технічні — повідомлення, інші — сам переклад
    // EN: Text shown in translation column:
    //     technical — message, others — translation itself
    public string TranslationDisplay =>
        IsTechnical
            ? "⚙ UA: Технічний — не перекладати! / EN: Technical — do not translate!"
            : (Translation ?? string.Empty);

    // UA: true якщо переклад змінився після завантаження
    // EN: true if translation changed since load
    public bool IsModified => _translation != _originalTranslation;

    public void SetLoadedTranslation(string? value)
    {
        _translation         = value;
        _originalTranslation = value;
        OnPropertyChanged(nameof(Translation));
        OnPropertyChanged(nameof(TranslationDisplay));
        OnPropertyChanged(nameof(IsTranslated));
        OnPropertyChanged(nameof(IsModified));
        Validate();
    }

    // UA: Статус вичитки — вільний текст (напр. "+", "-", "+/-" чи
    //     коментар), за зразком review-колонки в SWH.LocEditor
    //     (LocEntry.ReviewNote). ПРИНЦИПОВО не пов'язаний з core.lvl:
    //     LvlLocalizationService/SaveAsync про це поле нічого не знає —
    //     воно живе ЛИШЕ в GUI (MainWindow._reviewStatuses,
    //     що переживає перебудову _allRows у RefreshGrid) і в "робочому"
    //     CSV, який пишеться поряд зі збереженням core.lvl
    //     (MainWindow.BtnSave_Click → BuildCsvContent → review\...csv).
    // EN: Proofreading status — free text (e.g. "+", "-", "+/-", or a
    //     comment), modeled after the review column in SWH.LocEditor
    //     (LocEntry.ReviewNote). DELIBERATELY unrelated to core.lvl:
    //     LvlLocalizationService/SaveAsync knows nothing about this field —
    //     it lives ONLY in the GUI (MainWindow._reviewStatuses, which
    //     survives _allRows being rebuilt in RefreshGrid) and in the
    //     "working" CSV written alongside the core.lvl save
    //     (MainWindow.BtnSave_Click → BuildCsvContent → review\...csv).
    private string _reviewStatus = string.Empty;
    public string ReviewStatus
    {
        get => _reviewStatus;
        set
        {
            value ??= string.Empty;
            if (_reviewStatus == value) return;
            _reviewStatus = value;
            OnPropertyChanged(nameof(ReviewStatus));
            OnPropertyChanged(nameof(WasReviewed));
        }
    }

    public bool WasReviewed => !string.IsNullOrWhiteSpace(ReviewStatus);

    // UA: Механіка показу дублів (ідентичного оригіналу) — за зразком
    //     SWH.LocEditor (LocEntry.DuplicateGroupSize/IsDuplicateOriginal/
    //     HasInconsistentDuplicateTranslation, обчислюються ЗОВНІ, у
    //     MainWindow.RecomputeDuplicates, точнісінько як там же
    //     CsvLocDocument.RecomputeDuplicates обчислює їх для LocEntry).
    //     ПРИЗНАЧЕННЯ: коли ОДИН і той самий англійський рядок трапляється
    //     у грі кілька разів під різними хешами (типовий кейс: назва
    //     зброї повторюється і в списку екіпіровки, і в підказці), легко
    //     перекласти один випадок і забути про інші — вони лишаються
    //     англійськими, хоча "виглядають перекладеними" у списку "Без
    //     перекладу" (бо один з дублів уже перекладено, лічильник це не
    //     покаже як проблему). DuplicateGroupSize/IsDuplicateOriginal дають
    //     ЗНАК рядку, що в нього є "брати"; HasInconsistentDuplicateTranslation
    //     — явна ЧЕРВОНА ПРАПОРЕЦЬ, що бодай один із братів має ІНШИЙ
    //     переклад (або взагалі не перекладений, поки решта — так).
    // EN: Duplicate-text (identical original) detection — modeled after
    //     SWH.LocEditor (LocEntry.DuplicateGroupSize/IsDuplicateOriginal/
    //     HasInconsistentDuplicateTranslation, computed EXTERNALLY, in
    //     MainWindow.RecomputeDuplicates, exactly like
    //     CsvLocDocument.RecomputeDuplicates computes them there for
    //     LocEntry). PURPOSE: when the same English string appears in the
    //     game multiple times under different hashes (typical case: a
    //     weapon name repeats in both the loadout list and a tooltip), it's
    //     easy to translate one occurrence and forget the others — they
    //     stay in English even though the row "looks handled" in the
    //     translated list (since one of the duplicates is already
    //     translated, the counter won't flag it as a problem).
    //     DuplicateGroupSize/IsDuplicateOriginal give the row a SIGN that it
    //     has "siblings"; HasInconsistentDuplicateTranslation is an explicit
    //     RED FLAG that at least one sibling has a DIFFERENT translation
    //     (or none at all, while the rest do).
    private int _duplicateGroupSize = 1;
    public int DuplicateGroupSize
    {
        get => _duplicateGroupSize;
        private set
        {
            if (_duplicateGroupSize == value) return;
            _duplicateGroupSize = value;
            OnPropertyChanged(nameof(DuplicateGroupSize));
            OnPropertyChanged(nameof(IsDuplicateOriginal));
        }
    }

    public bool IsDuplicateOriginal => DuplicateGroupSize > 1;

    private bool _hasInconsistentDuplicateTranslation;
    public bool HasInconsistentDuplicateTranslation
    {
        get => _hasInconsistentDuplicateTranslation;
        private set
        {
            if (_hasInconsistentDuplicateTranslation == value) return;
            _hasInconsistentDuplicateTranslation = value;
            OnPropertyChanged(nameof(HasInconsistentDuplicateTranslation));
        }
    }

    // UA: Викликається ЛИШЕ з MainWindow.RecomputeDuplicates — рядок сам
    //     не знає про своїх "братів" (немає посилання на _allRows), тож
    //     групування рахується зовні й лише проштовхується сюди.
    // EN: Called ONLY from MainWindow.RecomputeDuplicates — a row doesn't
    //     know about its "siblings" on its own (no reference to _allRows),
    //     so grouping is computed externally and just pushed in here.
    public void SetDuplicateInfo(int groupSize, bool inconsistent)
    {
        DuplicateGroupSize = groupSize;
        HasInconsistentDuplicateTranslation = inconsistent;
    }

    private bool _isTechnical;
    public bool IsTechnical
    {
        get => _isTechnical;
        set
        {
            if (_isTechnical == value) return;
            _isTechnical = value;
            OnPropertyChanged(nameof(IsTechnical));
            OnPropertyChanged(nameof(TranslationDisplay));
            // UA: IsTranslated тепер ЗАЛЕЖИТЬ від IsTechnical (див. коментар
            //     над IsTranslated) — тому теж повідомляємо про зміну.
            // EN: IsTranslated now DEPENDS on IsTechnical (see the comment
            //     above IsTranslated) — so notify about it too.
            OnPropertyChanged(nameof(IsTranslated));
            OnPropertyChanged(nameof(StatusColor));
            OnPropertyChanged(nameof(StatusTooltip));
        }
    }

    // UA: "Перекладено" стосується ЛИШЕ змісту перекладу і НЕ залежить від
    //     технічності — три статуси (Технічний / Перекладено / Без перекладу)
    //     мають бути ВЗАЄМОВИКЛЮЧНИМИ. Домішування "IsTechnical ||" сюди
    //     зробило б технічні рядки з будь-яким текстом одночасно і
    //     "технічними", і "перекладеними" — тому технічність розводиться
    //     ОКРЕМО: у лічильниках (Count(... && !IsTechnical)) і у фільтрах
    //     (RbTranslated ховає технічні), а IsTranslated лишається чистою
    //     ознакою "є справжній переклад": непорожній І (короткий нижче
    //     порогу, АБО містить кирилицю). Ехо-англійський переклад
    //     нетехнічного рядка кирилиці не має → НЕ "перекладено" (падає в
    //     "Без перекладу", як і задумано).
    // EN: "Translated" is ONLY about the translation content and does NOT
    //     depend on technical-ness — the three statuses (Technical /
    //     Translated / Untranslated) must be MUTUALLY EXCLUSIVE. Mixing an
    //     "IsTechnical ||" term in here would make technical rows with any
    //     text simultaneously "technical" AND "translated" — so
    //     technical-ness is split out SEPARATELY: in counts
    //     (Count(... && !IsTechnical)) and filters (RbTranslated hides
    //     technical), while IsTranslated stays a clean "has a real
    //     translation" signal: non-empty AND (short below the threshold,
    //     OR contains Cyrillic). An echo English translation of a
    //     non-technical row has no Cyrillic → NOT "translated" (falls into
    //     "Untranslated", as intended).
    public bool IsTranslated =>
        !string.IsNullOrWhiteSpace(Translation) &&
        (Translation!.Length < ValidationService.MinLengthForCyrillicCheck ||
         ValidationService.ContainsCyrillic(Translation));

    // UA: Поріг "задовгого" перекладу — коефіцієнт + запас (та сама формула,
    //     що вже перевірена в TranslationValidator, тут з ІНШИМИ, тіснішими
    //     значеннями за замовчуванням). Static + settable з GUI-попапу
    //     (⚙ біля рядка стану), бо це РІВЕНЬ ПРЕЗЕНТАЦІЇ (наскільки рядок
    //     "виглядає підозріло довгим"), а не мовна коректність — не
    //     належить Core/ValidationService.
    //
    //     Калібровано на ДВОХ реальних кейсах з гри:
    //       1) "tech" (4) → "технології" (10): реально ламало інтерфейс
    //          у попередньому проєкті (EaW) — поріг МАЄ спрацювати.
    //          4×1.3+4=9.2 → 10>9.2 ✓ спрацьовує.
    //       2) "SINGLEPLAYER" (12) → АІ "ОДНОКОРИСТУВАЦЬКА ГРА" (21):
    //          РЕАЛЬНО обрізало текст у вкладці BF2 "МЕРЕЖЕВА ГРА/..." —
    //          поріг МАЄ спрацювати.
    //          12×1.3+4=19.6 → 21>19.6 ✓ спрацьовує.
    //          Ручний варіант "САМОСТІЙНА ГРА" (14, у межах допустимого) —
    //          НЕ мав би спрацьовувати.
    //          12×1.3+4=19.6 → 14<19.6 ✓ не спрацьовує.
    // EN: "Too long" threshold — ratio + margin (the same formula already
    //     proven in TranslationValidator, here with DIFFERENT, tighter
    //     defaults). Static + settable from the GUI popup (⚙ next to the
    //     status bar), because this is a PRESENTATION-LEVEL concern (does
    //     the string "look suspiciously long"), not language correctness —
    //     doesn't belong in Core/ValidationService.
    //
    //     Calibrated against TWO real in-game cases:
    //       1) "tech" (4) → "технології" (10): actually broke the UI in
    //          the earlier project (EaW) — the threshold SHOULD trigger.
    //          4×1.3+4=9.2 → 10>9.2 ✓ triggers.
    //       2) "SINGLEPLAYER" (12) → AI "ОДНОКОРИСТУВАЦЬКА ГРА" (21):
    //          REALLY got clipped in BF2's "МЕРЕЖЕВА ГРА/..." tab — the
    //          threshold SHOULD trigger.
    //          12×1.3+4=19.6 → 21>19.6 ✓ triggers.
    //          The manual "САМОСТІЙНА ГРА" (14) alternative, within the
    //          safe range — should NOT trigger.
    //          12×1.3+4=19.6 → 14<19.6 ✓ doesn't trigger.
    public static double LengthRatioThreshold = 1.3;
    public static int    LengthMarginThreshold = 4;

    // UA: Результат валідації
    // EN: Validation result
    private bool   _isValid = true;
    private string _validationMessage = string.Empty;

    public bool   IsValid           => _isValid;
    public bool   HasValidationError => !_isValid;
    public string ValidationMessage  => _validationMessage;

    public void Validate()
    {
        var result = ValidationService.Validate(Original, Translation);

        // UA: Технічні рядки (коди, власні назви) НЕ підлягають цій
        //     перевірці — їхня "довжина" не має мовного сенсу (див.
        //     TechnicalStringService), і технічні рядки й так виключені
        //     з лічильника/фільтра "Проблемні" деінде — тут узгоджено з
        //     тим самим принципом.
        // EN: Technical rows (codes, proper names) are NOT subject to this
        //     check — their "length" has no linguistic meaning (see
        //     TechnicalStringService), and technical rows are already
        //     excluded from the "Issues" counter/filter elsewhere — kept
        //     consistent with that same principle here.
        var tooLong = !IsTechnical &&
                      !string.IsNullOrEmpty(Translation) &&
                      Translation!.Length > Original.Length * LengthRatioThreshold + LengthMarginThreshold;

        _isValid = result.IsValid && !tooLong;
        _validationMessage = !result.IsValid
            ? result.Message
            : tooLong
                ? $"UA: Переклад задовгий ({Translation!.Length} симв. проти {Original.Length} в оригіналі) / " +
                  $"EN: Translation too long ({Translation!.Length} chars vs {Original.Length} in original)"
                : string.Empty;

        OnPropertyChanged(nameof(IsValid));
        OnPropertyChanged(nameof(HasValidationError));
        OnPropertyChanged(nameof(ValidationMessage));
        OnPropertyChanged(nameof(StatusColor));
    }

    // UA: Колір статусного кружечка
    // EN: Status dot color
    public Brush StatusColor
    {
        get
        {
            if (!_isValid)    return new SolidColorBrush(Color.FromRgb(0xE6, 0xA8, 0x32)); // warning
            if (IsTechnical)  return new SolidColorBrush(Color.FromRgb(0x7A, 0x9E, 0xB5)); // technical
            if (IsTranslated) return new SolidColorBrush(Color.FromRgb(0x4C, 0xAF, 0x7D)); // translated
            return new SolidColorBrush(Color.FromRgb(0xCF, 0x66, 0x79));                   // untranslated
        }
    }

    public string StatusTooltip =>
        !_isValid    ? $"UA: Проблема / EN: Issue: {_validationMessage}" :
        IsTechnical  ? "UA: Технічний / EN: Technical" :
        IsTranslated ? "UA: Перекладено / EN: Translated" :
                       "UA: Не перекладено / EN: Untranslated";

    public event PropertyChangedEventHandler? PropertyChanged;
    private void OnPropertyChanged(string name) =>
        PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(name));
}
