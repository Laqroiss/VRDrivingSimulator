using UnityEngine;
using UnityEngine.InputSystem;
using UnityEngine.InputSystem.Controls;

// Drop-in shim for the few legacy UnityEngine.Input calls used in this project.
// Keeps existing KeyCode-typed inspector fields and "Horizontal"/"Vertical"/"Mouse X"/"Mouse Y"
// axis-name strings working under the new Input System.
public static class LegacyInput
{
    public static bool GetKey(KeyCode k)
    {
        var c = ToButton(k);
        return c != null && c.isPressed;
    }

    public static bool GetKeyDown(KeyCode k)
    {
        var c = ToButton(k);
        return c != null && c.wasPressedThisFrame;
    }

    public static bool GetKeyUp(KeyCode k)
    {
        var c = ToButton(k);
        return c != null && c.wasReleasedThisFrame;
    }

    public static float GetAxisRaw(string axis)
    {
        var kb = Keyboard.current;
        if (kb == null) return 0f;
        switch (axis)
        {
            case "Horizontal":
            {
                float h = 0f;
                if (kb.aKey.isPressed || kb.leftArrowKey.isPressed)  h -= 1f;
                if (kb.dKey.isPressed || kb.rightArrowKey.isPressed) h += 1f;
                return h;
            }
            case "Vertical":
            {
                float v = 0f;
                if (kb.sKey.isPressed || kb.downArrowKey.isPressed) v -= 1f;
                if (kb.wKey.isPressed || kb.upArrowKey.isPressed)   v += 1f;
                return v;
            }
        }
        return 0f;
    }

    // Approximates legacy mouse delta scaling (legacy GetAxis applied ~0.1 per pixel).
    public static float GetAxis(string axis)
    {
        if (axis == "Mouse X") return (Mouse.current?.delta.x.ReadValue() ?? 0f) * 0.1f;
        if (axis == "Mouse Y") return (Mouse.current?.delta.y.ReadValue() ?? 0f) * 0.1f;
        return GetAxisRaw(axis);
    }

    private static ButtonControl ToButton(KeyCode k)
    {
        var ms = Mouse.current;
        switch (k)
        {
            case KeyCode.Mouse0: return ms?.leftButton;
            case KeyCode.Mouse1: return ms?.rightButton;
            case KeyCode.Mouse2: return ms?.middleButton;
        }

        var kb = Keyboard.current;
        if (kb == null) return null;

        switch (k)
        {
            case KeyCode.A: return kb.aKey;
            case KeyCode.B: return kb.bKey;
            case KeyCode.C: return kb.cKey;
            case KeyCode.D: return kb.dKey;
            case KeyCode.E: return kb.eKey;
            case KeyCode.F: return kb.fKey;
            case KeyCode.G: return kb.gKey;
            case KeyCode.H: return kb.hKey;
            case KeyCode.I: return kb.iKey;
            case KeyCode.J: return kb.jKey;
            case KeyCode.K: return kb.kKey;
            case KeyCode.L: return kb.lKey;
            case KeyCode.M: return kb.mKey;
            case KeyCode.N: return kb.nKey;
            case KeyCode.O: return kb.oKey;
            case KeyCode.P: return kb.pKey;
            case KeyCode.Q: return kb.qKey;
            case KeyCode.R: return kb.rKey;
            case KeyCode.S: return kb.sKey;
            case KeyCode.T: return kb.tKey;
            case KeyCode.U: return kb.uKey;
            case KeyCode.V: return kb.vKey;
            case KeyCode.W: return kb.wKey;
            case KeyCode.X: return kb.xKey;
            case KeyCode.Y: return kb.yKey;
            case KeyCode.Z: return kb.zKey;

            case KeyCode.Alpha0: return kb.digit0Key;
            case KeyCode.Alpha1: return kb.digit1Key;
            case KeyCode.Alpha2: return kb.digit2Key;
            case KeyCode.Alpha3: return kb.digit3Key;
            case KeyCode.Alpha4: return kb.digit4Key;
            case KeyCode.Alpha5: return kb.digit5Key;
            case KeyCode.Alpha6: return kb.digit6Key;
            case KeyCode.Alpha7: return kb.digit7Key;
            case KeyCode.Alpha8: return kb.digit8Key;
            case KeyCode.Alpha9: return kb.digit9Key;

            case KeyCode.F1:  return kb.f1Key;
            case KeyCode.F2:  return kb.f2Key;
            case KeyCode.F3:  return kb.f3Key;
            case KeyCode.F4:  return kb.f4Key;
            case KeyCode.F5:  return kb.f5Key;
            case KeyCode.F6:  return kb.f6Key;
            case KeyCode.F7:  return kb.f7Key;
            case KeyCode.F8:  return kb.f8Key;
            case KeyCode.F9:  return kb.f9Key;
            case KeyCode.F10: return kb.f10Key;
            case KeyCode.F11: return kb.f11Key;
            case KeyCode.F12: return kb.f12Key;

            case KeyCode.Space:        return kb.spaceKey;
            case KeyCode.Return:       return kb.enterKey;
            case KeyCode.KeypadEnter:  return kb.numpadEnterKey;
            case KeyCode.Escape:       return kb.escapeKey;
            case KeyCode.Tab:          return kb.tabKey;
            case KeyCode.Backspace:    return kb.backspaceKey;
            case KeyCode.LeftShift:    return kb.leftShiftKey;
            case KeyCode.RightShift:   return kb.rightShiftKey;
            case KeyCode.LeftControl:  return kb.leftCtrlKey;
            case KeyCode.RightControl: return kb.rightCtrlKey;
            case KeyCode.LeftAlt:      return kb.leftAltKey;
            case KeyCode.RightAlt:     return kb.rightAltKey;

            case KeyCode.UpArrow:    return kb.upArrowKey;
            case KeyCode.DownArrow:  return kb.downArrowKey;
            case KeyCode.LeftArrow:  return kb.leftArrowKey;
            case KeyCode.RightArrow: return kb.rightArrowKey;
        }
        return null;
    }
}
