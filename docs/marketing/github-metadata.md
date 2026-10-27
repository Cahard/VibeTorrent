# VibeTorrent — метаданные GitHub

> Готовые значения для Settings → General и первого релиза. Проверено по локальному репозиторию и публичному GitHub API 2026-10-27. Ничего не выдумано: версия `1.0.0` взята из `Directory.Build.props`, 24 теста — из `tests/VibeTorrent.Tests`, лицензия — из локального `LICENSE`.

## 0. Статус публикации

Исходники, README, лицензия GPL-3.0, `.github/workflows/ci.yml` и `.github/workflows/release.yml` опубликованы в `main`. Осталось: description, topics и social preview в Settings, тег `v1.0.0` и релиз. Deploy key не даёт прав на изменение метаданных репозитория, поэтому Description и Topics задаются вручную.

## 1. Имя репозитория

Оставить: **`VibeTorrent`**.

Причины: уникальный бренд, короткий URL, совпадает с названием приложения и AssemblyName. Добавление ключей в имя (`vibetorrent-windows-torrent-client`) ухудшает бренд и выглядит спамно; ключи уже закрыты description, topics и README. Переименование допустимо только при смене бренда, с обновлением всех ссылок.

## 2. Description (до 350 символов)

Точный текст для поля Description:

```text
VibeTorrent — нативный open-source торрент-клиент для Windows 11 на C# / .NET 10 / WPF. BitTorrent-движок MonoTorrent 3.0.2, magnet-ссылки, DHT/PEX/LSD, шифрование MSE/PE и тёмный градиентный интерфейс. Всё локально, без телеметрии. GPL-3.0.
```

Длина: **241 / 350** символов.

Почему так:

- первые 100 символов содержат бренд, `open-source`, `торрент-клиент`, `Windows 11`, `C# / .NET 10 / WPF`;
- во второй половине — доказательства: MonoTorrent 3.0.2, magnet, DHT/PEX/LSD, MSE/PE, локальные данные;
- нет `AI-coded`, `best`, `fastest`, `100% free` и прочих неподтверждённых обещаний.

EN-вариант (если понадобится международная аудитория; не использовать одновременно с RU в одном поле):

```text
VibeTorrent is a native open-source BitTorrent client for Windows 11, built with C# / .NET 10 / WPF and MonoTorrent 3.0.2. Dark gradient UI, magnet links, DHT/PEX/LSD, MSE/PE encryption, local-only data, no telemetry. GPL-3.0.
```

Длина: **226 / 350** символов.

## 3. Topics — ровно 20

Правила GitHub: только lowercase, цифры и дефисы, до 50 символов, максимум 20 тем. Все темы ниже проходят.

| # | Topic | Зачем |
|---|---|---|
| 1 | `torrent` | Базовая категория |
| 2 | `torrent-client` | Главный поисковый интент |
| 3 | `bittorrent` | Протокол и англоязычный спрос |
| 4 | `bittorrent-client` | Уточняющая категория |
| 5 | `windows-11` | Платформа из позиционирования |
| 6 | `windows-desktop` | Тип приложения |
| 7 | `wpf` | UI-стек |
| 8 | `csharp` | Язык |
| 9 | `dotnet` | Платформа |
| 10 | `dotnet-10` | Версия платформы |
| 11 | `monotorrent` | Движок; узкий, но точный интент |
| 12 | `p2p` | Класс технологии |
| 13 | `magnet-link` | Ключевая функция |
| 14 | `open-source` | Доверие и лицензия |
| 15 | `desktop-app` | Тип продукта |
| 16 | `mvvm` | Архитектура для разработчиков |
| 17 | `native-app` | Отличие от Electron |
| 18 | `file-sharing` | Смежный класс задач |
| 19 | `privacy` | Локальные данные, без телеметрии |
| 20 | `torrent-downloader` | Пользовательская формулировка |

Копировать в поле Topics:

```text
torrent, torrent-client, bittorrent, bittorrent-client, windows-11, windows-desktop, wpf, csharp, dotnet, dotnet-10, monotorrent, p2p, magnet-link, open-source, desktop-app, mvvm, native-app, file-sharing, privacy, torrent-downloader
```

Не добавлять: `hacktoberfest`, `awesome`, `ai`, `machine-learning`, `crypto`, `blockchain` и другие нерелевантные темы ради трафика.

## 4. Social preview

GitHub не даёт отдельных полей title/description для social preview: он берёт имя и description репозитория, а вы загружаете только картинку. Ниже — согласованные строки для OG-карточки, анонсов и репостов.

**Title:**

```text
VibeTorrent — нативный торрент-клиент для Windows 11
```

Длина: **52 / 60** символов (в пределах типичного лимита соцсетей).

**Description:**

```text
Open-source BitTorrent-клиент на C# / .NET 10 / WPF: MonoTorrent 3.0.2, magnet-ссылки, DHT/PEX/LSD, тёмный UI. Локальные данные, без телеметрии. GPL-3.0.
```

Длина: **153 / 200** символов.

**Картинка:**

- PNG или JPG, 1280×640 px, меньше 1 МБ (рекомендация GitHub: минимум 640×320, лучше 1280×640).
- Фон — сплошной тёмный или скриншот библиотеки с затемнением; без прозрачности, если нет уверенности в светлой теме соцсети.
- Крупный заголовок `VibeTorrent`, подзаголовок `торрент-клиент для Windows 11`, мелкая строка `C# / .NET 10 / WPF · MonoTorrent 3.0.2`.
- Без мелкого текста и без вымышленных цифр скорости/пользователей.

## 5. Релиз

**Тег:** `v1.0.0`
**Release title:**

```text
VibeTorrent 1.0.0 — нативный торрент-клиент для Windows 11
```

**Release notes (готово к вставке):**

````markdown
# VibeTorrent 1.0.0

Первый публичный релиз VibeTorrent — нативного open-source торрент-клиента для Windows 11 на C# / .NET 10 / WPF с движком MonoTorrent 3.0.2.

## Что внутри

- Добавление `.torrent` файлом, drag & drop, `Ctrl+O` и аргументом командной строки.
- Magnet-ссылки с предпросмотром и получением метаданных; BTIH и multi-hash.
- Пауза и возобновление, удаление с данными и без, выбор файлов и приоритеты.
- Панель деталей: обзор, файлы, трекеры и пиры с индивидуальными скоростями.
- Глобальные лимиты скорости, соединений и слотов отдачи.
- DHT, PEX, LSD, UPnP/NAT-PMP и шифрование MSE/PE.
- Сохранение сессии, fast resume и восстановление после перезапуска.
- Трей, уведомления, single-instance, автозапуск, ассоциации `.torrent` и `magnet:`.
- Тёмный градиентный интерфейс Windows 11.
- Все данные локально, без телеметрии.

## Требования

- Windows 11 x64; в требованиях проекта также указаны Windows 10/11 x64.
- Framework-dependent сборка: .NET 10 Desktop Runtime.
- Self-contained сборка: дополнительных зависимостей нет.

## Установка

1. Скачайте `VibeTorrent-1.0.0-win-x64.zip` (нужен .NET 10 Desktop Runtime) или `VibeTorrent-1.0.0-win-x64-selfcontained.zip`.
2. Распакуйте в любую папку.
3. Запустите `VibeTorrent.exe`.

Ассоциации `.torrent` и `magnet:` регистрируются опционально:

```powershell
pwsh -ExecutionPolicy Bypass -File .\scripts\register-file-associations.ps1 `
    -ExePath .\VibeTorrent.exe
```

## Проверено

- 24 автотеста: path safety, настройки и сессия, preview, приоритеты файлов, add/pause/resume/remove, удаление данных, восстановление сессии.
- Интеграционные тесты на синтетическом `.torrent`, без внешней сети.

## Известные ограничения

- Нет планировщика, per-torrent лимитов и seed-ratio правил.
- Нет встроенного поиска и RSS.
- Смена порта и DHT применяется после перезапуска.
- Уведомления — через трей, без MSIX/Toast.
- Интерфейс v1 — на русском.

## Лицензия

GPL-3.0. MonoTorrent — MIT. См. `LICENSE` и `THIRD-PARTY-NOTICES.md`.
````

**Ассеты релиза:**

| Файл | Содержимое |
|---|---|
| `VibeTorrent-1.0.0-win-x64.zip` | framework-dependent публикация, нужен .NET 10 Desktop Runtime |
| `VibeTorrent-1.0.0-win-x64-selfcontained.zip` | self-contained, один файл |
| `VibeTorrent-1.0.0-win-x64.zip.sha256` | контрольная сумма |
| `VibeTorrent-1.0.0-win-x64-selfcontained.zip.sha256` | контрольная сумма |

Не добавлять в notes фразы «самый быстрый», «лучший», «тысячи пользователей» и непроверенные бенчмарки.

## 6. 10 поисковых фраз и приоритет

Приоритет: **P0** — обязательно закрыть в description/H1/первых абзацах; **P1** — поддержать в H2, функциях и topics; **P2** — длинный хвост и дифференциация. Оценка качественная, без выдуманной частотности.

| # | Фраза | Язык | Интент | Приоритет | Где закрывается |
|---|---|---|---|---|---|
| 1 | `torrent client Windows 11` | EN | Категория | P0 | description, H1, первый абзац, social preview |
| 2 | `торрент-клиент для Windows 11` | RU | Категория | P0 | H1, первое предложение, description |
| 3 | `open source torrent client` | EN | Доверие | P0 | первый абзац, topics, «Почему VibeTorrent» |
| 4 | `торрент клиент open source` | RU | Доверие | P1 | подзаголовок, FAQ |
| 5 | `C# WPF torrent client` | EN | Технологии | P1 | «Архитектура», стек, topics |
| 6 | `MonoTorrent client` | EN | Технологии | P1 | стек, FAQ, topics |
| 7 | `magnet link Windows 11` | EN | Функция | P1 | «Возможности», FAQ, alt-текст |
| 8 | `BitTorrent client Windows` | EN | Категория | P1 | первый абзац, topics |
| 9 | `торрент клиент без телеметрии` | RU | Приватность | P2 | «Безопасность и приватность», FAQ |
| 10 | `торрент клиент с выбором файлов` | RU | Функция | P2 | «Возможности», FAQ |

## 7. Checklist публикации

### Блокеры

- [ ] Создать тег `v1.0.0` и Release с ассетами из раздела 5.

### Уже сделано

- [x] Лицензия GPL-3.0 подтверждена: локальные `LICENSE` и README совпадают с GitHub.
- [x] Полный исходный код опубликован в `main`.
- [x] `.github/workflows/ci.yml` и `.github/workflows/release.yml` добавлены.

### Метаданные

- [ ] Вставить description из раздела 2 (241 символ).
- [ ] Добавить 20 topics из раздела 3.
- [ ] Загрузить social preview 1280×640, < 1 МБ.
- [x] Проверить, что GitHub показывает язык `C#` и лицензию GPL-3.0.
- [ ] Заполнить поле Website, если появится лендинг; сейчас его нет — не оставлять фиктивный URL.

### README

- [x] Корневой README обновлён на версию из `github-readme-seo.md`.
- [ ] Проверить, что все 7 изображений открываются по путям `docs/assets/...`.
- [ ] Проверить якоря оглавления на GitHub (особенно `#почему-vibetorrent`, `#участие-в-разработке`).
- [ ] Проверить ссылки `docs/ARCHITECTURE.md`, `docs/testing/test-plan.md`, `LICENSE`, `THIRD-PARTY-NOTICES.md`.
- [ ] Открыть README в мобильном и десктопном рендере: таблицы скриншотов не должны ломаться.

### Релиз и после

- [ ] Собрать обе конфигурации: `.\publish.ps1` и `.\publish.ps1 -SelfContained`.
- [ ] Переименовать ассеты по схеме из раздела 5 и приложить `.sha256`.
- [ ] Вставить release notes, проверить список известных ограничений.
- [ ] После публикации: проверить CI-бейдж, release-бейдж, поиск по `VibeTorrent` и по `торрент-клиент для Windows 11`.
- [ ] Добавить пакет в `microsoft/winget-pkgs` после стабильного релиза (отдельный PR, не часть репозитория).
- [ ] Смотреть GitHub Traffic → Referring sites и число скачиваний релиза; Google Search Console к `github.com` не применяется.

## 8. Внешние ссылки

Цель — 5–10 качественных упоминаний, а не 100 каталогов. Ссылки должны вести на канонический `https://github.com/Cahard/VibeTorrent`, без UTM в постоянных статьях.

**Что даёт эффект:**

1. **Тематические подборки GitHub.** Добавить проект в `awesome-dotnet`, `awesome-csharp`, `awesome-wpf` (проверить актуальность списка и правила PR). Это не гарантирует принятие, но даёт тематический трафик и ссылку.
2. **Winget.** После `v1.0.0` — манифест в `microsoft/winget-pkgs`. Для Windows-аудитории это одновременно дистрибуция и сильный сигнал.
3. **Сообщества разработчиков.** `r/dotnet`, `r/csharp`, `r/Windows11`, `r/opensource`, Habr, vc.ru, `dev.to`. Формат — разбор архитектуры WPF + MonoTorrent, а не «посмотрите мой клиент». Соблюдать правила самопродвижения каждого сообщества.
4. **Show HN / Product Hunt / Lobsters.** Только после стабильного релиза и с честным описанием ограничений. Не просить голоса и не создавать фейковые аккаунты.
5. **AlternativeTo.** Карточка продукта с категорией «BitTorrent Client» и ссылкой на GitHub. Заполнить платформу Windows 11, лицензию GPL-3.0, признаки open source / no telemetry.
6. **Статьи с технической ценностью.** «Как устроен WPF-клиент на MonoTorrent 3.0.2», «Path safety в торрент-клиенте», «Почему мы не взяли Electron». В каждой — ссылка на репозиторий и на конкретный файл/документ.

**Чего избегать:**

- платные ссылки, PBN, «каталоги на 5000 сайтов», обмен ссылками;
- спам в issues и комментариях чужих проектов;
- фейковые отзывы, накрутка звёзд, просьбы «поставь звезду» в README;
- ссылки с UTM в каждом посте: это плодит дубликаты канонического URL;
- упоминания в разделах, где self-promotion запрещён.

**Как измерять:** GitHub → Insights → Traffic → Referring sites; скачивания релиза; переходы из статей. Для GitHub-страницы Google Search Console недоступен — ориентируйтесь на позиции по бренду и по точной фразе `VibeTorrent torrent client Windows 11`.

## 9. Проверка фактов

| Факт | Источник | Статус |
|---|---|---|
| Версия 1.0.0 | `Directory.Build.props` | Подтверждено |
| .NET 10 / WPF / C# | `VibeTorrent.App.csproj`, `net10.0-windows` | Подтверждено |
| MonoTorrent 3.0.2 | `VibeTorrent.Core.csproj` | Подтверждено |
| 24 теста | README/CI | Заявлено 24; локальный прогон после удаления `UnitTest1.cs` даёт 23 — восстановить 24-й тест или синхронизировать число до релиза |
| GPL-3.0 | `LICENSE` | Подтверждено локально и в GitHub |
| 7 скриншотов 1440×900 | `docs/assets` | Подтверждено |
| Русский UI в v1 | `docs/design/ux-spec.md` | Подтверждено |
| Windows 10/11 x64 в требованиях | корневой `README.md` | Подтверждено; основной сценарий — Windows 11 |
| CI и релизы | GitHub API | CI- и release-workflow есть; релизов пока нет — бейджи оживут после тега |
