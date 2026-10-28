# ZTorrent — финальное предметное ревью WPF-интерфейса

> **Статус:** отчёт исторический. В текущем коде закрыты: стабильные коллекции деталей,
> graceful persistence при `SessionEnding`, ретрай single-instance, подсветка навигации,
> восстановление Maximized, скрытый старт без мигания, focus scope модалок, защита
> drag&drop в текстовых полях, авто-скрытие панели только при пересечении breakpoint,
> ошибка настроек в футере, клавиатурные чекбоксы; мёртвый `IsSidebarCollapsed` удалён.

Scope: `src/ZTorrent.App`, .NET 10 WPF, Win11. Исходники не менялись. Только дефекты, видимые пользователю.

## Дефекты

### 1. HIGH — Списки в панели деталей сбрасываются каждые 2 с
`ViewModels/TorrentDetailsViewModel.cs:68-72`, `Core/Services/TorrentEngineService.cs:663`, `Views/TorrentDetailsView.xaml:330,438`
**Симптом:** на вкладках «Файлы»/«Пиры» прокрутка отскакивает в начало и выделение снимается каждые ~2 с; списком на 300+ файлов пользоваться нельзя.
**Причина:** `DetailsLoopAsync` (`MainViewModel.cs:1251-1265`) каждые 2 с делает `Details.Update()` → `OnPropertyChanged(string.Empty)`; `Files/Peers/Trackers` берутся из нового снапшота (новый `List`), `ItemsSource` получает новую ссылку → ItemsControl делает полный Reset.
**Фикс:** держать в `TorrentDetailsViewModel` стабильные `ObservableCollection` и синхронизировать их по ключу (`RelativePath`/`Address`); `OnPropertyChanged(string.Empty)` — только для `Summary`.

### 2. HIGH — Завершение сеанса Windows не приводит к graceful shutdown
`App.xaml.cs:22-25`, `App.xaml:4`, `MainWindow.xaml.cs:156-171`
**Симптом:** при выходе из Windows/завершении сеанса процесс убивается без сохранения геометрии окна и хвоста сессии.
**Причина:** `ShutdownMode=OnExplicitShutdown`, обработчиков `Application.SessionEnding`/`ShutdownRequested` нет; единственный путь сохранения — `RequestExitAsync`, который вызывает только кнопка/трей.
**Фикс:** подписаться в `OnStartup` на `SessionEnding` → `_isExiting=true; await RequestExitAsync()` (или синхронный `SaveUiStateAsync` + `engine.SaveSessionAsync`).

### 3. HIGH — Повторный запуск иногда «ничего не делает»
`Services/SingleInstanceCoordinator.cs:88-111`, `App.xaml.cs:34-39`
**Симптом:** `ZTorrent.exe file.torrent` при уже запущенном клиенте изредка молча завершается, окно не активируется, торрент не добавляется.
**Причина:** сервер создаётся заново на каждое сообщение (`await using` + `maxNumberOfServerInstances:1`), между итерациями пайпа нет; клиент (`TryForward`, 2 с) в этот зазор не подключается, `TryAcquire` уже false → `Shutdown(0)` без обратной связи.
**Фикс:** создать `NamedPipeServerStream` один раз и циклить `WaitForConnectionAsync`; при неудачном `TryForward` — 2-3 ретрая, а не тихий выход.

### 4. MEDIUM — Активный пункт навигации визуально «выключается»
`MainWindow.xaml:183-185,301-303`, `MainViewModel.cs:378-386`, `Themes/Controls.xaml:390-423`
**Симптом:** клик по уже выбранному фильтру («Все загрузки») или по «Настройки» снимает подсветку с активного пункта до следующей смены фильтра.
**Причина:** `NavToggle.IsChecked` — `Mode=OneWay`; `ToggleButton.OnToggle` выставляет `IsChecked=false` через `SetCurrentValue`, а `SetFilter`/`OpenSettings` при неизменном значении не шлют `PropertyChanged` — биндинг не возвращает `true`.
**Фикс:** в `SetFilter` (и в начале `OpenSettings`) всегда вызывать `OnPropertyChanged(nameof(IsFilterAll))`… / `IsSettingsOpen`.

### 5. MEDIUM — Развёрнутое окно не восстанавливается
`MainWindow.xaml.cs:38-41,98-104`, `MainViewModel.cs:793-800`
**Симптом:** закрыл развёрнутым — после рестарта окно открывается обычным (размер берётся из `RestoreBounds`), `IsWindowMaximized` игнорируется.
**Фикс:** в `Loaded` — `if (settings.IsWindowMaximized) WindowState = WindowState.Maximized;`.

### 6. MEDIUM — «Тихий» старт мигает окном
`App.xaml.cs:68-76`, `MainWindow.xaml.cs:73-86`
**Симптом:** автозапуск (`StartMinimized`/`--minimized`) на секунды показывает окно, затем прячет в трей; плюс balloon «свёрнут» на каждом логине.
**Причина:** `_mainWindow.Show()` вызывается до `InitializeAsync`, `HideToTray()` — после; первый показ всегда виден.
**Фикс:** при `StartMinimized` не вызывать `Show()` до инициализации (создать окно, инициализировать, показать только если не minimized).

### 7. MEDIUM — Модальные диалоги не модальны для клавиатуры и перехватывают DnD в текстовые поля
`MainWindow.xaml.cs:261-271,324-334`, `MainWindow.xaml:464-509`
**Симптом:** при открытом Add/Confirm `Ctrl+F` уводит фокус в поиск за оверлеем, Tab ходит по библиотеке. Текст/файл, брошенный в поле magnet или «Папка загрузки», не вставляется — открывается диалог добавления.
**Причина:** оверлеи — обычные `Grid` без focus scope; ветка `Ctrl+F` не проверяет `hasModal`; `Window_PreviewDrop` обрабатывает всё и ставит `e.Handled=true` независимо от `OriginalSource`.
**Фикс:** guard `hasModal` для `Ctrl+F`; в `Window_PreviewDrop/Over` пропускать событие (`return`), если `e.OriginalSource` — `TextBoxBase`/`PasswordBox`; при открытии оверлея — `Keyboard.Focus` на первый контрол + `FocusManager.IsFocusScope`.

### 8. MEDIUM — Панель деталей: авто-скрытие спорит с пользователем
`MainWindow.xaml.cs:139-154`, `MainViewModel.cs:670-676`
**Симптом:** панель, открытая вручную при ширине < 1320, закрывается при любом ресайзе (даже 1 px) и сохраняется как «закрыта». Сохранение настроек возвращает `IsDetailsPaneOpen`/размер окна из снимка, сделанного при открытии страницы.
**Фикс:** ставить `_autoDetailsHidden` только при авто-скрытии и не переоценивать панель на каждом `SizeChanged`; UI-поля брать из живого состояния на момент Save.

### 9. MEDIUM — Ошибка валидации настроек не видна
`Views/SettingsView.xaml:72-82`, `ViewModels/SettingsViewModel.cs:168-210`
**Симптом:** при прокрученной вниз странице нажатие «Сохранить» выглядит как «ничего не произошло» — баннер с ошибками появляется в начале области прокрутки, вне экрана.
**Фикс:** дублировать первую ошибку в футере и/или скроллить `ScrollViewer` к баннеру + фокус на проблемное поле.

### 10. LOW — Файловые чекбоксы недоступны с клавиатуры
`Themes/Controls.xaml:462-466`, `Views/AddTorrentView.xaml:236`, `Views/TorrentDetailsView.xaml:344`
**Симптом:** в «Содержимое» и на вкладке «Файлы» файл нельзя выбрать/снять с клавиатуры (Tab не доходит, Space не работает).
**Причина:** `CompactCheckBox` с `Focusable=False` без `FocusVisual`.
**Фикс:** `Focusable=True` + FocusVisual, либо `IsChecked` TwoWay и выбор по Space на уровне `ListBoxItem`.

### 11. LOW — `IsSidebarCollapsed` сохраняется, но нигде не применяется
`MainViewModel.cs:136-137,422-426`, `MainWindow.xaml:131`
**Симптом:** состояние и команда есть, колонка сайдбара всегда 252 px — «свёрнутость» невидима пользователю, но пишется в settings.json.
**Фикс:** либо привязать ширину колонки к `IsSidebarCollapsed`, либо убрать из модели/настроек.

## 5 главных исправлений

1. Стабильные коллекции в `TorrentDetailsViewModel` (п.1) — иначе вкладка «Файлы» непригодна.
2. Graceful exit по `SessionEnding` (п.2).
3. Долгоживущий pipe-сервер + ретрай форвардинга (п.3).
4. `PropertyChanged` в `SetFilter`/`OpenSettings` (п.4).
5. Восстановление `Maximized` и старт без показа окна (п.5-6).

## Корректно (не переделывать)

Виртуализация `ListBox` + `Recycling` (`Controls.xaml:751-772`); заморозка кистей в конвертерах статусов; DPI PerMonitorV2; идемпотентные `Dispose`; `AsyncRelayCommand` блокирует повторный Confirm/Add; `ProgressRing`/`LinearProgress` и shimmer — на `OnRender`/`TranslateTransform`, без frozen Freezable.
