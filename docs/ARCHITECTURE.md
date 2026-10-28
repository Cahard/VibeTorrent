# Архитектура ZTorrent

## 1. Слои

```text
┌────────────────────────────────────────────────────────────┐
│ ZTorrent.App (net10.0-windows, WPF)                     │
│  MainWindow / LibraryView / Details / Settings / Add       │
│  MainViewModel + дочерние VM, UI-сервисы, tray, chrome     │
└──────────────────────────┬─────────────────────────────────┘
                           │ DTO + события + команды
┌──────────────────────────▼─────────────────────────────────┐
│ ZTorrent.Core (net10.0)                                 │
│  TorrentEngineService  ── MonoTorrent 3.0.2 / ClientEngine │
│  SettingsService, SessionStore, TorrentPreviewService     │
│  AppPaths, AppSettings, PathSafety, SafeDataDeleter        │
└────────────────────────────────────────────────────────────┘
```

`ZTorrent.Core` не ссылается на WPF и может тестироваться отдельно.
`ZTorrent.App` не работает с MonoTorrent напрямую.

## 2. Поток данных

1. Пользователь добавляет `.torrent` или magnet.
2. `TorrentPreviewService` валидирует и строит неизменяемый `TorrentPreview`.
3. `TorrentEngineService` копирует `.torrent` в `%LOCALAPPDATA%\ZTorrent\torrents`,
   добавляет торрент в `ClientEngine`, применяет приоритеты и стартует.
4. `SessionStore` атомарно сохраняет запись (hash, magnet/копия, save path, paused,
   приоритеты файлов).
5. Раз в секунду движок строит `EngineSnapshot` с `TorrentSnapshot` и статистикой.
   Событие приходит в фоновом потоке; `WpfUiDispatcher` переносит его в UI.
6. `MainViewModel` обновляет стабильные `TorrentItemViewModel` по `Id`, поэтому
   выделение, hover и контекст не теряются.
7. При выборе торрента отдельный цикл раз в 2 секунды забирает
   `TorrentDetailsSnapshot` (файлы, трекеры, пиры). Это не блокирует список.

## 3. Состояния и завершённость

- `TorrentManager.State` маппится в `TorrentStatus` (Downloading, Seeding, Paused, Error…).
- `TorrentManager.PartialProgress` — прогресс **выбранных** файлов; он используется для
  прогресса и `IsComplete`. Это важно, когда часть файлов `DoNotDownload`.
- `IsPaused` — намерение пользователя (`PersistedTorrent.StartPaused`) или состояние движка.
- `IsComplete` не зависит от `Seeding`: завершённый торрент может раздаваться.

## 4. Конкурентность

- Все мутации (`Add`, `Pause`, `Resume`, `Remove`, `SetFilePriority`, `ApplySettings`,
  сохранение сессии) сериализованы `SemaphoreSlim`.
- Снимки состояния читаются под тем же gate, поэтому словари менеджеров не меняются
  во время обхода.
- Восстановление сессии выполняется **до** запуска цикла снимков — это устраняет
  гонку между восстановлением и UI-снимками.
- Ошибка одного тика снимков логируется и не останавливает цикл.
- `async void` в UI используется только для обработчиков событий/команд платформы;
  длительные операции возвращают `Task` и наблюдаются.

## 5. Движок MonoTorrent

- `ClientEngine` создаётся один раз; лимиты и per-torrent настройки обновляются через
  `UpdateSettingsAsync`.
- DHT в 3.0.2 включается наличием `DhtEndPoint`: при `EnableDht=false` передаётся `null`.
- Порт/DHT нельзя безопасно менять на лету: `UpdateSettingsAsync` перезапускает
  слушатели. ZTorrent сохраняет настройку и применяет её при следующем запуске,
  явно предупреждая пользователя.
- Удаление с данными: сначала `RemoveMode.CacheDataOnly` (убирает fastresume/metadata),
  затем `SafeDataDeleter` удаляет только файлы, находящиеся под save path.
  Это исключает «лживый» fast resume после ручного удаления данных.
- Приоритеты файлов сопоставляются по индексу `TorrentManager.Files` ↔ `Torrent.Files`;
  для magnet они переприменяются, когда приходят метаданные.
- Fast resume включён (`AutoSaveLoadFastResume`, `FastResumeMode.Accurate`); metadata
  cache и fastresume лежат внутри `CacheDirectory` приложения, а именно
  `%LOCALAPPDATA%\ZTorrent\cache\fastresume`. `StopAllAsync` при обычном выходе
  сохраняет битфилд; после жёсткого падения Accurate-режим намеренно делает hash check.

## 6. Персистентность

`settings.json` и `session.json` пишутся через временный файл + `File.Move(overwrite)`.
Битый JSON отправляется в `*.corrupt-<timestamp>` и не блокирует запуск.
Сессия валидируется: обязательный `Id`, абсолютный `SavePath`, наличие `.torrent`
или magnet, уникальность Id. `torrents: null` безопасно превращается в пустой список.

При старте:

1. создаётся `ClientEngine`;
2. записи сессии добавляются по одной;
3. приоритеты применяются;
4. торренты, которые были активны, стартуют (если `AutoResumeOnStartup`);
5. только потом включается цикл снимков.

Ошибка восстановления одной записи не роняет остальные: запись отображается как
`Error` и может быть удалена.

## 7. Безопасность

- `.torrent` ограничен 32 МБ, 100 000 файлов.
- Пути из метаданных отклоняются при traversal, UNC, ADS, зарезервированных именах,
  управляющих символах и недопустимых для Windows символах.
- Удаление файлов проверяет `PathSafety.IsUnderRoot`; корень загрузки не удаляется.
- UNC-папки загрузки отклоняются (утечка NTLM и непредсказуемое поведение).
- Логи не содержат полные magnet/passkey.
- Нет телеметрии. Сеть используется только для BitTorrent, DHT, трекеров и UPnP.

## 8. UI

- Только тёмная тема; токены в `Themes/Tokens.xaml`.
- Кастомный chrome через `WindowChrome` + DWM dark/corner attributes.
- Анимации: `ShimmerBehavior` (один проход блика на CTA), мягкие opacity-анимации
  фона, hover/focus через XAML-триггеры. Список не анимируется построчно.
- Виртуализированный `ListBox`; карточки переиспользуются.
- При ширине < 1320 px панель деталей автоматически сворачивается, чтобы не сжимать
  список; при расширении возвращается.
- Все интерактивные элементы имеют tooltip и видимый фокус; горячие клавиши
  не срабатывают внутри текстового ввода.

## 9. Тестирование

- xUnit + интеграционные тесты на синтетическом `.torrent`.
- Реальные `ClientEngine` в тестах с DHT/PEX/UPnP выключенными, свободным портом.
- Проверяются add/pause/resume/remove, удаление данных, восстановление сессии,
  приоритеты, preview/types, path safety, настройки и повреждённый JSON.
- Подробный ручной план: `docs/testing/test-plan.md`.

## 10. Известные ограничения

- Для magnet до получения метаданных нельзя показать список файлов.
- Fast resume после аварийного падения может быть неполным; `Accurate` проверяет данные.
- Нет ограничения скорости на отдельный торрент и расписаний.
- Windows Toast без MSIX не используется; уведомления идут через tray balloon.
