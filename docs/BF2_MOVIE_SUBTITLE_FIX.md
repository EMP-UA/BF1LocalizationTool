# BF2: фікс зникнення субтитрів відеороликів (`d3d9.dll`) / BF2: fixing disappearing movie subtitles (`d3d9.dll`)

## 1. Дефект / The bug

**UA:** На будь-якій роздільності, відмінній від 4:3 чи 5:4 (тобто на 16:9,
16:10), підпис під заголовком відеоролика в Battlefront II не
малюється — не обрізається й не зміщується, а відсутній повністю. Ролик
і решта інтерфейсу відображаються нормально. Це підтверджений баг
оригінальної гри: він відтворюється на повністю ванільних файлах, без
жодних правок цього інструмента.

Формат контейнера відео (`.mvs`) сам не містить жодних даних субтитрів —
увесь текст зберігається у звичайних записах `Locl` у `core.lvl`, а
`.mvs` містить лише відеопотік. Дефект — не у файлах локалізації, а в
самому рушії гри.

**EN:** At any resolution other than 4:3 or 5:4 (i.e. 16:9, 16:10), the caption
under a movie's title in Battlefront II isn't drawn at all — not clipped,
not shifted, simply absent. The clip and the rest of the interface render
normally. This is a confirmed bug in the original game: it reproduces on
fully vanilla files, with none of this tool's changes applied.

The video container format (`.mvs`) itself holds no subtitle data at
all — all text lives in ordinary `Locl` entries inside `core.lvl`, and
`.mvs` contains only the video stream. The defect isn't in the
localization files; it's in the game engine itself.

## 2. Причина / Root cause

**UA:** Рушій малює двовимірний інтерфейс на віртуальному «полотні» 640×480
(пропорція 4:3), а показує це полотно камерою з реальною пропорцією
екрана гравця. Горизонтальний масштаб полотна обчислюється коректно, як
`640 / ширина_екрана`, а вертикальний — окремо, як `480 / висота_екрана`.
Ці два значення узгоджені лише на 4:3, де вся висота полотна потрапляє в
кадр. На 16:9 камера фізично показує лише `640×360` з полотна `640×480`
— чверть висоти полотна виходить за межі видимого кадру. Підпис
відеоролика прив'язаний до самого низу «безпечної зони» (фіксована
константа `0.9` від половини висоти полотна), яка на 16:9 потрапляє саме
в цю невидиму смугу.

**EN:** The engine draws its 2D interface onto a virtual 640×480 canvas (a 4:3
aspect ratio), then displays that canvas through a camera matching the
player's actual screen aspect ratio. The canvas's horizontal scale is
computed correctly as `640 / screen_width`, while its vertical scale is
computed separately as `480 / screen_height`. The two only agree at 4:3,
where the canvas's full height fits in frame. At 16:9, the camera
physically shows only `640×360` of the `640×480` canvas — a quarter of
the canvas height falls outside the visible frame. The movie caption is
anchored to the very bottom of a "safe area" (a fixed `0.9` constant of
half the canvas height), which at 16:9 lands exactly in that invisible
strip.

## 3. Виправлення / The fix

**UA:** Проблема виправляється без зміни `core.lvl` чи будь-яких файлів
локалізації — окремим файлом `d3d9.dll`. У ванільній грі такого файлу
немає: він кладеться в теку гри поруч із `BattlefrontII.exe`, не
замінюючи жодного оригінального файлу.

`d3d9.dll` — проксі-бібліотека. Вона підміняє системну `d3d9.dll` за
стандартним порядком пошуку бібліотек Windows, одразу довантажує
справжню системну бібліотеку й пропускає крізь себе без змін усі
виклики, крім `IDirect3DDevice9::DrawPrimitive` і
`::DrawIndexedPrimitive`. Перед кожним із цих двох викликів проксі читає
поточні константи вертексного шейдера, що відповідають масштабу полотна
(`StartRegister=12, Vector4fCount=9`), і якщо виявлена асиметрія
відповідає або самому багу (`scaleY ≈ 480 / висота_екрана`), або
окремому випадку екрана вибору бійця (текстура в першому слоті — формат
DXT3), виправляє `scaleY`, підганяючи його під
`scaleX = 640 / ширина_екрана`.

Перехоплення саме цих двох методів рендеру, а не сеттера константи
шейдера напряму, — принципове рішення: гра встановлює цю константу не
через публічний метод пристрою, а через `IDirect3DStateBlock9`, що минає
публічний vtable пристрою повністю. Пряме хукання сеттера або підміна
вказівника на таблицю методів пристрою призводить до аварійного
завершення гри.

**EN:** The problem is fixed without touching `core.lvl` or any localization
file — with a separate `d3d9.dll`. The vanilla game ships no such file:
it's placed in the game folder next to `BattlefrontII.exe`, replacing
nothing.

`d3d9.dll` is a proxy library. It stands in for the system `d3d9.dll`
via Windows' standard library search order, immediately loads the real
system library, and passes every call through unchanged except
`IDirect3DDevice9::DrawPrimitive` and `::DrawIndexedPrimitive`. Before
each of those two calls, the proxy reads the current vertex-shader
constants that hold the canvas scale (`StartRegister=12,
Vector4fCount=9`), and if the asymmetry it finds matches either the bug
itself (`scaleY ≈ 480 / screen_height`) or the separate unit-selection
screen case (the texture in slot 0 is DXT3-format), it corrects
`scaleY` to match `scaleX = 640 / screen_width`.

Intercepting exactly these two render calls, rather than the shader
constant setter directly, is the key design choice: the game sets that
constant not through the device's public method but through
`IDirect3DStateBlock9`, which bypasses the device's public vtable
entirely. Hooking the setter directly, or replacing the device's method
table pointer, crashes the game.

## 4. Підтвердження / Confirmation

**UA:** Перевірено реальною грою на 1920×1080: усі три пристрої
`IDirect3DDevice9`, які гра створює під час роботи, коректно перехоплені;
зафіксовано виправлення `scaleY` з `0.444444` на очікуване `0.333333`
(`scaleX = 0.333333` для цієї роздільності). Чотирма знімками одного
проходження підтверджено: текст екрана завантаження на місці, субтитр
відеоролика присутній і читається, екран вибору бійця не спотворений,
бойова панель зброї (зброя, боєприпаси, здоров'я, витривалість, компас)
без зсуву чи стиснення.

**EN:** Verified with a real play session at 1920×1080: all three
`IDirect3DDevice9` instances the game creates during runtime are
correctly intercepted; the fix is recorded changing `scaleY` from
`0.444444` to the expected `0.333333` (`scaleX = 0.333333` at that
resolution). Four screenshots from one session confirm: loading-screen
text is in place, the movie caption is present and legible, the
unit-selection screen isn't distorted, and the combat weapon HUD (weapon,
ammo, health, stamina, compass) shows no shift or compression.
