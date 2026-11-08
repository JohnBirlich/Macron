# CHANGELOG

## [Unreleased]

## [0.0.1] - 2026-11-08
### Added
- Каркас solution: Macron.App, Macron.Core, Macron.Input.Windows, тесты.
- Модель событий (MouseMove/Down/Up, MouseWheel, KeyDown/Up) с дельта-временами и JSON-сериализацией в .macron.json.
- Конечный автомат состояний Idle → Recording → Idle → Playing → Idle (только через MacroController).
- Планировщик таймингов: гибридное ожидание (Thread.Sleep − 2 мс + SpinWait), отмена < 100 мс.
- Низкоуровневые хуки мыши/клавиатуры на отдельном потоке, фильтрация injected-событий, сжатие movimientos мыши до ~60/с.
- Воспроизведение через SendInput: мышь — абсолютные координаты виртуального рабочего стола (DPI/мультимонитор), клавиатура — по сканкодам.
- Глобальные горячие клавиши: Record F6, Play F8, Stop F7, Panic Ctrl+Alt+F12.
- WPF-интерфейс: одно окно, список макросов, Record/Stop/Play/Save/Open, скорость воспроизведения, повторы, пауза перед стартом, статус-бар.
- Настройки и библиотека макросов в %APPDATA%\Macron.
- Атомарное сохранение (temp → .bak → замена).
- CI на push/PR (windows-latest, .NET 8, build + test) и публикация релиза по тегу v*.
