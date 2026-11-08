# Macron

Простой аналог TinyTask на C# / .NET 8 / WPF: записал действия мыши и клавиатуры → воспроизвёл → сохранил.

[![CI](https://github.com/JohnBirlich/Macron/actions/workflows/ci.yml/badge.svg)](https://github.com/JohnBirlich/Macron/actions/workflows/ci.yml)
[![Latest release](https://img.shields.io/github/v/release/JohnBirlich/Macron?label=latest)](https://github.com/JohnBirlich/Macron/releases)
[![License: MIT](https://img.shields.io/badge/license-MIT-blue.svg)](LICENSE)

## Скриншот

Скриншот главного окна добавится с первым выпуском, в котором есть интерфейс.

## Скачать

[Releases](https://github.com/JohnBirlich/Macron/releases) — `Macron.exe`: один self-contained exe под Windows 10/11 x64. .NET ставить не нужно.

Перед первым запуском сверьте SHA256 файла с `SHA256SUMS.txt` из релиза. SmartScreen может ругаться на неизвестный exe — пока нет подписи кода это ожидаемо.

## Возможности (1.0)

- Запись кликов, клавиш, прокрутки и движения мыши
- Воспроизведение с исходными интервалами
- Скорость воспроизведения 0.1×–10×
- Повтор N раз или бесконечно
- Глобальные горячие клавиши (настраиваемые: Record F6, Play F8, Stop F7, Panic Ctrl+Alt+F12)
- Сохранение и загрузка `.macron.json`
- Список макросов, переименование, удаление
- Пауза перед стартом воспроизведения
- Опция «не записывать движения мыши»

## Горячие клавиши

| Действие | Клавиша |
|----------|---------|
| Record | F6 |
| Play | F8 |
| Stop | F7 |
| Panic (аварийная остановка) | Ctrl+Alt+F12 |

Panic глушит воспроизведение мгновенно и работает даже в бесконечном цикле.

## Быстрый старт

1. Скачайте `Macron.exe` из [Releases](https://github.com/JohnBirlich/Macron/releases) и запустите.
2. Нажмите **Record** (F6) — выполните действия мышью и клавиатурой.
3. Нажмите **Stop** (F7).
4. Нажмите **Play** — макрос воспроизведётся с теми же интервалами.
5. Нажмите **Save** — сохраните `.macron.json`.

## Ограничения

- Без прав администратора приложение не управляет окнами, запущенными от администратора.
- Это не realtime-система: интервалы стабильны, но не субмиллисекундные.
- Координаты абсолютные (относительно виртуального рабочего стола). При смене масштаба DPI, разрешения или количества мониторов клики могут уехать (относительные координаты запланированы на 1.1).
- Формат `.macron.json` до `v1.0.0` может меняться.

## Честное предупреждение

Macron автоматизирует ваш собственный ввод. Не используйте его в играх с античитом и там, где автоматизация запрещена правилами сервиса.

## Сборка из исходников

Требуется Windows, .NET 8 SDK и набор инструментов для WPF (входит в пакет разработки для классических приложений в установщике Visual Studio).

```bash
git clone https://github.com/JohnBirlich/Macron.git
cd Macron
dotnet build -c Release
dotnet test -c Release
```

## Как устроен репозиторий

Четыре проекта в одном решении:

- `src/Macron.Core` — модель событий, JSON, конечный автомат состояний, планировщик таймингов, repeat/speed (без WPF и WinAPI, тестируется отдельно).
- `src/Macron.Input.Windows` — WinAPI: хуки, SendInput, горячие клавиши, DPI/виртуальный рабочий стол.
- `src/Macron.App` — WPF + MVVM, одно окно, настройки.
- `tests/Macron.Core.Tests` — xUnit.

## Версии и релизы

Текущая линейка — `0.x`, начиная с `v0.0.1`. До `v1.0.0` формат файла и поведение могут меняться — смотрите [CHANGELOG](CHANGELOG.md). Каждый релиз содержит ровно `Macron.exe`, `SHA256SUMS.txt` и notes из CHANGELOG.

## Баг-репорты

[Issues](https://github.com/JohnBirlich/Macron/issues). Прикладывайте версию, Windows и масштаб, шаги воспроизведения и лог (логи не содержат нажатых клавиш).

## Лицензия

[MIT](LICENSE).
