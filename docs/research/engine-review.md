# Ревью движка ZTorrent (MonoTorrent 3.0.2)

> **Статус:** это исторический отчёт по версии до исправлений. В текущем коде закрыты
> восстановление сессии без гонки, `CacheDataOnly` при удалении данных, отключение DHT,
> цикл снимков с retry, `PartialProgress`, безопасная смена порта, отложенные приоритеты
> magnet, защита `Torrent!`, очистка после неудачного add/restore, `CancellationToken.None`
> для сессии, валидация путей/`torrents: null`, `SelectMany` для трекеров и кодировка.
> Отчёт сохранён как инженерный артефакт и источник контекста для регрессий.

Сверено по исходникам, XML-докам пакета и исходникам тега `release-v3.0.2` (для семантики, которой нет в доках). Сборка не запускалась: SDK 10.0.100 в среде нет, API проверялся рефлексией (`artifacts/probe`, `artifacts/api-probe`) и доками.

Сигнатуры подтверждены: `ClientEngine.AddAsync(Torrent|MagnetLink, string saveDirectory, TorrentSettings)` → `Task<TorrentManager>`; `TorrentManager.PauseAsync/StartAsync/StopAsync()/StopAsync(TimeSpan)`; `ClientEngine.RemoveAsync(TorrentManager, RemoveMode)` → `Task<bool>` (требует `State == Stopped`); `ClientEngine.UpdateSettingsAsync(EngineSettings)`; `TorrentManager.UpdateSettingsAsync(TorrentSettings)`; `ClientEngine.SaveStateAsync(string)`, `StopAllAsync(TimeSpan)`, `Dht` (`IDht`); `RemoveMode = KeepAllData(0) | CacheDataOnly | DownloadedDataOnly | CacheDataAndDownloadedData`; `FastResumeMode.Accurate`; сборщики `EngineSettingsBuilder`/`TorrentSettingsBuilder` (коллекции не-null, дубли `AllowedEncryption` запрещены).

## Дефекты

**[critical] TorrentEngineService.cs:858, :755** — restore пишет `_torrents`/`_failed` без `_gate`, а sampling-loop (:127) уже перечисляет их под gate (:1104, :1133). `Dictionary` не потокобезопасен → падение/порча при каждом старте с сессией. Fix: restore под `_gate` либо до запуска loop.

**[high] :459, :464** — удаление данных идёт с `KeepAllData`, т.е. fast resume (`<cache>/fastresume/<hash>.fresume`) остаётся; `LoadFastResumeAsync` ставит `HashChecked=true` без проверки файлов → повторно добавленный торрент «100%», куски не качаются, пирам отдаётся мусор. Fix: `deleteData ? CacheDataOnly : KeepAllData`.

**[high] :1316** — `DhtEndPoint` задан всегда, поэтому `EnableDht=false` не выключает DHT: в 3.0.2 DHT создаётся только при `DhtEndPoint != null` (default `0.0.0.0:0`). Fix: `null` при выключенном DHT.

**[high] :1200-1218** — `catch` вне `while`: одно исключение `PublishSnapshotAsync` (включая OCE от токена вызывающего) навсегда убивает sampling, UI замирает. Fix: try/catch на каждый тик, публиковать с `CancellationToken.None`.

**[high] :1031, :1049** — `Progress`/`Complete` считают все файлы (включая `DoNotDownload`), а `State=Seeding` ставится по выбранным кускам → «Seeding, 40%, IsComplete=false». Fix: `Math.Clamp(manager.PartialProgress, 0, 1)` и `isComplete` по `PartialProgress`.

**[high] :598-625** — `ClientEngine.UpdateSettingsAsync` в 3.0.2 перезапускает слушатели: при занятом порте старые уже остановлены, `catch` оставляет движок без входящих, а текст «применится после перезапуска» ложен (порт применяется сразу). Fix: preflight-bind порта, откат/ошибка без потери слушателей.

**[medium] :866, :213/:291, :1063** — пока `HasMetadata=false`, приоритеты magnet молча отбрасываются и после метаданных не переприменяются; `SelectedBytes` = `preview.TotalSize` (0 без `xl`). Fix: применять `Record.FilePriorities` + `RecalculateSelectedBytes` при переходе в `Metadata`.

**[medium] :277** — magnet добавляется с `isPrivate:false`: DHT/LSD MonoTorrent гасит сам (`Torrent.IsPrivate`), но `PeerExchangeManager` проверяет только `AllowPeerExchange` → приватный торрент утекает через PEX. Fix: `UpdateSettingsAsync` по факту метаданных + хранить `IsPrivate`.

**[medium] :499** — `Manager.Torrent!` без `HasMetadata`: в 3.0.2 свойство — `Torrent?` → NRE на magnet. Fix: guard + `TorrentValidationException`.

**[medium] :218-241, :196-200** — при сбое `StartAsync` или отмене `AddAsync` менеджер уже зарегистрирован в движке: копия `.torrent` удаляется (:241), запись не сохраняется, повторное добавление → `TorrentException` «already been registered». Fix: копию удалять только без регистрации; при отмене — `RemoveAsync(manager, CacheDataOnly)`.

**[medium] :233, :301, :326, :349…** — сессия сохраняется с токеном вызывающего: отмена после изменения даёт состояние в памяти без записи на диск (после рестарта торрента нет, данные есть). Fix: `PersistSessionLockedAsync(CancellationToken.None)`.

**[medium] :793, :818 + SessionStore.cs:57** — restore не проверяет абсолютность `SavePath`/`TorrentPath` и не пере-валидирует magnet; `"torrents": null` даёт NRE (ловятся только `JsonException`/IO) и теряет всю сессию. Fix: валидация путей/`IsUnderRoot` + null-guard.

**[medium] TorrentPreviewService.cs:145** — `Torrent.AnnounceUrls` — `IList<IList<string>>` (тиры), `Select(uri => uri.ToString())` даёт строку вида `System.Collections.Generic.List` + `[System.String]`; magnet-ветка (:103) верна (плоский `IList<string>`). Fix: `.SelectMany(tier => tier)`.

**[medium] :169, :186, :228, :502, :757, :1274, :1487** — все русские литералы двойного кодирования (`Р­С‚РѕС‚` вместо `Этот`, файл с BOM): ошибки нечитаемы, текстовые тесты ломаются. Fix: перекодировать CP1251→UTF-8 (в `TorrentPreviewService`/`PathSafety` всё верно).

**[medium] :908** — «все файлы `DoNotDownload`»: `selected=0` подменяется на `Torrent.Size`, хотя качать нечего; `PartialProgressSelector` пуст, `State` навсегда `Downloading`. Fix: оставлять 0 и отклонять пустой выбор.

**[low] :695, :1259** — `SaveStateAsync` пишет файл, который нечем прочитать (в 3.0.2 нет `LoadStateAsync`); при fallback порта снапшот показывает `ListenPort=0` (реальный порт из `EngineSettings` не узнать). Fix: убрать мёртвый вызов, не показывать 0.

**[low] :662, :706, :718** — `DisposeAsync`: `_disposed` ставится до `_gate`, `_engine=null` и `_gate.Dispose()` — при ждущих вызовах это ODE/NRE на `_engine!`; та же гонка в `SaveSessionAsync` (:639). Fix: `Interlocked.Exchange`, не диспозить `_gate`.

**[low] :367, :433, :744** — `PauseAll` ставит `StartPaused=true` даже при провале pause; `_failed` игнорирует `deleteData` и восстанавливается/падает вечно. Fix: флаг после успеха, счётчик попыток/`Disabled`.

## 5 главных исправлений

1. `DhtEndPoint = null` при `EnableDht=false` (:1316) — иначе DHT работает вопреки настройке.
2. `RemoveMode.CacheDataOnly` при `deleteData=true` (:459) — иначе «лживый» fast resume и порча данных.
3. Восстановление под `_gate` + try/catch на тик sampling (:858, :755, :1200).
4. `PartialProgress` для прогресса/завершённости (:1031, :1049).
5. Preflight порта до `UpdateSettingsAsync`, не терять слушателей (:598-625).

## API-факты, которые нельзя перепутать

- `TorrentManager.Torrent` — `Torrent?`; `HasMetadata => Torrent != null`; до метаданных `Files` пуст.
- `Progress`/`Bitfield` — по всем файлам; `PartialProgress` — по выбранным; `Complete => Bitfield.AllTrue`; `Seeding` может быть при `Complete == false`.
- `AddAsync` сам грузит fast resume; `LoadFastResumeAsync` не проверяет файлы и ставит `HashChecked=true`.
- `RemoveAsync(manager, mode)` требует `Stopped`; `KeepAllData = 0` не трогает fast resume/metadata; `CacheDataOnly` удаляет `<cache>/fastresume/<hash>.fresume` и `<cache>/metadata/<hash>.torrent`.
- `PauseAsync` не пишет fast resume (только `StopAsync`/`StopAllAsync`); `StopAsync` бросает, если режим `Stopping`.
- `UpdateSettingsAsync(EngineSettings)` реально перезапускает слушатели и DHT; `TorrentManager.UpdateSettingsAsync` только присваивает.
- DHT включается наличием `DhtEndPoint`; отдельного `EnableDht` нет.
- `CacheDirectory` — база для `fastresume`, `metadata`, `dht_nodes.cache`.
- `Torrent.AnnounceUrls` — `IList<IList<string>>`; `MagnetLink.AnnounceUrls` — `IList<string>`.
- `ITracker`: `Uri`, `Status`, `FailureMessage`, `WarningMessage`, `TimeSinceLastAnnounce`; `TrackerTier`: `Trackers`, `LastAnnounceSucceeded`.
- `TorrentState`: `Stopped, Paused, Starting, Downloading, Seeding, Hashing, HashingPaused, Stopping, Error, Metadata, FetchingHashes`.
- `TorrentManager.Files` = `Torrent.Files.Select(...)` → индексные приоритеты корректны (padding включён).
- В 3.0.2 нет `LoadStateAsync`; `SaveStateAsync(string)` — write-only legacy.
- MonoTorrent сам отклоняет absolute/`/../` в `path` (`PathValidator`), но не symlink-атрибуты и `:` в каталоге.

## Проверено и корректно

`EngineSettingsBuilder` и состав `EngineSettings` (CacheDirectory, FastResumeMode.Accurate, UsePartialFiles=false, WritesOnly, DiskCacheBytes, AllowedEncryption с PlainText); `RemoveMode`-семантика; `SafeDataDeleter` (`IsUnderRoot`, не удаляет корень, чистит пустые каталоги, `Distinct`); `SessionStore` (temp+`File.Move`, карантин битого JSON, дедуп по Id, нормализация `FilePriorities`); пересчёт `record.Id` по фактическому хешу; сопоставление `manager.Files` ↔ `Torrent.Files`; `CreateContainingDirectory=true` (одиночный файл — прямо в `SavePath`); маппинги `TorrentState`/`TrackerState`/`DhtState`/`Priority`; `PeerId` API.
