# VibeTorrent — дизайн-система

**Версия:** 1.0 · **Статус:** к реализации · **Платформа:** Windows 11, WPF, .NET 10
**Единицы:** все размеры — DIP (device-independent pixel, 1/96"), если не указано иное.
**Связанный документ:** [ux-spec.md](./ux-spec.md) — экраны, состояния, тексты, адаптивность.

Документ описывает только визуальный слой: токены, компоненты, motion, доступность.
Никаких внешних ассетов, шрифтов, иконок-файлов и картинок — только Segoe UI Variable,
Segoe Fluent Icons, градиенты и геометрия.

---

## 1. Принципы

1. **Тишина как роскошь.** 90 % площади — нейтральный графит. Акцент — только там, где
   пользователь действует или где изменилось состояние. Не более одного градиентного
   акцента в одной визуальной группе.
2. **Глубина без грязи.** Иерархия строится светлотой поверхности и 1 px границей, а не
   тенями. Тени — привилегия плавающих слоёв (меню, тост, диалог, тултип).
3. **Движение объясняет, а не развлекает.** Анимируется только появление/исчезновение,
   смена состояния и «живой» прогресс. Список при скролле не анимируется никогда.
4. **Плотно для профи, понятно новичку.** Строка списка несёт 6 параметров, но читается
   одной строкой названия и одной строкой метаданных.
5. **Клавиатура — первый класс.** Любое действие мышью имеет клавиатурный эквивалент и
   видимый фокус. Цвет никогда не единственный носитель смысла.

---

## 2. Цвет

### 2.1 Сырая палитра (ramp)

Нейтральная ось — «графит с индиго»: холодная, синий канал выше красного на 8–16 единиц.
Чистый чёрный и чистый серый не используются.

| Имя | HEX | Роль в ramp |
|---|---|---|
| `ink-000` | `#06070C` | абсолютный низ (скрим, тень окна) |
| `ink-900` | `#080A11` | фон окна, тайтлбар |
| `ink-850` | `#0C0F17` | фон контента (canvas) |
| `ink-800` | `#11151F` | панели: сайдбар, детали |
| `ink-750` | `#151A26` | поверхность карточек (surface-1) |
| `ink-700` | `#1B2130` | hover-поверхность, меню, тултип (surface-2) |
| `ink-650` | `#232B3D` | нажатая поверхность, поля (surface-3) |
| `ink-500` | `#364159` | сильная граница, thumb скроллбара |
| `ink-400` | `#4A5773` | граница hover, разделитель активный |
| `ink-300` | `#5D6C8C` | интерактивная граница (3:1 к фону) |
| `ink-200` | `#8A95AD` | третичный текст |
| `ink-100` | `#AEB8CE` | вторичный текст |
| `ink-050` | `#E9EDF8` | основной текст |

Акцентная триада (единственная хроматическая ось продукта):

| Имя | HEX | Имя | HEX | Имя | HEX |
|---|---|---|---|---|---|
| `cyan-300` | `#67E8F9` | `violet-400` | `#A78BFA` | `pink-300` | `#F9A8D4` |
| `cyan-400` | `#22D3EE` | `violet-450` | `#9D7BFA` | `pink-400` | `#F472B6` |
| `cyan-500` | `#06B6D4` | `violet-500` | `#8B5CF6` | `pink-500` | `#EC4899` |

Статусные цвета:

| Имя | HEX | Имя | HEX |
|---|---|---|---|
| `green-400` | `#4ADE80` | `amber-400` | `#FBBF24` |
| `green-500` | `#22C55E` | `red-400` | `#FB7185` |
| `blue-400` | `#60A5FA` | `red-600` | `#BE123C` |
| `slate-400` | `#94A3B8` | | |

### 2.2 Семантические токены

Ключи ресурсов WPF: `VT.Color.*` (Color) + `VT.Brush.*` (SolidColorBrush).

**Фоны и поверхности**

| Токен | HEX | Применение |
|---|---|---|
| `VT.Color.Bg.Window` | `#080A11` | окно, тайтлбар, область за скруглением |
| `VT.Color.Bg.Canvas` | `#0C0F17` | центральная область контента |
| `VT.Color.Bg.Panel` | `#11151F` | сайдбар, панель деталей, страница настроек |
| `VT.Color.Surface1` | `#151A26` | карточка торрента, строка таблицы, поле-карточка |
| `VT.Color.Surface2` | `#1B2130` | hover карточки, меню, тултип, тост |
| `VT.Color.Surface3` | `#232B3D` | нажатие, фон поля ввода в фокусе, сегмент-контрол |
| `VT.Color.SurfaceInset` | `#080A11` | трек прогресса, «утопленное» поле, код/значение |
| `VT.Color.Scrim` | `#B305070C` | затемнение под модалкой (72 %) |

**Границы**

| Токен | HEX | Применение | Контраст к соседнему фону |
|---|---|---|---|
| `VT.Color.Border.Subtle` | `#1D2331` | разделители внутри панелей, линии секций | 1,22 к canvas (декоративная, допустимо) |
| `VT.Color.Border.Default` | `#262E40` | рамка карточки, панели, меню | 1,41 к canvas |
| `VT.Color.Border.Strong` | `#364159` | рамка hover, активная вкладка-подложка, thumb | 1,88 к canvas |
| `VT.Color.Border.Interactive` | `#5D6C8C` | рамка полей ввода, чекбоксов, свитчей (обязательна ≥3:1) | 3,64 к `#0C0F17`; 3,30 к surface-1 |
| `VT.Color.Border.Focus` | `#67E8F9` | кольцо фокуса, 2 px | 9,76–13,64 к любым фонам |

**Текст**

| Токен | HEX | Применение | Контраст (мин.–макс. по поверхностям) |
|---|---|---|---|
| `VT.Color.Text.Primary` | `#E9EDF8` | названия, значения, заголовки | 12,08–16,89 |
| `VT.Color.Text.Secondary` | `#AEB8CE` | метаданные, описания, подписи полей | 7,11–9,93 |
| `VT.Color.Text.Tertiary` | `#8A95AD` | placeholder, третьестепенные подписи, disabled-пояснения | 4,70–5,71 |
| `VT.Color.Text.Disabled` | `#5A6479` | только неактивные контролы (не несёт информации) | 2,38–3,33 |
| `VT.Color.Text.OnAccent` | `#071018` | текст на градиентной кнопке/акцентной плашке | 6,06–10,60 (по стопам градиента) |
| `VT.Color.Text.OnDanger` | `#FFFFFF` | текст на `#BE123C` | 6,29 |

**Статусы** (иконка + текст + цвет; цвет никогда не единственный признак)

| Токен | HEX | Значение | Контраст к surface-3 |
|---|---|---|---|
| `VT.Color.Status.Downloading` | `#22D3EE` | Загружается | 7,83 |
| `VT.Color.Status.Seeding` | `#4ADE80` | Раздаётся | 8,12 |
| `VT.Color.Status.Completed` | `#94A3B8` | Завершён, не раздаётся | 5,52 |
| `VT.Color.Status.Paused` | `#94A3B8` | Пауза | 5,52 |
| `VT.Color.Status.Queued` | `#8A95AD` | В очереди | 4,70 |
| `VT.Color.Status.Metadata` | `#60A5FA` | Получение метаданных | 5,56 |
| `VT.Color.Status.Checking` | `#A78BFA` | Проверка файлов | 5,20 |
| `VT.Color.Status.Warning` | `#FBBF24` | Предупреждение | 8,47 |
| `VT.Color.Status.Error` | `#FB7185` | Ошибка | 5,26 |

**Служебные**

| Токен | HEX | Применение |
|---|---|---|
| `VT.Color.Danger.Solid` | `#BE123C` | деструктивная кнопка (только в диалоге удаления) |
| `VT.Color.Danger.SolidHover` | `#C81E4A` | hover (белый текст 5,61) |
| `VT.Color.Danger.SolidPressed` | `#A10F35` | pressed |
| `VT.Color.Danger.Soft` | `#26FB7185` | фон danger-ghost hover (15 %) |
| `VT.Color.Highlight.White06` | `#0FFFFFFF` | hover-оверлей (6 %) |
| `VT.Color.Highlight.White08` | `#14FFFFFF` | hover-оверлей акцентных кнопок (8 %) |
| `VT.Color.Highlight.Black08` | `#14000000` | pressed-оверлей (8 %) |
| `VT.Color.Track` | `#1E2534` | трек прогресс-бара, дорожка кольца |
| `VT.Color.Shadow` | `#000000` | цвет теней |

### 2.3 Градиенты

Все градиенты — `LinearGradientBrush`/`RadialGradientBrush` со `StartPoint`/`EndPoint`,
замороженные (`PresentationOptions:Freeze="True"`), кроме явно анимируемых.

| Токен | Тип | Стопы | Применение |
|---|---|---|---|
| `VT.Grad.Accent` | Linear, `0,0 → 1,1` (135°) | `#22D3EE` 0 %, `#9D7BFA` 52 %, `#F472B6` 100 % | primary-кнопка, логотип, активная навигация |
| `VT.Grad.Accent.Soft` | Linear, `0,0 → 1,1` | те же при α=15 % (`#2622D3EE`, `#269D7BFA`, `#26F472B6`) | фон выбранной строки, активный чип |
| `VT.Grad.Accent.Bar` | Linear, `0,0 → 0,1` (вертикаль) | те же 3 стопа | левая полоса выбранной строки, индикатор вкладки |
| `VT.Grad.Progress.Download` | Linear, `0,0 → 1,0` | `#22D3EE` → `#9D7BFA` | заполнение прогресса загрузки |
| `VT.Grad.Progress.Seed` | Linear, `0,0 → 1,0` | `#A78BFA` → `#F472B6` | заполнение прогресса раздачи |
| `VT.Grad.Aurora` | Radial, центр `0.2,-0.15`, радиус `0.95` | `#1422D3EE` → `#0F9D7BFA` 45 % → `#00000000` | статичное свечение за тулбаром (1 слой на окно) |
| `VT.Grad.Halo` | Radial, центр `0.5,0.5` | `#409D7BFA` 0 % → `#00000000` 70 % | подсветка под primary-кнопкой (вместо DropShadow) |
| `VT.Grad.Shimmer` | Linear, `0,0 → 1,0` | `#00FFFFFF` → `#FFFFFFFF` 50 % → `#00FFFFFF` | блик на акцентных поверхностях |
| `VT.Grad.FadeBottom` | Linear, `0,0 → 0,1` | `#000C0F17` → `#FF0C0F17` | нижнее затухание списка под bulk-баром |
| `VT.Grad.ScrimDialog` | Linear, `0,0 → 0,1` | `#B305070C` → `#C905070C` | затемнение под модалкой |

**Правило 90/7/3.** На любом экране: ≈90 % площади — нейтральные поверхности, ≈7 % —
текст и иконки, ≤3 % — градиент. В одном визуальном регионе допускается **один**
градиентный акцент плюс его мягкая подложка (`Soft`). Два насыщенных градиента рядом
(например, primary-кнопка и градиентный заголовок) запрещены.

**Где градиент разрешён:** primary-кнопка действия, логотип, активный пункт навигации,
полоса выбранной строки, заполнение прогресса, индикатор активной вкладки, кольцо статуса
«Загружается» в деталях. **Запрещён:** фон карточек, фон панелей, обычный текст,
disabled-элементы, прогресс в состоянии «Пауза».

### 2.4 XAML: структура и фрагменты

```
Themes/
  Colors.Dark.xaml      → Color + SolidColorBrush (VT.*)
  Gradients.xaml        → LinearGradientBrush / RadialGradientBrush
  Typography.xaml       → FontFamily, TextBlock styles
  Metrics.xaml          → Thickness, CornerRadius, double (VT.Space.*, VT.Size.*)
  Motion.xaml           → Duration, IEasingFunction, Storyboard-шаблоны
  Controls/Buttons.xaml, Inputs.xaml, TorrentCard.xaml, Progress.xaml,
  Controls/ScrollBar.xaml, ToolTip.xaml, Toast.xaml, Dialog.xaml, Menu.xaml
```

```xml
<!-- Colors.Dark.xaml -->
<Color x:Key="VT.Color.Surface1">#FF151A26</Color>
<SolidColorBrush x:Key="VT.Brush.Surface1" Color="{StaticResource VT.Color.Surface1}"
                 PresentationOptions:Freeze="True"/>

<!-- Gradients.xaml -->
<LinearGradientBrush x:Key="VT.Grad.Accent" StartPoint="0,0" EndPoint="1,1"
                     PresentationOptions:Freeze="True">
  <GradientStop Color="#FF22D3EE" Offset="0"/>
  <GradientStop Color="#FF9D7BFA" Offset="0.52"/>
  <GradientStop Color="#FFF472B6" Offset="1"/>
</LinearGradientBrush>
```

Правила для кистей:
- всё, что не анимируется, — `Freeze` (иначе WPF копирует кисть на каждый кадр);
- `DynamicResource` обязателен для цветов и размеров в контролах (тема/контраст
  переключаются без перезапуска); `StaticResource` допустим внутри одного словаря;
- ни один `Effect` не вешается на элемент, у которого анимируется `Opacity`/`RenderTransform`
  (эффект перерисовывается каждый кадр).

---

## 3. Типографика

### 3.1 Семейства

Segoe UI Variable — вариативное семейство с оптическими размерами. WPF не умеет
переключать оптическую ось, поэтому семейство выбирается по кеглю:

| Кегль | FontFamily (строка для XAML) | Токен |
|---|---|---|
| ≤ 12 px | `Segoe UI Variable Small, Segoe UI` | `VT.Font.Family.Small` |
| 13–19 px | `Segoe UI Variable Text, Segoe UI` | `VT.Font.Family.Text` |
| ≥ 20 px | `Segoe UI Variable Display, Segoe UI` | `VT.Font.Family.Display` |

Обязательно указывать fallback `Segoe UI` — на системах без Variable-семейства
(Windows 10, урезанные сборки) интерфейс остаётся читаемым.

Доступные веса: `Light` (300), `SemiLight` (350), `Normal` (400), `SemiBold` (600),
`Bold` (700). **Используем только 400 и 600.** Промежуточные веса не имитируем.

### 3.2 Шкала

| Токен | Размер / интерлиньяж | Вес | Семейство | Регистр | Применение |
|---|---|---|---|---|---|
| `VT.Font.Display` | 32 / 40 | SemiBold | Display | обычный | заголовок пустого состояния, крупная цифра прогресса в деталях |
| `VT.Font.H1` | 24 / 32 | SemiBold | Display | обычный | заголовок страницы («Настройки»), имя торрента в шапке деталей |
| `VT.Font.H2` | 20 / 28 | SemiBold | Display | обычный | заголовок модального окна, заголовок пустого блока |
| `VT.Font.H3` | 16 / 24 | SemiBold | Text | обычный | подсекция настроек, заголовок группы в деталях |
| `VT.Font.BodyStrong` | 14 / 20 | SemiBold | Text | обычный | название торрента, подпись поля, текст кнопки, пункт навигации |
| `VT.Font.Body` | 14 / 20 | Normal | Text | обычный | основной текст, значения полей, описания |
| `VT.Font.BodySmall` | 13 / 18 | Normal | Text | обычный | плотные таблицы (пиры, файлы), вторичные значения |
| `VT.Font.BodySmallStrong` | 13 / 18 | SemiBold | Text | обычный | проценты, скорости, числовые значения |
| `VT.Font.Caption` | 12 / 16 | Normal | Small | обычный | метаданные, подписи, подсказки |
| `VT.Font.CaptionStrong` | 12 / 16 | SemiBold | Small | UPPER | заголовки колонок, статус-чипы, метки секций |
| `VT.Font.Micro` | 11 / 14 | SemiBold | Small | обычный | бейджи-счётчики, хоткей-подсказки, номер версии |

**О трекинге.** WPF не поддерживает `letter-spacing`; не имитируем его пробелами и
`Glyphs`. UPPER-метки держим короткими (≤ 14 символов) и не применяем к ним
дополнительное разрежение. Для `Run` внутри UPPER-метки `Typography.Capitals` не
используется — только реальный верхний регистр из ресурсной строки.

**Числа.** Все числовые значения (скорости, размеры, проценты, счётчики) — с
`Typography.NumeralAlignment="Tabular"`; если метрика шрифта не применилась, колонка
получает фиксированную ширину из ux-spec. Дробный разделитель — запятая (`ru-RU`),
разряды — неразрывный пробел (`U+00A0`).

**Переносы.** Названия торрентов — одна строка, `TextTrimming="CharacterEllipsis"`,
`TextWrapping="NoWrap"`. Полное имя — в тултипе и в шапке панели деталей (2 строки,
`TextTrimming` по словам через `TextWrapping="Wrap"` + `MaxHeight="56"`).

Запрещено: текст меньше 11 px, курсив (`FontStyle="Italic"`) в UI, `Bold` (700) для
акцента — акцент делается цветом и SemiBold, `TextDecorations` кроме ссылок.

---

## 4. Пространство и сетка

### 4.1 Шкала отступов (4-базовая)

| Токен | DIP | Типичное применение |
|---|---|---|
| `VT.Space.05` | 2 | зазор между иконкой и бейджем, внутренний отступ трека |
| `VT.Space.1` | 4 | вертикальный зазор между строками текста в чипе |
| `VT.Space.15` | 6 | зазор иконка→текст в плотной строке, отступ прогресса |
| `VT.Space.2` | 8 | зазор между карточками, внутренний отступ меню, gap иконка→текст |
| `VT.Space.3` | 12 | внутренний отступ карточки по вертикали, gap колонок |
| `VT.Space.4` | 16 | внутренний отступ карточки по горизонтали, поля диалога |
| `VT.Space.5` | 20 | отступ секции настроек внутри |
| `VT.Space.6` | 24 | gutter контента, отступ секций друг от друга |
| `VT.Space.8` | 32 | отступ блока от края панели, gap между группами |
| `VT.Space.10` | 40 | верхний отступ пустого состояния |
| `VT.Space.12` | 48 | расстояние между крупными зонами страницы |
| `VT.Space.16` | 64 | отступ hero-блока, высота тулбара |

Правило: любой отступ — из шкалы. Дробные значения (5, 7, 9, 11, 13, 18) запрещены.

### 4.2 Константы макета

| Параметр | 1280×800 | 1440×900 (default) | 1920×1080 |
|---|---|---|---|
| Ширина окна / минимальная | 1280 / **1024** | 1440 | 1920 |
| Высота окна / минимальная | 800 / **640** | 900 | 1080 |
| Тайтлбар | 40 | 40 | 40 |
| Сайдбар | 232 | 248 | 264 |
| Панель деталей | 336 | 384 | 420 |
| Сплиттер (визуал / зона захвата) | 1 / 8 | 1 / 8 | 1 / 8 |
| Тулбар контента | 64 | 64 | 64 |
| Строка активных фильтров (условная) | 36 | 36 | 36 |
| Шапка колонок списка | 32 | 32 | 32 |
| Карточка торрента | 76 | 76 | 76 |
| Зазор между карточками | 8 | 8 | 8 |
| Gutter контента (слева/справа) | 20 | 24 | 32 |
| Ширина области списка (вкл. gutter) | 711 | 807 | 1235 |
| Ширина карточки | 671 | 759 | 1171 |

Высота строки 76 DIP: текстовый блок 40 DIP (20 + 4 + 16), центрируется по вертикали;
остальное — вертикальные отступы по 18 DIP.

Компактный режим списка (`Настройки → Внешний вид → Плотность: Компактно`) — строка
56 DIP, метаданные в одну строку с названием, прогресс остаётся в своей колонке.

---

## 5. Радиусы, границы, тени

### 5.1 Радиусы

| Токен | DIP | Применение |
|---|---|---|
| `VT.Radius.05` | 2 | полоса-индикатор (левый бар строки), мини-трек |
| `VT.Radius.1` | 3 | заполнение прогресс-бара |
| `VT.Radius.15` | 4 | бейдж-счётчик, маленький чип |
| `VT.Radius.2` | 6 | компактные кнопки 28 px, поле 32 px |
| `VT.Radius.25` | 8 | кнопки 32 px, поля ввода, чипы, пункт меню |
| `VT.Radius.3` | 10 | иконочная плитка 40×40, миниатюра |
| `VT.Radius.4` | 12 | карточка торрента, меню, тост, тултип |
| `VT.Radius.5` | 16 | модальное окно, крупная панель |
| `VT.Radius.Pill` | 999 | статус-чип, переключатель, аватар-кружок |

Вложенность: радиус дочернего элемента = радиус родителя − внутренний отступ
(карточка 12 → кнопка внутри 8 при отступе 4). Не смешиваем 12 и 4 в одном блоке —
используем 12 / 8 / 4 по уровням.

### 5.2 Границы

| Токен | Значение | Применение |
|---|---|---|
| `VT.Border.Hairline` | 1 px | карточки, панели, меню, разделители |
| `VT.Border.Strong` | 1 px `#364159` | hover-карточка, активный сегмент |
| `VT.Border.Focus` | 2 px `#67E8F9` | кольцо фокуса, внешний отступ 2 px |
| `VT.Border.Separator` | 1 px `#1D2331` | линии между секциями, `Margin="0,8"` |

Сглаживание: `UseLayoutRounding="True"` и `SnapsToDevicePixels="True"` на корне окна и
всех контейнерах. При DPI 125–175 % граница 1 px может попасть на полпикселя — поэтому
все границы рисуются `Border` (не `Rectangle` с `Stroke`), а отступы внутри — целые.

### 5.3 Elevation

| Уровень | Тень (DropShadowEffect) | Применение |
|---|---|---|
| `E0` | нет | карточки, панели, строки, всё в виртуализированном списке |
| `E1` | нет; hover = `Border.Strong` + `Surface2` | hover карточки |
| `E2` | `BlurRadius=24`, `ShadowDepth=6`, `Direction=270`, `Opacity=0.45`, `Color=#000` | контекстное меню, комбобокс, тултип |
| `E3` | `BlurRadius=32`, `ShadowDepth=10`, `Opacity=0.50` | тост, всплывающая панель, flyout |
| `E4` | `BlurRadius=40`, `ShadowDepth=16`, `Opacity=0.55` | модальное окно, диалог подтверждения |

**Свечение акцента** реализуется **не** `DropShadowEffect`, а двумя статичными слоями:
`Ellipse/ Border` с `VT.Grad.Halo` (радиальный, α ≤ 25 %) под кнопкой + внутренняя
верхняя подсветка `#26FFFFFF` толщиной 1 px. Причина: `Effect` перерисовывается на
каждом кадре анимации и стоит 3–8 мс на элемент.

Бюджет: не более **3** элементов с `Effect` на экране одновременно; ни одного `Effect`
внутри `ItemsControl`/`ListBox`.

---

## 6. Иконки

Только **Segoe Fluent Icons** (`FontFamily="Segoe Fluent Icons"`, fallback
`Segoe MDL2 Assets`). Никаких PNG/SVG/Path-иконок, кроме геометрии прогресса и логотипа.

| Размер | Токен | Применение |
|---|---|---|
| 12 | `VT.Icon.12` | inline в метаданных (замок приватности, трекер) |
| 14 | `VT.Icon.14` | иконка внутри чипа, компактная кнопка |
| 16 | `VT.Icon.16` | базовая: кнопки, пункты меню, статус строки |
| 20 | `VT.Icon.20` | навигация сайдбара, заголовки секций |
| 24 | `VT.Icon.24` | тулбар, шапка деталей |
| 32 | `VT.Icon.32` | пустые состояния (в круге 72) |
| 48 | `VT.Icon.48` | drag&drop оверлей |

Выравнивание: `TextBlock` с `VerticalAlignment="Center"`, `TextAlignment="Center"`,
`LineHeight` = размеру, `Margin="0,0,0,-1"` для оптической компенсации базовой линии.
Иконка всегда имеет текстовую подпись или `ToolTip` + `AutomationProperties.Name`.

**Таблица глифов.** Коды — из Segoe Fluent Icons (Windows 11). Перед релизом прогнать
визуальную проверку: отсутствующий глиф рендерится как `□`. В колонке «резерв» —
альтернативный код, если основной не отображается в целевой сборке Windows.

| Смысл | Код | Резерв |
|---|---|---|
| Логотип / торрент | `E896` (Download) | `E898` |
| Библиотека (все) | `E8F1` (Library) | `E71D` |
| Загружаются | `E896` (Download) | `E74B` |
| Раздают | `E898` (Upload) | `E74A` |
| Завершено | `E73E` (CheckMark) | `E930` |
| Пауза | `E769` (Pause) | `E71A` |
| Ошибки | `E783` (Error) | `E7BA` |
| Статистика | `E9D2` (Chart) | `E7C3` |
| Настройки | `E713` (Setting) | `E115` |
| Поиск | `E721` (Search) | `E11A` |
| Фильтр | `E71C` (Filter) | `E16E` |
| Сортировка | `E8CB` (Sort) | `E74A` |
| Обновить | `E72C` (Refresh) | `E117` |
| Добавить | `E710` (Add) | `E109` |
| Ещё / меню | `E712` (More) | `E10C` |
| Закрыть | `E711` (Cancel) | `E10A` |
| Удалить | `E74D` (Delete) | `E107` |
| Открыть папку | `E838` (FolderOpen) | `E8B7` |
| Файл | `E7C3` (Page) | `E8A5` |
| Папка | `E8B7` (Folder) | `E8DA` |
| Пиры | `E716` (People) | `E77B` |
| Трекер / глобус | `E774` (Globe) | `E909` |
| Ссылка / magnet | `E71B` (Link) | `E167` |
| Замок (приватный) | `E72E` (Lock) | `E1F6` |
| Скорость | `E916` (Stopwatch) | `E823` |
| Лимит скорости | `E9D9` | `E713` |
| DHT / сеть | `E968` | `E774` |
| Буфер обмена / вставить | `E77F` (Paste) | `E16D` |
| Чекбокс «выбрано» | `E73E` | `E10B` |
| Chevron вниз / вверх | `E70D` / `E70E` | `E011` / `E010` |
| Chevron вправо | `E76C` | `E0E3` |
| Свернуть / развернуть / закрыть окно | `E921` / `E922` / `E8BB` | `E923` (restore) |
| Информация | `E946` | `E11B` |
| Предупреждение | `E7BA` | `E1C4` |
| Успех | `E73E` | `E10B` |
| Уведомления | `E7E7` | `E91C` |
| Внешний вид | `E790` (Color) | `E771` |
| Хранилище | `E8B7` | `E7B8` |
| Копировать | `E8C8` | `E16F` |
| Сохранить | `E74E` | `E105` |

---

## 7. Компоненты

### 7.1 Кнопки

Пять визуальных вариантов. Высота/радиус/типографика — из таблицы размеров.

| Вариант | Когда | Фон normal | Текст |
|---|---|---|---|
| **Primary** | одно главное действие экрана/диалога | `VT.Grad.Accent` + halo 18 % | `Text.OnAccent`, 14 SemiBold |
| **Secondary** | альтернативные действия, «Отмена» | `Surface1` + граница `Border.Default` | `Text.Primary` |
| **Ghost** | действия в плотных строках, «Ещё» | прозрачный | `Text.Secondary` |
| **Icon** | иконка без текста (32×32 / 28×28) | прозрачный | иконка `Text.Secondary` |
| **Danger** | единственное деструктивное действие в диалоге | `Danger.Solid` | `Text.OnDanger` |
| **DangerGhost** | удаление в контекстном меню | прозрачный | `Danger`-текст `#FB7185` |

Размеры:

| Размер | Высота | Padding H | MinWidth | Радиус | Шрифт | Иконка |
|---|---|---|---|---|---|---|
| Large | 40 | 20 | 120 | 10 | 14 SemiBold | 20 |
| Default | 32 | 16 | 96 | 8 | 14 SemiBold | 16 |
| Small | 28 | 12 | 72 | 6 | 13 SemiBold | 14 |
| Icon | 32×32 | 0 | 32 | 8 | — | 16 |
| IconSmall | 28×28 | 0 | 28 | 6 | — | 14 |

Состояния (значения точные; все переходы — `Duration=120 ms`, `CubicEase EaseOut`,
если не указано иное):

| Состояние | Primary | Secondary | Ghost | Icon | Danger |
|---|---|---|---|---|---|
| **Normal** | градиент, halo 0.18, shimmer | `Surface1`, граница `#262E40` | прозрачный, текст `#AEB8CE` | прозрачный, иконка `#AEB8CE` | `#BE123C` |
| **Hover** | оверлей белый 8 %, halo 0.28 | `Surface2`, граница `#364159` | белый 6 %, текст `#E9EDF8` | белый 8 %, иконка `#E9EDF8` | `#C81E4A` |
| **Pressed** | оверлей чёрный 8 %, `Scale 0.985` (90 ms), halo 0.22, shimmer скрыт | `Surface3`, граница `#364159` | белый 10 % | белый 12 % | `#A10F35` |
| **Disabled** | `Surface3`, текст `#5A6479`, без halo/shimmer | прозрачный, граница `#1D2331`, текст `#5A6479` | текст `#5A6479` | иконка `#5A6479` | `Surface3`, текст `#5A6479` |
| **Focus (Tab)** | кольцо 2 px `#67E8F9`, отступ 2 px | то же | то же | то же | то же |

Правила:
- Иконка в кнопке — слева от текста, зазор 8; в Icon-кнопке — по центру.
- В одном диалоге ровно одна Primary; Destructive — только Danger и только в диалоге удаления.
- Кнопка не тянется на всю ширину, кроме случая «одна кнопка в пустом состоянии» (max 240).
- `Cursor="Hand"` у всех активных кнопок; у disabled — `Arrow`.
- Текст кнопки не переносится (`TextWrapping=NoWrap`), при нехватке ширины — ellipsis.

```xml
<Style x:Key="VT.Button.Primary" TargetType="Button">
  <Setter Property="Height" Value="32"/>
  <Setter Property="MinWidth" Value="96"/>
  <Setter Property="Padding" Value="16,0"/>
  <Setter Property="FontFamily" Value="{DynamicResource VT.Font.Family.Text}"/>
  <Setter Property="FontSize" Value="14"/>
  <Setter Property="FontWeight" Value="SemiBold"/>
  <Setter Property="Foreground" Value="{DynamicResource VT.Brush.Text.OnAccent}"/>
  <Setter Property="Cursor" Value="Hand"/>
  <Setter Property="FocusVisualStyle" Value="{StaticResource VT.FocusVisual}"/>
  <Setter Property="Template">
    <Setter.Value>
      <ControlTemplate TargetType="Button">
        <Grid RenderTransformOrigin="0.5,0.5">
          <Grid.RenderTransform>
            <ScaleTransform x:Name="Press" ScaleX="1" ScaleY="1"/>
          </Grid.RenderTransform>
          <!-- свечение: статичный радиальный слой, не Effect -->
          <Border x:Name="Halo" CornerRadius="12" Margin="-4"
                  Background="{DynamicResource VT.Grad.Halo}" Opacity="0.18"
                  IsHitTestVisible="False"/>
          <Border CornerRadius="8" Background="{DynamicResource VT.Grad.Accent}">
            <Border.Clip>                       <!-- скругление для shimmer: Border.ClipToBounds
                                                     НЕ скругляет углы, поэтому Geometry задаётся
                                                     конвертером Size→RectangleGeometry(RadiusX=8) -->
              <MultiBinding Converter="{StaticResource RectFromSize}">
                <Binding Path="ActualWidth"
                         RelativeSource="{RelativeSource TemplatedParent}"/>
                <Binding Path="ActualHeight"
                         RelativeSource="{RelativeSource TemplatedParent}"/>
              </MultiBinding>
            </Border.Clip>
            <Grid>
              <ContentPresenter x:Name="Content" Margin="{TemplateBinding Padding}"
                                HorizontalAlignment="Center" VerticalAlignment="Center"/>
              <Rectangle x:Name="Shimmer" Width="64" Opacity="0"
                         IsHitTestVisible="False"
                         Fill="{DynamicResource VT.Grad.Shimmer}">
                <Rectangle.RenderTransform>
                  <TransformGroup>
                    <SkewTransform AngleX="-18"/>
                    <TranslateTransform x:Name="ShimmerX" X="-120"/>
                  </TransformGroup>
                </Rectangle.RenderTransform>
              </Rectangle>
              <Border x:Name="Overlay" Background="White" Opacity="0"
                      CornerRadius="8" IsHitTestVisible="False"/>
              <Border Height="1" Margin="1,0" VerticalAlignment="Top"
                      Background="#26FFFFFF" CornerRadius="8,8,0,0"
                      IsHitTestVisible="False"/>
            </Grid>
          </Border>
        </Grid>
        <ControlTemplate.Triggers>
          <Trigger Property="IsMouseOver" Value="True">
            <Trigger.EnterActions>
              <BeginStoryboard>
                <Storyboard>
                  <DoubleAnimation Storyboard.TargetName="Overlay"
                                   Storyboard.TargetProperty="Opacity" To="0.08"
                                   Duration="0:0:0.12"/>
                  <DoubleAnimation Storyboard.TargetName="Halo"
                                   Storyboard.TargetProperty="Opacity" To="0.28"
                                   Duration="0:0:0.12"/>
                </Storyboard>
              </BeginStoryboard>
            </Trigger.EnterActions>
            <Trigger.ExitActions>
              <BeginStoryboard>
                <Storyboard>
                  <DoubleAnimation Storyboard.TargetName="Overlay"
                                   Storyboard.TargetProperty="Opacity" To="0"
                                   Duration="0:0:0.12"/>
                  <DoubleAnimation Storyboard.TargetName="Halo"
                                   Storyboard.TargetProperty="Opacity" To="0.18"
                                   Duration="0:0:0.12"/>
                </Storyboard>
              </BeginStoryboard>
            </Trigger.ExitActions>
          </Trigger>
          <Trigger Property="IsPressed" Value="True">
            <Setter TargetName="Overlay" Property="Background" Value="Black"/>
            <Setter TargetName="Overlay" Property="Opacity" Value="0.08"/>
            <Setter TargetName="Press" Property="ScaleX" Value="0.985"/>
            <Setter TargetName="Press" Property="ScaleY" Value="0.985"/>
          </Trigger>
          <Trigger Property="IsEnabled" Value="False">
            <Setter TargetName="Halo" Property="Opacity" Value="0"/>
            <Setter Property="Opacity" Value="1"/>
          </Trigger>
        </ControlTemplate.Triggers>
      </ControlTemplate>
    </Setter.Value>
  </Setter>
</Style>
```

Для disabled кнопки подменяем фон: в триггере `IsEnabled=False` ставим `Overlay.Background=#232B3D`
и `Overlay.Opacity=1`, а `Foreground=#5A6479` — при этом градиент остаётся под оверлеем и
не виден (альтернатива: отдельный `DisabledFill` поверх, `CornerRadius=8`).

### 7.2 Иконочная кнопка

32×32, радиус 8, иконка 16 по центру (`TextBlock FontFamily="Segoe Fluent Icons"`),
прозрачный фон. Hover — белый 8 %, pressed — 12 %, focus — кольцо. Обязательны `ToolTip`
и `AutomationProperties.Name`. В карточке кнопка «Ещё» появляется только при
hover/selection/focus (opacity 0→1, 120 ms) и остаётся видимой, если открыто её меню.

### 7.3 Поля ввода

| Свойство | Default | Compact | Large |
|---|---|---|---|
| Высота | 36 | 32 | 40 |
| Радиус | 8 | 6 | 8 |
| Padding H | 12 | 10 | 14 |
| Фон | `SurfaceInset` `#080A11` | то же | то же |
| Граница | 1 px `#5D6C8C` | то же | то же |
| Текст | 14/20 `Text.Primary`, `CaretBrush=#67E8F9` | 13/18 | 14/20 |
| Placeholder | `Text.Tertiary` | `Text.Tertiary` | `Text.Tertiary` |

| Состояние | Граница | Фон | Примечание |
|---|---|---|---|
| Normal | `#5D6C8C` | `#080A11` | |
| Hover | `#6B7BA0` | `#0A0D14` | |
| Focus | `#67E8F9`, 1 px + внешнее кольцо 2 px α 28 % | `#080A11` | курсор cyan, `SelectionBrush=#3367E8F9` |
| Error | `#FB7185` | `#120A0F` | под полем текст ошибки 12/16 `#FB7185`, иконка `E783` 12 |
| Disabled | `#1D2331` | `#0C0F17` | текст `#5A6479` |

Специализированные:
- **Поиск** — 320×34 (в тулбаре), иконка `E721` 16 слева (x=12), клиренс-кнопка `E711`
  20×20 справа, появляется при непустом тексте; `Esc` очищает и снимает фильтр.
- **Числовое поле (лимиты)** — текст справа, `NumeralAlignment=Tabular`, суффикс единиц
  («КБ/с», «МБ/с») 12/16 `Text.Tertiary` справа с отступом 8; шаг стрелок 64; запрет
  нецифровых символов; допустимо `0` = «без ограничения».
- **ComboBox** — как поле Default, шеврон `E70D` 12 справа; поповер `Surface2`, радиус 10,
  тень E2, пункт 34 px, радиус 6, выбранный — фон `Accent.Soft` + галочка `E73E` справа.
- **Textarea** (описание magnet-списка) — min-height 96, `AcceptsReturn`, без ресайза.

### 7.4 Чекбоксы, радио, переключатели, сегменты

| Контрол | Размер | Норма | Активен | Фокус |
|---|---|---|---|---|
| Checkbox | 18×18, радиус 4 | граница 1.5 px `#5D6C8C`, фон прозрачный | фон `#22D3EE`, галочка `E73E` 12 цвета `#071018` | кольцо 2 px offset 2 |
| Checkbox (частичный) | 18×18 | — | фон `#22D3EE`, горизонтальная черта 8×2 `#071018` | то же |
| Radio | 18×18, круг | граница 1.5 px `#5D6C8C` | граница `#22D3EE` 2 px, точка 8 px `#22D3EE` | то же |
| Toggle | трек 44×24, радиус 12 | фон `#232B3D`, граница `#5D6C8C`, thumb 18 `#F4F7FF` слева (x=3) | фон `VT.Grad.Accent`, thumb справа (x=23) | кольцо 2 px offset 2 |
| Segmented | высота 32, радиус 8, padding 2 | фон `#080A11`, сегмент прозрачный, текст `#AEB8CE` | сегмент `#232B3D`, текст `#E9EDF8`, радиус 6 | кольцо вокруг сегмента |

Анимация toggle: `TranslateTransform.X` 3→23 за 160 ms `CubicEase EaseOut`; цвет —
кроссфейд двух `Border` (фон off/on) 160 ms. Никакой анимации `Width`.

Чипы:

| Тип | Высота | Padding | Радиус | Normal | Selected |
|---|---|---|---|---|---|
| Status | 22 | 8, 0 (иконка 12 + 4 gap) | Pill | фон = цвет статуса 12 % (`α=1F`), текст = цвет статуса, граница 1 px 24 % | не выбирается |
| Category | 20 | 8, 0 | Pill | фон `#0FFFFFFF`, текст `#AEB8CE` | — |
| Filter | 26 | 10, 0 | Pill | граница `#262E40`, текст `#AEB8CE` | фон `Accent.Soft`, граница `#669D7BFA`, текст `#E9EDF8` |
| Клавиша (хоткей) | 18 | 6, 0 | 4 | фон `#14FFFFFF`, текст `#8A95AD`, 11/14 | — |

Статус-чип всегда содержит иконку (не только цвет): `E896` загрузка, `E898` раздача,
`E769` пауза, `E73E` завершено, `E783` ошибка, `E916` проверка, `E774` метаданные.

### 7.5 Карточка торрента

Анатомия (слева направо), высота 76, радиус 12, фон `Surface1`, граница 1 px `#262E40`,
padding `16,0`:

```
┌──┬───────────────────────────────────────────┬───────────────┬────────────┬────┐
│20│ Имя торрента.ubuntu-24.04.iso   [чипы]    │        62 %   │ ↓ 7,8 МБ/с │ 32 │
│  │ 24,8 ГБ из 40,0 ГБ · 12 трекеров          │ ▓▓▓▓▓▓▓░░░░░  │ ↑ 1,2 МБ/с │    │
│  │ добавлен 12 окт, 14:05                    │  ≈ 24 мин     │            │    │
└──┴───────────────────────────────────────────┴───────────────┴────────────┴────┘
   ↑ статус-иконка 16             ↑ прогресс 140             ↑ скорости 116   ↑ «Ещё»
```

Колонки (`Grid.ColumnDefinitions`, 1440 → ширина карточки 759, контент 727):
`20 | 12 | * (min 240) | 16 | 140 | 16 | 116 | 12 | 32` → имя получает 363 DIP.
При ширине окна < 1360: `20 | 12 | * | 12 | 120 | 12 | 100 | 8 | 32` (имя 323 при 1280).
При ≥ 1680: `20 | 12 | * | 16 | 180 | 16 | 140 | 12 | 32` (имя 711 при 1920).

| Зона | Содержимое | Типографика |
|---|---|---|
| Статус | иконка 16 цвета статуса, по центру | — |
| Имя | название в одну строку + чипы (статус, категория, замок приватности) в той же строке справа | 14/20 SemiBold `Text.Primary`; чипы 20 |
| Метаданные | `24,8 ГБ из 40,0 ГБ · 12 трекеров · 12 окт` | 12/16 `Text.Tertiary`, разделитель `·` |
| Прогресс | процент 13/18 SemiBold справа; бар 6 px; ETA 11/14 `Text.Tertiary` справа | tabular |
| Скорости | `↓ 7,8 МБ/с` (13/18), `↑ 1,2 МБ/с` (12/16), выравнивание вправо, иконки 12 | tabular |
| Действие | IconSmall `E712` 28×28 | — |

Состояния карточки:

| Состояние | Фон | Граница | Дополнительно |
|---|---|---|---|
| Normal | `Surface1` | `#262E40` | действия скрыты (opacity 0) |
| Hover | `Surface2` | `#364159` | действия opacity 1; переход 120 ms |
| Selected | `Surface1` + `Accent.Soft` (10 %) | `#3E4C6B` | левая полоса 3×44, `VT.Grad.Accent.Bar`, radius 2; текст имени `#FFFFFF` |
| Selected + hover | то же + белый 3 % | `#4A5B80` | |
| Focus (Tab) | как selected/hover | + кольцо 2 px `#67E8F9`, offset −2 | |
| Error | `Surface1` | `#4A2A33` | полоса 3×76 `#FB7185`; метаданные заменяются текстом ошибки `#FB7185` |
| Paused | `Surface1` | `#262E40` | бар `#364159` (без градиента), скорости «—», иконка `E769` |
| Metadata | `Surface1` | `#262E40` | имя «Получение метаданных…», indeterminate-бар (60 % ширины, sweep), ETA «—» |
| Checking | `Surface1` | `#262E40` | бар `#A78BFA` indeterminate, метаданные «Проверка файлов: 62 %» |
| Seeding | `Surface1` | `#2C3A34` | бар `Seed`-градиент, процент 100 %, скорости только ↑ |
| Disabled (bulk) | `Surface1` `Opacity=0.55` | `#262E40` | без hover и меню |

Правила:
- Карточка — `ListBoxItem` с `SnapsToDevicePixels`, без `Effect`, без анимации геометрии.
- Двойной клик — «Открыть папку с файлами» (настраивается); `Enter` — то же.
- Hover не поднимает карточку (`TranslateTransform` запрещён — смазывает текст).
- Все скорости/размеры обновляются не чаще 2 раз в секунду одним пакетным обновлением
  (см. ux-spec, §21).

```xml
<DataTemplate x:Key="VT.TorrentCard">
  <Border Height="76" CornerRadius="12" Padding="16,0"
          Background="{DynamicResource VT.Brush.Surface1}"
          BorderBrush="{DynamicResource VT.Brush.BorderDefault}"
          BorderThickness="1" SnapsToDevicePixels="True">
    <Grid>
      <Grid.ColumnDefinitions>
        <ColumnDefinition Width="20"/>
        <ColumnDefinition Width="12"/>
        <ColumnDefinition Width="*" MinWidth="240"/>
        <ColumnDefinition Width="16"/>
        <ColumnDefinition Width="140"/>
        <ColumnDefinition Width="16"/>
        <ColumnDefinition Width="116"/>
        <ColumnDefinition Width="12"/>
        <ColumnDefinition Width="32"/>
      </Grid.ColumnDefinitions>

      <TextBlock Grid.Column="0" FontFamily="Segoe Fluent Icons" FontSize="16"
                 VerticalAlignment="Center" Text="{Binding StatusGlyph}"
                 Foreground="{Binding StatusBrush}"/>

      <StackPanel Grid.Column="2" VerticalAlignment="Center">
        <DockPanel LastChildFill="True">
          <ItemsControl DockPanel.Dock="Right" ItemsSource="{Binding Chips}"
                        VerticalAlignment="Center" Margin="8,0,0,0">
            <ItemsControl.ItemsPanel>
              <ItemsPanelTemplate><StackPanel Orientation="Horizontal"/></ItemsPanelTemplate>
            </ItemsControl.ItemsPanel>
          </ItemsControl>
          <TextBlock Text="{Binding Name}" TextTrimming="CharacterEllipsis"
                     TextWrapping="NoWrap" FontSize="14" FontWeight="SemiBold"
                     Foreground="{DynamicResource VT.Brush.TextPrimary}"/>
        </DockPanel>
        <TextBlock Text="{Binding MetaLine}" Margin="0,4,0,0"
                   FontSize="12" LineHeight="16" TextTrimming="CharacterEllipsis"
                   Foreground="{DynamicResource VT.Brush.TextTertiary}"/>
      </StackPanel>

      <StackPanel Grid.Column="4" VerticalAlignment="Center">
        <TextBlock Text="{Binding PercentText}" HorizontalAlignment="Right"
                   FontSize="13" FontWeight="SemiBold" LineHeight="18"
                   Typography.NumeralAlignment="Tabular"
                   Foreground="{DynamicResource VT.Brush.TextPrimary}"/>
        <ProgressBar Style="{StaticResource VT.ProgressCard}" Value="{Binding Progress}"
                     Margin="0,4,0,0"/>
        <TextBlock Text="{Binding EtaText}" HorizontalAlignment="Right" Margin="0,2,0,0"
                   FontSize="11" LineHeight="14"
                   Foreground="{DynamicResource VT.Brush.TextTertiary}"/>
      </StackPanel>

      <StackPanel Grid.Column="6" VerticalAlignment="Center">
        <TextBlock Text="{Binding DownText}" HorizontalAlignment="Right"
                   FontSize="13" LineHeight="18" Typography.NumeralAlignment="Tabular"
                   Foreground="{DynamicResource VT.Brush.TextSecondary}"/>
        <TextBlock Text="{Binding UpText}" HorizontalAlignment="Right" Margin="0,2,0,0"
                   FontSize="12" LineHeight="16" Typography.NumeralAlignment="Tabular"
                   Foreground="{DynamicResource VT.Brush.TextTertiary}"/>
      </StackPanel>

      <Button Grid.Column="8" Style="{StaticResource VT.Button.IconSmall}"
              Content="&#xE712;" ToolTip="Действия"
              AutomationProperties.Name="Действия с торрентом"
              Visibility="{Binding IsActionsVisible, Converter={StaticResource BoolToVisibility}}"/>
    </Grid>
  </Border>
</DataTemplate>
```

Выбранное состояние и hover реализуются на уровне `ListBoxItem` (`ItemContainerStyle`),
а не внутри шаблона: фон/граница/полоса — через триггеры `IsSelected`, `IsMouseOver`.

### 7.6 Прогресс-бары

| Свойство | Card | Compact | Details | Hero (кольцо) |
|---|---|---|---|---|
| Высота | 6 | 4 | 8 | 96 (диаметр) |
| Радиус | 3 | 2 | 4 | — |
| Трек | `#1E2534` | `#1E2534` | `#1E2534` | `#232B3D`, толщина 8 |
| Заполнение | `Grad.Progress.Download` | то же | то же | `Grad.Accent` по дуге |
| Верхняя подсветка | 1 px `#14FFFFFF` | — | 1 px | — |
| Анимация значения | `ScaleX`, 220 ms, `QuadraticEase EaseOut` | 180 ms | 260 ms | `StrokeDashOffset` 400 ms |

Правила:
- Заполнение — `Rectangle` с `RenderTransformOrigin="0,0.5"` и `ScaleTransform.ScaleX`;
  `Width` не анимируется (ломает `Measure` и вызывает перерасчёт списка).
- Indeterminate — блок 40 % ширины, `TranslateTransform.X` −40 %→140 %, 1400 ms, `Linear`,
  `RepeatBehavior=Forever`. Только для статусов «Метаданные» и «Проверка», максимум 5
  одновременно видимых; на остальных строках — статичное заполнение 15 %.
- «Пауза» — плоский `#364159`, без анимации и градиента.
- «Ошибка» — `#FB7185` с `Opacity=0.6` на последнем известном значении.
- «Раздача» — `#A78BFA → #F472B6`; «Завершено, не раздаётся» — `#94A3B8`.
- Доступность: `AutomationProperties.Name="Прогресс"`, `AutomationProperties.HelpText`
  = «62 процента, осталось 24 минуты»; `ProgressBar` не фокусируется.

### 7.7 Скроллбар

| Параметр | Значение |
|---|---|
| Ширина дорожки | 10 DIP (горизонталь — высота 10) |
| Thumb | ширина 6, радиус 3, центрируется (margin 2) |
| Thumb normal | `#364159` |
| Thumb hover | `#4A5773` |
| Thumb drag | `#5D6C8C` |
| Мин. длина thumb | 32 |
| Дорожка | прозрачная (не рисуется) |
| Стрелки | отсутствуют |
| Auto-hide | через 1200 ms после последнего скролла — `Opacity 1→0` за 180 ms; появляется при hover списка, скролле, `KeyboardFocusWithin` |
| Поведение | `PanningMode="VerticalOnly"` (тач), `IsDeferredScrollingEnabled="False"` |

Скроллбар — оверлейный: `Grid` со списком и скроллбаром в одной ячейке, скроллбар
выровнен вправо, `Margin="0,4,2,4"`, чтобы не отнимать ширину у карточек.

### 7.8 Тултип

| Параметр | Значение |
|---|---|
| Фон | `#1B2130`, `Opacity` 0.98 |
| Граница | 1 px `#364159` |
| Радиус | 8 |
| Padding | 10, 6 |
| Тень | E2 |
| Шрифт | 12/16 `Text.Primary` (заголовок 12 SemiBold), максимум 2 строки |
| MaxWidth | 320 |
| Задержка | 600 ms (`SystemParameters.MouseHoverTime`) |
| Появление | `Opacity 0→1` + `TranslateY 4→0`, 120 ms `CubicEase EaseOut` |
| Позиция | снизу по центру, отступ 8; при нехватке места — сверху/сбоку |
| Хоткей | справа, чип 18 px, `Margin="8,0,0,0"` |

Содержимое rich-тултипа: заголовок 12 SemiBold + пояснение 12 `Text.Secondary` +
опциональный чип клавиши. Тултип обязателен для всех иконочных кнопок и усечённых имён.

### 7.9 Тосты

| Параметр | Значение |
|---|---|
| Ширина | 360 (min 320, max 420) |
| Позиция | правый нижний угол, отступ 16 от края окна и 16 от bulk-бара |
| Радиус | 12, фон `#1B2130` (α 98 %), граница 1 px `#364159`, тень E3 |
| Акцент | полоса слева 3 px цветом статуса, вся высота |
| Padding | 14, 12 |
| Иконка | 20, цвет статуса, слева |
| Заголовок | 13/18 SemiBold `Text.Primary` |
| Текст | 12/16 `Text.Secondary`, max 2 строки |
| Действие | Ghost Small («Открыть папку», «Повторить», «Отменить») справа внизу |
| Закрытие | IconSmall `E711` 24×24 в правом верхнем углу |
| Появление | `Opacity 0→1`, `TranslateY 12→0`, 220 ms `CubicEase EaseOut` |
| Исчезновение | `Opacity 1→0`, `TranslateY 0→6`, 180 ms `CubicEase EaseIn` |
| Автоскрытие | успех 4000 ms, информация 5000 ms, ошибка 8000 ms; тост с действием «Отменить» — 10 000 ms (sticky-ошибки не исчезают) |
| Стек | максимум 3, зазор 8, новые снизу; 4-й вытесняет самый старый |
| Пауза | таймер останавливается при hover и при `Window.IsActive=false` |

### 7.10 Модальные окна и диалоги

| Параметр | Значение |
|---|---|
| Ширина | 560 (подтверждение), 640 (добавление торрента) |
| Радиус | 16, фон `Surface1` `#151A26`, граница 1 px `#364159`, тень E4 |
| Скрим | `VT.Grad.ScrimDialog`, клик по скриму = «Отмена» (кроме сценария с несохранёнными данными) |
| Padding | 24 |
| Шапка | заголовок 20/28 SemiBold + подзаголовок 13/18 `Text.Secondary`; close 32×32 справа сверху |
| Футер | высота 72, верхняя граница 1 px `#1D2331`, кнопки справа, зазор 8, «Отмена» левее главной |
| Появление | скрим `Opacity 0→0.72` 180 ms; окно `Opacity 0→1` + `Scale 0.98→1` + `TranslateY 8→0`, 220 ms `CubicEase EaseOut` |
| Исчезновение | 160 ms `CubicEase EaseIn` |
| Фокус | захват фокуса, `Tab` цикличен внутри, `Esc` = «Отмена», `Enter` = главное действие |
| Затемнение фона | контент за скримом не анимируется и не скроллится |

### 7.11 Вкладки, навигация, таблицы, скелетоны, меню

**Вкладки панели деталей.** Высота 44, текст 13 SemiBold, `Text.Secondary` в покое,
`Text.Primary` активна; индикатор — 2 px `VT.Grad.Accent.Bar` под активной вкладкой,
переезжает `TranslateTransform.X` 180 ms `CubicEase EaseOut` (не анимируем `Width`).
Счётчик на вкладке — чип 18 px `#0FFFFFFF`.

**Пункт навигации сайдбара.** Высота 40, радиус 8, padding 12, иконка 20 + зазор 12 +
текст 14 SemiBold. Normal: текст `Text.Secondary`, иконка `Text.Tertiary`.
Hover: фон `#0FFFFFFF`, текст `Text.Primary`. Selected: фон `Accent.Soft`, текст
`#FFFFFF`, левая полоса 3×20 `Accent.Bar`, иконка `#67E8F9`. Бейдж-счётчик справа —
чип 20 px, фон `#1FFFFFFF`, текст 11 SemiBold. Transition 120 ms.

**Строки таблиц (Файлы / Пиры / Трекеры).** Header 28, текст 12 SemiBold UPPER
`Text.Tertiary`; строка файлов 32, пиров/трекеров 40; hover `#0AFFFFFF`; selected
`Accent.Soft`; разделителей нет, вместо них — зазор 2 и радиус 6 у hover-подложки.
Числовые колонки — вправо, tabular.

**Скелетон.** Прямоугольник с радиусом конечного контента, фон `Surface1`, поверх —
`Grad.Shimmer` (`TranslateTransform.X`, 1600 ms, `Linear`, forever, α 0.12). Первый
экран: 6 строк по 76 с зазором 8. Скелетон показывается только при первом запуске и при
загрузке > 150 ms, максимум 3 s, затем — состояние ошибки.

**Спиннер.** Кольцо 20×20, толщина 2, трек `#232B3D`, дуга 90° `#67E8F9`,
вращение 900 ms `Linear` forever. Только внутри кнопок и диалогов; в списке — скелетон.

**Контекстное меню.** Ширина min 220 / max 360, padding 6, радиус 12, фон `#1B2130`,
граница `#364159`, тень E2. Пункт: 32, радиус 8, padding 10/12, иконка 16 + зазор 10,
текст 13, хоткей справа 11 `Text.Tertiary`. Hover — `#14FFFFFF`. Separator — 1 px
`#1D2331` с `Margin="6,4"`. Опасный пункт — текст `#FB7185`, hover `#26FB7185`.
Открытие: `Opacity 0→1` + `TranslateY 4→0`, 140 ms; закрытие 100 ms. Подменю — шеврон
`E76C`, задержка открытия 250 ms наведением.

---

## 8. Motion

### 8.1 Токены длительности

| Токен | Значение | Применение |
|---|---|---|
| `VT.Dur.Instant` | `0:0:0.08` | смена цвета границы, «нажатие» |
| `VT.Dur.Fast` | `0:0:0.12` | hover/focus, появление пункта меню, тултипа |
| `VT.Dur.Base` | `0:0:0.18` | появление вкладки, скрытие тоста, смена контента |
| `VT.Dur.Medium` | `0:0:0.22` | появление тоста/модалки/скелетона |
| `VT.Dur.Slow` | `0:0:0.28` | раскрытие секции настроек, панель деталей |
| `VT.Dur.Panel` | `0:0:0.36` | крупные переходы (страница настроек) |
| `VT.Dur.Toast` | `0:0:4.0` / `5.0` / `8.0` | автоскрытие (успех/инфо/ошибка) |
| `VT.Dur.ToastHover` | таймер на паузе | пока курсор над тостом |
| `VT.Dur.Sweep` | `0:0:1.2` | один проход блика shimmer |
| `VT.Dur.Shimmer` | `0:0:5.2` | полный цикл shimmer (sweep + пауза) |
| `VT.Dur.Indeterminate` | `0:0:1.4` | цикл indeterminate-прогресса |
| `VT.Dur.Skeleton` | `0:0:1.6` | цикл блика скелетона |
| `VT.Dur.Spinner` | `0:0:0.9` | оборот спиннера |

### 8.2 Кривые

| Токен | Класс | Параметры | Применение |
|---|---|---|---|
| `VT.Ease.Standard` | `CubicEase` | `EasingMode="EaseInOut"` | смена цвета/фона, перемещение индикатора |
| `VT.Ease.Enter` | `CubicEase` | `EasingMode="EaseOut"` | появление элементов (быстрый старт, мягкое торможение) |
| `VT.Ease.Exit` | `CubicEase` | `EasingMode="EaseIn"` | исчезновение |
| `VT.Ease.Emphasis` | `BackEase` | `Amplitude="0.12"`, `EasingMode="EaseOut"` | модалка, тост (едва заметный «перелёт») |
| линейно | без `EasingFunction` | — | shimmer, indeterminate, спиннер, прогресс |

`LinearEase` в WPF не существует — для линейного движения `EasingFunction` не задаётся.
`ElasticEase`/`BounceEase` запрещены.

```xml
<CubicEase x:Key="VT.Ease.Enter" EasingMode="EaseOut"/>
<Duration x:Key="VT.Dur.Fast">0:0:0.12</Duration>
```

### 8.3 Что именно анимируется

| Элемент | Свойство | Длительность | Кривая | Примечание |
|---|---|---|---|---|
| Кнопка (hover) | `Overlay.Opacity` 0→0.08, `Halo.Opacity` 0.18→0.28 | 120 ms | Enter | не анимируем цвет кисти |
| Кнопка (press) | `ScaleTransform` 1→0.985 | 90 ms | Exit | обратно — 120 ms |
| Карточка (hover) | `Background`/`BorderBrush` (ColorAnimation) | 120 ms | Standard | `SolidColorBrush` не заморожен |
| Карточка (selected) | `Accent.Soft` overlay `Opacity` 0→1 | 120 ms | Standard | полоса появляется с `ScaleY` 0.6→1, 140 ms |
| Кнопка «Ещё» в карточке | `Opacity` 0→1 | 120 ms | Enter | только при hover/selection |
| Вкладка деталей | индикатор `TranslateTransform.X` | 180 ms | Enter | ширина индикатора меняется мгновенно |
| Смена контента вкладки | `Opacity` 0→1 | 140 ms | Enter | без слайда |
| Навигация сайдбара | фон/текст ColorAnimation + полоса `ScaleY` | 120 ms | Standard | |
| Появление поповера/меню | `Opacity` + `TranslateY` 4→0 | 140 ms | Enter | |
| Закрытие меню | `Opacity` | 100 ms | Exit | |
| Тултип | `Opacity` + `TranslateY` 4→0 | 120 ms | Enter | |
| Модалка | скрим `Opacity` 180 ms; окно `Opacity` + `Scale 0.98→1` + `TranslateY 8→0` | 220 ms | Emphasis | `ScaleTransform`, не `Width` |
| Тост | `Opacity` + `TranslateY 12→0` | 220 ms | Enter / 180 ms Exit | |
| Аккордеон настроек | `Opacity` + `TranslateY 6→0`, высота — мгновенно | 180 ms | Enter | не анимируем `Height` |
| Прогресс (значение) | `ScaleTransform.ScaleX` | 220 ms | QuadraticEase EaseOut | обновление ≤ 2 Гц |
| Indeterminate | `TranslateTransform.X` | 1400 ms, forever | линейно | ≤ 5 одновременно |
| Shimmer CTA | `TranslateTransform.X` + `Opacity` | 1200 ms в цикле 5200 ms | линейно | 1–2 элемента на экран |
| Скелетон | `TranslateTransform.X` | 1600 ms, forever | линейно | только первый экран |
| Спиннер | `RotateTransform.Angle` 0→360 | 900 ms, forever | линейно | |
| Тост (таймер) | — | 4/5/8 s | — | пауза при hover |
| Сворачивание деталей | контент `Opacity` + `TranslateX 8→0` | 180 ms | Enter | `GridLength` меняется мгновенно |

**Никогда не анимируем:** `Width`, `Height`, `Margin`, `Padding`, `FontSize`,
`GridLength`, `Top/Left` (кроме `Canvas`-оверлеев), `Visibility` (только дискретно),
`DropShadowEffect.BlurRadius`, `Clip`, а также `LayoutTransform` внутри скролла.

### 8.4 Shimmer: точная спецификация

Назначение — «переливание» акцентной поверхности. Не блик по тексту, а медленный
проход света по фону. Разрешён **только** на: primary-кнопке главного действия экрана,
логотипе, активном пункте навигации. Максимум 2 элемента на экран.

Геометрия: полоса шириной 64 DIP, `SkewTransform AngleX="-18"`, старт `X=-120`,
финиш `X=+220` (для кнопки шириной ~160). Клип — по скруглённому контуру кнопки
(`RectangleGeometry` через конвертер `RectFromSize`, т.к. `ClipToBounds` не скругляет).

Тайминг: цикл 5,2 s. Проход 1,2 s (23 %), пауза 4,0 s (77 %). Пиковая прозрачность 0,22.
Такая пауза делает эффект «дорогим», а не мигающим.

```xml
<Storyboard x:Key="VT.Shimmer" RepeatBehavior="Forever">
  <DoubleAnimationUsingKeyFrames Storyboard.TargetName="ShimmerX"
                                 Storyboard.TargetProperty="X">
    <LinearDoubleKeyFrame KeyTime="0:0:0"      Value="-120"/>
    <LinearDoubleKeyFrame KeyTime="0:0:1.2"    Value="220"/>
    <LinearDoubleKeyFrame KeyTime="0:0:5.2"    Value="220"/>
  </DoubleAnimationUsingKeyFrames>
  <DoubleAnimationUsingKeyFrames Storyboard.TargetName="Shimmer"
                                 Storyboard.TargetProperty="Opacity">
    <LinearDoubleKeyFrame KeyTime="0:0:0"      Value="0"/>
    <LinearDoubleKeyFrame KeyTime="0:0:0.2"    Value="0.22"/>
    <LinearDoubleKeyFrame KeyTime="0:0:1.0"    Value="0.22"/>
    <LinearDoubleKeyFrame KeyTime="0:0:1.2"    Value="0"/>
    <LinearDoubleKeyFrame KeyTime="0:0:5.2"    Value="0"/>
  </DoubleAnimationUsingKeyFrames>
</Storyboard>
```

Управление:
- `Begin(this, isControllable: true)` при `Loaded`; `Pause` при `Window.Deactivated` и при
  `MotionLevel != Full`; `Resume` при `Activated`.
- При `IsMouseOver` прозрачность пика снижается до 0,14 (кнопка «успокаивается» под курсором).
- Shimmer не запускается, если кнопка `IsEnabled=False` или элемент вне видимой области
  (`IsVisibleChanged`).
- Одновременно в дереве не более 2 storyboard'ов shimmer.

### 8.5 Бюджет производительности

| Ограничение | Значение |
|---|---|
| Анимируемых свойств на элемент | ≤ 2 |
| Анимируемых элементов в кадре | ≤ 12 |
| `Effect` на экране | ≤ 3, ни одного внутри `ItemsControl` |
| Частота обновления данных из движка | ≤ 2 Гц, пакетно, один `Dispatcher`-проход |
| Бюджет кадра | 16,7 ms; цель — ≤ 8 ms на UI-потоке |
| Строк в виртуализированном списке | только видимые + 2 буфера (`VirtualizationMode="Recycling"`) |
| Анимации при скролле | отсутствуют (hover-переходы подавляются на время инерционного скролла) |
| Долгие анимации при `WindowState=Minimized` | останавливаются |

### 8.6 Уменьшение движения

Настройка `Настройки → Внешний вид → Анимации`: **Полные / Уменьшенные / Выключены**
(по умолчанию — «Полные», но при `SystemParameters.ClientAreaAnimation == false`
автоматически «Уменьшенные»).

| Режим | Что меняется |
|---|---|
| Полные | всё по §8.3 |
| Уменьшенные | shimmer off, halo без пульсации, тост/модалка только fade 120 ms, hover без transition, прогресс обновляется без анимации значения |
| Выключены | только мгновенная смена состояний; остаётся статичное заполнение прогресса |

---

## 9. Доступность (WCAG 2.1 AA)

### 9.1 Контраст

Проверено расчётом по формуле относительной яркости; значения — минимум по всем
поверхностям, на которых токен применяется.

| Пара | Контраст | Требование | Итог |
|---|---|---|---|
| `Text.Primary` `#E9EDF8` на `Surface1` `#151A26` | 14,85 | ≥ 4,5 | ✔ AAA |
| `Text.Primary` на `Surface3` `#232B3D` | 12,08 | ≥ 4,5 | ✔ AAA |
| `Text.Secondary` `#AEB8CE` на `Surface3` | 7,11 | ≥ 4,5 | ✔ AAA |
| `Text.Tertiary` `#8A95AD` на `Surface3` | 4,70 | ≥ 4,5 | ✔ AA |
| `Text.OnAccent` `#071018` на стопе `#9D7BFA` | 6,06 | ≥ 4,5 | ✔ AA (минимум по градиенту) |
| `Text.OnAccent` на стопе `#22D3EE` | 10,60 | ≥ 4,5 | ✔ AAA |
| `Text.OnAccent` на стопе `#F472B6` | 7,23 | ≥ 4,5 | ✔ AAA |
| `Text.OnDanger` `#FFFFFF` на `#BE123C` | 6,29 | ≥ 4,5 | ✔ AA |
| Статус `#FB7185` на `Surface3` | 5,26 | ≥ 4,5 | ✔ AA |
| Статус `#A78BFA` на `Surface3` | 5,20 | ≥ 4,5 | ✔ AA |
| Статус `#22D3EE` на `Surface3` | 7,83 | ≥ 4,5 | ✔ AAA |
| Кольцо фокуса `#67E8F9` на `Surface3` | 9,76 | ≥ 3,0 | ✔ |
| Граница поля `#5D6C8C` на `#0C0F17` | 3,64 | ≥ 3,0 | ✔ (граница — часть идентификации контрола) |
| `Text.Disabled` `#5A6479` | 2,38–3,33 | не применяется | Disabled-элементы исключены из 1.4.3; текст в них не несёт информации |

Запрещено: серый текст на градиенте, текст `Text.Tertiary` на `Surface2` мельче 12 px
(4,64 — запас всего 0,14), любой текст на `Text.OnAccent`-плашке не цвета `#071018`.

### 9.2 Фокус

- Кольцо: 2 px `#67E8F9`, отступ 2 px, радиус = радиус контрола + 2; рисуется
  `FocusVisualStyle` (не `Border` внутри шаблона), поэтому не виден при клике мышью и
  виден при `Tab`.
- Внутри списка фокус на строке — кольцо с отрицательным отступом (−2), чтобы не
  перекрывать соседнюю строку.
- `FocusVisualStyle` не равен `null` ни у одного интерактивного элемента; кастомный
  `FocusVisualStyle` не отключает `KeyboardNavigation`.
- При открытии модалки фокус ставится на первый интерактивный элемент (для «Добавить
  торрент» — поле magnet); при закрытии возвращается на кнопку-источник.
- При обновлении списка фокус сохраняется по `InfoHash`, а не по индексу.

### 9.3 Клавиатура и семантика

| Требование | Реализация |
|---|---|
| Полная навигация без мыши | `Tab`/`Shift+Tab`, стрелки в списке, `Enter`/`Space`, контекстное меню по `Shift+F10` или клавише меню |
| Порядок обхода | тайтлбар → сайдбар → тулбар → фильтры → список → детали |
| Видимый фокус | всегда, см. §9.2 |
| Имена для скринридера | `AutomationProperties.Name` у всех иконочных кнопок; `AutomationProperties.HelpText` у прогресса и статусов |
| Живые регионы | тост — `AutomationProperties.LiveSetting="Polite"`; критическая ошибка — `"Assertive"` |
| Статус не только цветом | иконка + текст в чипе; у карточки — `StatusGlyph` + подпись статуса в тултипе |
| Минимальная цель | 32×32 DIP для всех интерактивных элементов; строка списка 76 |
| Прокрутка колесом над списком | не перехватывается, если курсор над панелью деталей |
| Таймауты | тост с ошибкой не исчезает, пока не закрыт или пока не истекут 8 s; hover ставит таймер на паузу |
| Мигание | нет миганий чаще 3 Гц; shimmer — 1 проход в 5,2 s |

### 9.4 Высокий контраст

Словарь `Themes/HighContrast.xaml`, подключаемый при `SystemParameters.HighContrast`:
все `VT.Brush.*` переопределяются на `SystemColors` (`WindowBrush`, `WindowTextBrush`,
`HighlightBrush`, `GrayTextBrush`, `HotTrackBrush`); градиенты заменяются
`SystemColors.HighlightBrush`; `Effect` отключается; границы — 1 px `WindowTextBrush`;
статусы — только иконка + текст без цвета. Проверка: при HC ни один элемент не остаётся
с прозрачным фоном и невидимой границей.

### 9.5 Масштабирование

- Системный DPI 100–200 % поддерживается автоматически (все размеры в DIP).
- Масштаб текста Windows (Параметры → Специальные возможности → Размер текста) в v1
  осознанно не поддерживается: WPF не масштабирует шрифты автоматически, а глобальный
  `LayoutTransform` ломает виртуализацию и попадание в пиксели. Вместо него — пресеты
  плотности «Обычная / Компактная» и системный DPI.
- Минимальный размер окна 1024×640 DIP — при 200 % на экране 1920×1080 доступно
  960×540, поэтому окно должно корректно работать в свёрнутом режиме сайдбара
  (см. ux-spec, §20).

---

## 10. Сводный индекс токенов

| Группа | Префикс | Примеры ключей |
|---|---|---|
| Цвета | `VT.Color.` | `Bg.Window`, `Bg.Canvas`, `Bg.Panel`, `Surface1..3`, `SurfaceInset`, `Track`, `Border.Subtle/Default/Strong/Interactive/Focus`, `Text.Primary/Secondary/Tertiary/Disabled/OnAccent/OnDanger`, `Status.Downloading…Error`, `Danger.Solid/SolidHover/SolidPressed/Soft`, `Highlight.White06/White08/Black08`, `Scrim` |
| Кисти | `VT.Brush.` | те же имена, что у цветов (1:1), плюс `VT.Brush.Transparent` |
| Градиенты | `VT.Grad.` | `Accent`, `Accent.Soft`, `Accent.Bar`, `Progress.Download`, `Progress.Seed`, `Aurora`, `Halo`, `Shimmer`, `FadeBottom`, `ScrimDialog` |
| Шрифты | `VT.Font.` | `Family.Small/Text/Display`, `Display`, `H1`, `H2`, `H3`, `Body`, `BodyStrong`, `BodySmall`, `BodySmallStrong`, `Caption`, `CaptionStrong`, `Micro` |
| Отступы | `VT.Space.` | `05, 1, 15, 2, 3, 4, 5, 6, 8, 10, 12, 16` |
| Радиусы | `VT.Radius.` | `05, 1, 15, 2, 25, 3, 4, 5, Pill` |
| Размеры | `VT.Size.` | `TitleBar=40`, `Sidebar=248`, `Details=384`, `Toolbar=64`, `FilterRow=36`, `ListHeader=32`, `Card=76`, `CardCompact=56`, `CardGap=8`, `Gutter=24` |
| Иконки | `VT.Icon.` | `12, 14, 16, 20, 24, 32, 48` |
| Тени | `VT.Shadow.` | `E2`, `E3`, `E4` (готовые `DropShadowEffect`, `x:Shared="False"`) |
| Движение | `VT.Dur.`, `VT.Ease.` | см. §8.1–8.2 |

Правило именования: `VT.<Категория>.<Смысл>`; никаких `Gray1`, `BlueButton`, `MainColor`.

---

## 11. Анти-паттерны

1. **Градиент везде.** Градиентный фон карточки + градиентная кнопка + градиентный
   заголовок в одном экране — визуальный шум. Максимум один акцент в регионе.
2. **Свечение через `DropShadowEffect`** на анимируемом элементе — просадка FPS.
   Только статичный `Grad.Halo` + внутренняя подсветка.
3. **Анимация `Width`/`Height`** прогресса, панели, аккордеона — перерасчёт layout на
   каждом кадре, дёргание виртуализированного списка.
4. **Пульсирующие статусы в каждой строке.** 200 бесконечных storyboard'ов убивают
   скролл. Живость показываем градиентом и обновлением цифр, а не миганием.
5. **Тень у карточек в списке.** 20 `Effect` в кадре. Карточки — граница + светлота.
6. **Цвет как единственный статус.** Красная точка без иконки и текста нечитаема для
   8 % мужчин и для скринридера.
7. **Мелкий серый текст** (`#5A6479` 11 px) для важных подписей — ниже AA.
8. **Свой шрифт-иконочный набор / PNG-иконки** — запрещены; только Segoe Fluent Icons.
9. **Резкие появления** (`Visibility` без fade) и «прыгающие» меню — воспринимаются как
   баг, а не как скорость.
10. **Модалка на каждое действие.** Пауза, приоритет файла, копирование magnet — без
    диалогов, только тост с «Отменить».
11. **Скроллбар, отнимающий ширину** у карточек (скачок layout при появлении).
    Только оверлейный.
12. **Разные радиусы в одном блоке** (12 у карточки, 2 у вложенной кнопки) — «дешёвый»
    вид. Вложенность = радиус родителя − отступ.

---

*Конец дизайн-системы. Экраны, состояния, тексты и адаптивность — в
[ux-spec.md](./ux-spec.md).*
