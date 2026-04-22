using UnityEngine;
#if ENABLE_INPUT_SYSTEM
using UnityEngine.InputSystem;
#endif

namespace FarmSimulator
{
    /// <summary>
    /// Toggles fullscreen mode with F4.
    /// Kept separate from gameplay scripts so window management stays self-contained.
    /// </summary>
    public class FullscreenToggleHotkey : MonoBehaviour
    {
        private void Update()
        {
            if (!WasTogglePressedThisFrame())
            {
                return;
            }

            ToggleFullscreen();
        }

        private static bool WasTogglePressedThisFrame()
        {
#if ENABLE_INPUT_SYSTEM
            return Keyboard.current?.f4Key.wasPressedThisFrame ?? false;
#else
            return Input.GetKeyDown(KeyCode.F4);
#endif
        }

        private static void ToggleFullscreen()
        {
            bool goFullscreen = !Screen.fullScreen;

            if (goFullscreen)
            {
                Screen.fullScreenMode = FullScreenMode.FullScreenWindow;
                Screen.fullScreen = true;
                return;
            }

            Screen.fullScreen = false;
            Screen.fullScreenMode = FullScreenMode.Windowed;
        }
    }
}
