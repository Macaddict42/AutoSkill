using ExileCore;
using System;
using System.Runtime.InteropServices;
using System.Threading;
using System.Windows.Forms;

namespace AutoSkill
{
    internal class KeyboardHelper
    {
        private readonly GameController _gameHandle;
        private float _curLatency;

        public KeyboardHelper(GameController g)
        {
            _gameHandle = g;
        }

        public void SetLatency(float latency)
        {
            _curLatency = latency;
        }

        [DllImport("user32.dll", CharSet = CharSet.Auto, SetLastError = false)]
        private static extern IntPtr SendMessage(IntPtr hWnd, int msg, int wParam, int lParam);
        /*
                [return: MarshalAs(UnmanagedType.Bool)]
                [DllImport("user32.dll", SetLastError = true)]
                private static extern bool PostMessage(IntPtr hWnd, uint msg, UIntPtr wParam, UIntPtr lParam);
        */
        [DllImport("User32.dll")]
        public static extern short GetAsyncKeyState(Keys vKey);
        [DllImport("USER32.dll")]
        private static extern short GetKeyState(int nVirtKey);
        public void KeyDown(Keys key)
        {
            Keyboard.KeyPress(key);
        }
        
        public bool KeyPressRelease(Keys key)
        {
            KeyDown(key);
            var lat = (int)(_curLatency);
            if (lat < 1000)
            {
                Thread.Sleep(lat);
                return true;
            }
            else
            {
                Thread.Sleep(1000);
                return false;
            }
        }
/*
        private void Write(string text, params object[] args)
        {
            foreach (var character in string.Format(text, args))
            {
                PostMessage(_gameHandle.Window.Process.MainWindowHandle, 0x0102, new UIntPtr(character), UIntPtr.Zero);
            }
        }
*/
    }
}
