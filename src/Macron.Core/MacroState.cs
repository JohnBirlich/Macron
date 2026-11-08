namespace Macron.Core;

/// <summary>
/// Конечный автомат приложения: Idle → Recording → Idle → Playing → Idle.
/// Любой переход только через MacroController.
/// </summary>
public enum MacroState
{
    Idle,
    Recording,
    Playing
}
