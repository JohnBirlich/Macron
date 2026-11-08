namespace Macron.Input.Windows;

using Macron.Core;
using Macron.Core.Events;
using System.Runtime.InteropServices;

/// <summary>
/// Воспроизведение событий через SendInput.
/// Мышь — абсолютные координаты в виртуальном рабочем столе (MOUSEEVENTF_ABSOLUTE | VIRTUALDESK),
/// так что клики не «едут» при масштабировании DPI и нескольких мониторах.
/// Клавиатура — по сканкодам (KEYEVENTF_SCANCODE), что надёжнее для раскладок RU/EN.
/// Каждое сообщение помечается меткой dwExtraInfo, чтобы рекorder мог отфильтровать
/// собственный ввод (вторая линия защиты наряду с флагом INJECTED).
/// </summary>
public sealed class InputPlayer
{
    internal static readonly UIntPtr MagicExtraInfo = unchecked((UIntPtr)0x4D414352u); // "MACR"

    public void Play(MouseMoveEvent evt)
    {
        var x = VirtualDesktop.NormalizeX(evt.X);
        var y = VirtualDesktop.NormalizeY(evt.Y);
        Send(new NativeMethods.INPUT
        {
            Type = NativeMethods.INPUT_MOUSE,
            Union = new NativeMethods.InputUnion
            {
                Mouse = new NativeMethods.MOUSEINPUT
                {
                    X = (int)x,
                    Y = (int)y,
                    DwFlags = NativeMethods.MOUSEEVENTF_MOVE | NativeMethods.MOUSEEVENTF_ABSOLUTE |
                              NativeMethods.MOUSEEVENTF_VIRTUALDESK,
                    DwExtraInfo = MagicExtraInfo
                }
            }
        });
    }

    public void Play(MouseDownEvent evt)
    {
        SendMouseButton(evt.Button, down: true);
    }

    public void Play(MouseUpEvent evt)
    {
        SendMouseButton(evt.Button, down: false);
    }

    public void Play(MouseWheelEvent evt)
    {
        Send(new NativeMethods.INPUT
        {
            Type = NativeMethods.INPUT_MOUSE,
            Union = new NativeMethods.InputUnion
            {
                Mouse = new NativeMethods.MOUSEINPUT
                {
                    MouseData = (uint)evt.Delta,
                    DwFlags = NativeMethods.MOUSEEVENTF_WHEEL | NativeMethods.MOUSEEVENTF_VIRTUALDESK,
                    DwExtraInfo = MagicExtraInfo
                }
            }
        });
    }

    public void Play(KeyDownEvent evt)
    {
        SendKey(evt.Scan, down: true, extended: IsExtended(evt.Vk));
    }

    public void Play(KeyUpEvent evt)
    {
        SendKey(evt.Scan, down: false, extended: IsExtended(evt.Vk));
    }

    private void SendMouseButton(MouseButton button, bool down)
    {
        uint flags = button switch
        {
            MouseButton.Left => down ? NativeMethods.MOUSEEVENTF_LEFTDOWN : NativeMethods.MOUSEEVENTF_LEFTUP,
            MouseButton.Right => down ? NativeMethods.MOUSEEVENTF_RIGHTDOWN : NativeMethods.MOUSEEVENTF_RIGHTUP,
            MouseButton.Middle => down ? NativeMethods.MOUSEEVENTF_MIDDLEDOWN : NativeMethods.MOUSEEVENTF_MIDDLEUP,
            MouseButton.XButton1 => down ? NativeMethods.MOUSEEVENTF_XDOWN : NativeMethods.MOUSEEVENTF_XUP,
            MouseButton.XButton2 => down ? NativeMethods.MOUSEEVENTF_XDOWN : NativeMethods.MOUSEEVENTF_XUP,
            _ => down ? NativeMethods.MOUSEEVENTF_LEFTDOWN : NativeMethods.MOUSEEVENTF_LEFTUP
        };

        uint mouseData = button switch
        {
            MouseButton.XButton1 => 0x00010000,
            MouseButton.XButton2 => 0x00020000,
            _ => 0
        };

        Send(new NativeMethods.INPUT
        {
            Type = NativeMethods.INPUT_MOUSE,
            Union = new NativeMethods.InputUnion
            {
                Mouse = new NativeMethods.MOUSEINPUT
                {
                    MouseData = mouseData,
                    DwFlags = flags | NativeMethods.MOUSEEVENTF_VIRTUALDESK,
                    DwExtraInfo = MagicExtraInfo
                }
            }
        });
    }

    private void SendKey(int scan, bool down, bool extended)
    {
        var flags = NativeMethods.KEYEVENTF_SCANCODE | (down ? 0u : NativeMethods.KEYEVENTF_KEYUP);
        if (extended)
        {
            flags |= NativeMethods.KEYEVENTF_EXTENDEDKEY;
        }

        Send(new NativeMethods.INPUT
        {
            Type = NativeMethods.INPUT_KEYBOARD,
            Union = new NativeMethods.InputUnion
            {
                Keyboard = new NativeMethods.KEYBDINPUT
                {
                    WVk = 0,
                    WScan = (ushort)scan,
                    DwFlags = flags,
                    DwExtraInfo = MagicExtraInfo
                }
            }
        });
    }

    // Для клавиш с правой стороне клавиатуры (AltGr, стрелки, NumPad и т.п.)
    private static bool IsExtended(int vk)
    {
        return vk is 0xA2 or 0xA3 or 0xA4 or 0xA5 or 0x25 or 0x26 or 0x27 or 0x28
               or 0x2D or 0x2E or 0x90 or 0x91 or 0x60 or 0x61 or 0x62 or 0x63 or 0x64
               or 0x65 or 0x66 or 0x67 or 0x68 or 0x69 or 0x6A or 0x6B or 0x6C or 0x6D
               or 0x6E or 0x6F or 0x9A or 0x9B;
    }

    private void Send(NativeMethods.INPUT input)
    {
        var inputs = new[] { input };
        NativeMethods.SendInput((uint)inputs.Length, inputs, Marshal.SizeOf<NativeMethods.INPUT>());
    }
}
