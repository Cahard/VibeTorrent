# VibeTorrent — open-source торрент-клиент для Windows 11 на C# / WPF

[![CI](https://github.com/Cahard/VibeTorrent/actions/workflows/ci.yml/badge.svg?branch=main)](https://github.com/Cahard/VibeTorrent/actions/workflows/ci.yml)
[![Release](https://img.shields.io/github/v/release/Cahard/VibeTorrent?display_name=tag&sort=semver)](https://github.com/Cahard/VibeTorrent/releases)
[![License: GPL-3.0](https://img.shields.io/badge/license-GPL--3.0-blue)](LICENSE)
[![.NET 10](https://img.shields.io/badge/.NET-10-512BD4)](https://dotnet.microsoft.com/)
[![Windows 11 x64](https://img.shields.io/badge/Windows-11%20x64-0078D4)](https://www.microsoft.com/windows/windows-11)

<!-- CI-бейдж оживает после первого запуска .github/workflows/ci.yml; release-бейдж — после тега v1.0.0. -->

VibeTorrent — нативный **торрент-клиент для Windows 11** на C# / .NET 10 / WPF. Это open-source BitTorrent-клиент без браузера и Electron: движок MonoTorrent 3.0.2, magnet-ссылки, DHT/PEX/LSD, шифрование MSE/PE и тёмный интерфейс с живыми градиентами.

Данные и управление — локально: библиотека, файлы, трекеры, пиры и настройки в одном окне. Ни телеметрии, ни облачных сервисов: данные лежат в `%LOCALAPPDATA%\VibeTorrent`.

*In English: VibeTorrent is a native open-source BitTorrent client for Windows 11, built with C# / WPF on .NET 10 and MonoTorrent 3.0.2.*

![Библиотека VibeTorrent: список торрентов, суммарные скорости, фильтры и панель деталей](docs/assets/01-library.png)

> [!NOTE]
> Скриншоты сделаны в демонстрационном режиме. Все торренты, имена и цифры в них вымышлены.

## Содержание

- [Возможности](#возможности)
- [Почему VibeTorrent](#почему-vibetorrent)
- [Быстрый старт](#быстрый-старт)
- [Релизы и загрузка](#релизы-и-загрузка)
- [Скриншоты](#скриншоты)
- [Архитектура](#архитектура)
- [Безопасность и приватность](#безопасность-и-приватность)
- [Тесты](#тесты)
- [Roadmap](#roadmap)
- [FAQ](#faq)
- [Участие в разработке](#участие-в-разработке)
- [Лицензия](#лицензия)

## Возможности

### Добавление и библиотека

- `.torrent` через файл, drag & drop, `Ctrl+O` и аргумент командной строки.
- Magnet-ссылки с предпросмотром и получением метаданных; BTIH и multi-hash.
- Поиск по названию, хешу или пути, фильтры по состояниям, сортировка и счётчики.
- Пауза и возобновление одного торрента или всех сразу.
- Удаление торрента с данными и без; удаление файлов — только внутри папки загрузки.

### Загрузка и сеть

- BitTorrent-движок **MonoTorrent 3.0.2**.
- DHT, PEX, LSD, UPnP/NAT-PMP и шифрование MSE/PE.
- Приватные торренты не уходят в DHT и PEX.
- Глобальные лимиты скорости, соединений и слотов отдачи.
- Сохранение сессии, fast resume и восстановление после перезапуска или аварийного завершения.

### Файлы и приоритеты

- Выбор файлов до добавления торрента.
- Приоритеты файлов после добавления, включая `DoNotDownload`.
- Прогресс и завершённость считаются по выбранным файлам, а не по всему торренту.
- Панель деталей: обзор, файлы, трекеры и пиры с индивидуальными скоростями.

### Интерфейс Windows 11

- Только тёмная тема, кастомные `ProgressRing`, `LinearProgress`, `Sparkline` и `ShimmerBehavior`.
- Панель деталей автоматически сворачивается при узком окне, чтобы не сжимать список.
- Сворачивание в трей, уведомления о завершении и ошибках, single-instance через named pipe.
- Запуск при входе в Windows; опциональная регистрация `.torrent` и `magnet:`.

### Данные

- Настройки, сессия, копии `.torrent`, fastresume, metadata и логи — в `%LOCALAPPDATA%\VibeTorrent`.
- Телеметрии и облачных сервисов нет. Сеть используется только для BitTorrent, DHT, трекеров и UPnP.

## Почему VibeTorrent

| Принцип | Что это значит |
|---|---|
| Нативный стек | WPF и .NET 10, без Electron и встроенного браузера. |
| Открытый код | GPL-3.0: проект можно собрать, проверить и форкнуть. |
| Локальные данные | Настройки, сессия и торренты остаются на компьютере. |
| Безопасные пути | Проверки traversal, UNC, ADS и зарезервированных имён; удаление только под папкой загрузки. |
| BitTorrent-движок | MonoTorrent 3.0.2 — единственная точка работы с `ClientEngine`. |
| Интерфейс | Плотная тёмная тема с градиентами и адаптивной панелью деталей. |

Кому подойдёт:

- пользователям Windows 11, которым нужен нативный open-source torrent client без облака;
- разработчикам, которым интересен пример WPF + MonoTorrent на .NET 10.

## Быстрый старт

> [!IMPORTANT]
> Нужны Windows 11 x64 и [.NET 10 SDK](https://dotnet.microsoft.com/download) 10.0.100+. В требованиях проекта также указаны Windows 10/11 x64; основной сценарий — Windows 11.

```powershell
git clone https://github.com/Cahard/VibeTorrent.git
cd VibeTorrent

.\build.ps1   # restore, build и 24 теста
.\run.ps1     # запуск приложения
```

Если PowerShell блокирует локальные скрипты:

```powershell
pwsh -ExecutionPolicy Bypass -File .\build.ps1
```

> [!TIP]
> Если .NET SDK лежит в `.dotnet\dotnet.exe`, скрипты найдут его автоматически.

### Публикация

```powershell
.\publish.ps1                  # framework-dependent, нужен .NET 10 Desktop Runtime
.\publish.ps1 -SelfContained   # self-contained, один файл
```

Результат: `artifacts\publish\win-x64\VibeTorrent.exe`.

### Ассоциации файлов

```powershell
pwsh -ExecutionPolicy Bypass -File .\scripts\register-file-associations.ps1 `
    -ExePath .\artifacts\publish\win-x64\VibeTorrent.exe
```

Отмена: `scripts\unregister-file-associations.ps1`.

### Горячие клавиши

| Клавиши | Действие |
|---|---|
| `Ctrl+O` | Добавить `.torrent` |
| `Ctrl+V` | Вставить magnet из буфера обмена |
| `Ctrl+F` | Поиск по библиотеке |
| `Ctrl+,` | Настройки |
| `Space` | Пауза / продолжить выбранное |
| `Delete` | Удалить выбранный торрент |
| `Esc` | Закрыть диалог / сбросить поиск |

## Релизы и загрузка

Готовые сборки для `win-x64` публикуются в [GitHub Releases](https://github.com/Cahard/VibeTorrent/releases). Если релизов ещё нет, соберите проект из исходников — это одна команда.

| Вариант | Что получаете | Что нужно |
|---|---|---|
| Framework-dependent | `VibeTorrent-<версия>-win-x64.zip` | .NET 10 Desktop Runtime |
| Self-contained | `VibeTorrent-<версия>-win-x64-selfcontained.zip` | ничего дополнительно |

Рядом с архивами публикуются `.sha256`-контрольные суммы.

> [!WARNING]
> Если сборка не подписана сертификатом код-подписи, Windows SmartScreen может показать предупреждение. Скачивайте файлы только со страницы Releases этого репозитория.

## Скриншоты

| Библиотека | Добавление по magnet |
|---|---|
| ![Библиотека VibeTorrent: список торрентов, суммарные скорости, фильтры и панель деталей](docs/assets/01-library.png) | ![Добавление magnet-ссылки в VibeTorrent: предпросмотр метаданных и выбор папки загрузки](docs/assets/02-add-torrent.png) |

| Настройки | Файлы торрента |
|---|---|
| ![Настройки VibeTorrent: папка загрузок, автозапуск, трей, порт и DHT](docs/assets/03-settings.png) | ![Вкладка «Файлы» в VibeTorrent: приоритеты и выбор файлов торрента](docs/assets/05-files-tab.png) |

| Добавление `.torrent` | Удаление |
|---|---|
| ![Добавление .torrent в VibeTorrent: проверка содержимого и выбор файлов](docs/assets/04-add-file.png) | ![Подтверждение удаления торрента в VibeTorrent с опцией удаления данных](docs/assets/06-confirm-remove.png) |

| Первый запуск |
|---|
| ![Пустая библиотека VibeTorrent: добавление первого торрента или magnet-ссылки](docs/assets/08-empty.png) |

## Архитектура

```text
src/VibeTorrent.App     WPF UI, MVVM, тема, tray, chrome, single-instance
        │
        ▼
src/VibeTorrent.Core    MonoTorrent wrapper, DTO, сессия, настройки, безопасность путей
```

- `VibeTorrent.Core` не ссылается на WPF и тестируется отдельно; `VibeTorrent.App` не работает с MonoTorrent напрямую.
- `TorrentEngineService` — единственная точка работы с `ClientEngine`. Снимки состояния — раз в секунду, детали — раз в две секунды, все мутации сериализованы.
- События движка маршалятся в UI через `IUiDispatcher`; диск и сеть не блокируют интерфейс.
- `settings.json` и `session.json` пишутся атомарно; битый JSON уходит в `*.corrupt-<timestamp>` и не блокирует запуск.

Подробнее: [docs/ARCHITECTURE.md](docs/ARCHITECTURE.md) · [Ревью движка](docs/research/engine-review.md).

## Безопасность и приватность

- `.torrent` ограничен 32 МБ и 100 000 файлов.
- Пути из метаданных отклоняются при traversal, UNC, ADS, зарезервированных именах, управляющих символах и недопустимых для Windows символах.
- Удаление данных проверяет `PathSafety.IsUnderRoot`; корень загрузки не удаляется.
- UNC-папки загрузки отклоняются.
- Логи не содержат полные magnet-ссылки и passkey.
- Телеметрии нет. Сеть используется только для BitTorrent, DHT, трекеров и UPnP.
- Приватные торренты не анонсируются в DHT и PEX.
- Удалённого управления и облачной синхронизации нет.

> [!NOTE]
> VibeTorrent не проверяет скачанные файлы антивирусом и не гарантирует легальность контента. Скачивайте только то, на что у вас есть права.

## Тесты

```powershell
dotnet test tests\VibeTorrent.Tests\VibeTorrent.Tests.csproj
```

24 теста: path safety (traversal, UNC, ADS, зарезервированные имена), форматирование, атомарные настройки и сессия, битый JSON, дедупликация, preview `.torrent`/magnet, приоритеты файлов, add/pause/resume/remove, удаление данных и восстановление сессии.

Интеграционные тесты используют реальный `ClientEngine` с выключенными DHT/PEX/UPnP, свободным портом и синтетическим `.torrent`; внешняя сеть не требуется.

Подробнее: [docs/testing/test-plan.md](docs/testing/test-plan.md).

## Roadmap

Направления вне v1.0. Это не обязательства и не сроки.

| Направление | Статус |
|---|---|
| Планировщик расписаний | не входит в v1.0 |
| Per-torrent лимиты и seed-ratio | не входит в v1.0 |
| Встроенный поиск и RSS | не входит в v1.0 |
| MSIX и Windows Toast | не входит в v1.0 |
| Английская локализация | в следующих версиях |
| Удалённое управление | не входит в v1.0 |

Предложить направление или сообщить об ошибке — через [Issues](https://github.com/Cahard/VibeTorrent/issues).

## FAQ

### VibeTorrent — бесплатный торрент-клиент?

Да. Код распространяется под GPL-3.0, без подписок и платных функций в v1.0.

### Есть ли реклама и телеметрия?

Телеметрии и облачных сервисов нет. Рекламных модулей в коде нет. Сеть используется только для BitTorrent, DHT, трекеров и UPnP.

### Поддерживается ли Windows 10?

Основная платформа — Windows 11 x64. В требованиях проекта указаны Windows 10/11 x64; целевая платформа — `net10.0-windows`.

### Где хранятся данные?

В `%LOCALAPPDATA%\VibeTorrent`: `settings.json`, `session.json`, `torrents\`, `fastresume\`, `metadata\`, `cache\`, `logs\`.

### Поддерживаются ли magnet-ссылки?

Да: предпросмотр, получение метаданных, BTIH и multi-hash. Список файлов появляется после получения метаданных.

### Можно ли выбрать файлы и задать приоритеты?

Да. Файлы выбираются до добавления, приоритеты меняются после, включая `DoNotDownload`.

### Есть ли планировщик и лимиты на отдельный торрент?

Нет. В v1.0 есть только глобальные лимиты скорости, соединений и слотов отдачи.

### Нужен ли .NET?

Для framework-dependent сборки — .NET 10 Desktop Runtime. Self-contained сборка дополнительных зависимостей не требует.

### Это Electron?

Нет. Это нативное WPF-приложение на C# / .NET 10.

### Интерфейс только на русском?

В v1.0 — да. Английская локализация заявлена в следующих версиях.

## Участие в разработке

- Баги и предложения — через [Issues](https://github.com/Cahard/VibeTorrent/issues). Приложите версию Windows, версию .NET и шаги воспроизведения.
- Перед PR запустите `.\build.ps1`: сборка и 24 теста должны проходить.
- Держите `VibeTorrent.Core` без зависимостей от WPF.
- Изменения в работе с путями сопровождайте тестами path safety.
- Не публикуйте уязвимости в открытых issues.

## Лицензия

GPL-3.0. См. [LICENSE](LICENSE). MonoTorrent распространяется под MIT; другие зависимости перечислены в [THIRD-PARTY-NOTICES.md](THIRD-PARTY-NOTICES.md).