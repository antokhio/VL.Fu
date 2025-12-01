using System.Runtime.InteropServices;
using VL.Core.Import;

namespace VL.Fu.WinForms.Hooks
{
    [ProcessNode(FragmentSelection = FragmentSelection.Explicit)]
    public class DisableNativeGestures : IDisposable
    {
        [DllImport("user32.dll")]
        [return: MarshalAs(UnmanagedType.Bool)]
        private static extern bool RegisterTouchWindow(IntPtr hWnd, uint ulFlags);

        [DllImport("user32.dll")]
        [return: MarshalAs(UnmanagedType.Bool)]
        private static extern bool UnregisterTouchWindow(IntPtr hWnd);

        [DllImport("kernel32.dll", SetLastError = true, CharSet = CharSet.Auto)]
        private static extern ushort GlobalAddAtom(string lpString);

        [DllImport("user32.dll", CharSet = CharSet.Auto, SetLastError = true)]
        private static extern bool SetProp(IntPtr hWnd, string lpString, IntPtr hData);

        // Constants
        private const int TWF_FINETOUCH = 0x00000001;
        private const string ATOM_PROP_NAME = "MicrosoftTabletPenServiceProperty";

        private const int WM_TABLET_QUERYSYSTEMGESTURESTATUS = 0x02CC;
        private const int WM_GESTURENOTIFY = 0x011A;
        private const int WM_GESTURE = 0x0119;

        // 0x00000001 = TABLET_DISABLE_PRESSANDHOLD
        // 0x00000008 = TABLET_DISABLE_PENTAPFEEDBACK
        // 0x00000010 = TABLET_DISABLE_PENBARRELFEEDBACK
        // 0x00010000 = TABLET_DISABLE_FLICKS
        // 0x00080000 = TABLET_DISABLE_SMOOTHSCROLLING
        private const int DISABLE_MASK = 0x00090019;

        private Form _form;
        private bool _hideCursor;
        private bool _cursorHidden;
        private readonly List<TabletEventHook> _hooks = new();

        [Fragment]
        public DisableNativeGestures() { }

        [Fragment]
        public void Update(Form form, bool hideCursor = false)
        {
            _hideCursor = hideCursor;
            UpdateCursorVisibility();

            if (form != _form)
            {
                Cleanup();
                _form = form;

                if (_form != null)
                {
                    _form.HandleCreated += OnHandleCreated;
                    if (_form.IsHandleCreated)
                    {
                        HookRecursively(_form);
                    }
                }
            }
        }

        private void OnHandleCreated(object sender, EventArgs e)
        {
            if (_form != null)
                HookRecursively(_form);
        }

        private void HookRecursively(Control c)
        {
            if (c.IsHandleCreated)
            {
                ApplyToHandle(c.Handle, c.GetType().Name);
            }
            // SkiaRenderer might not have child controls, but we keep this generic
            foreach (Control child in c.Controls)
            {
                HookRecursively(child);
            }
        }

        private void ApplyToHandle(IntPtr handle, string debugName)
        {
            try
            {
                RegisterTouchWindow(handle, TWF_FINETOUCH);

                ushort atom = GlobalAddAtom(ATOM_PROP_NAME);
                if (atom != 0)
                {
                    SetProp(handle, ATOM_PROP_NAME, (IntPtr)DISABLE_MASK);
                }

                if (!_hooks.Any(h => h.Handle == handle))
                {
                    Console.WriteLine($"[NativeGestures] Hooking {debugName} ({handle})");
                    _hooks.Add(new TabletEventHook(handle));
                }
            }
            catch { }
        }

        private void UpdateCursorVisibility()
        {
            if (_hideCursor && !_cursorHidden)
            {
                Cursor.Hide();
                _cursorHidden = true;
            }
            else if (!_hideCursor && _cursorHidden)
            {
                Cursor.Show();
                _cursorHidden = false;
            }
        }

        private void Cleanup()
        {
            foreach (var hook in _hooks)
                hook.ReleaseHandle();
            _hooks.Clear();

            if (_form != null)
            {
                _form.HandleCreated -= OnHandleCreated;
                if (_form.IsHandleCreated)
                {
                    try
                    {
                        UnregisterTouchWindow(_form.Handle);
                    }
                    catch { }
                }
            }
        }

        public void Dispose()
        {
            if (_cursorHidden)
            {
                Cursor.Show();
                _cursorHidden = false;
            }
            Cleanup();
            _form = null;
        }

        private class TabletEventHook : NativeWindow
        {
            public TabletEventHook(IntPtr handle)
            {
                AssignHandle(handle);
            }

            protected override void WndProc(ref Message m)
            {
                switch (m.Msg)
                {
                    case WM_TABLET_QUERYSYSTEMGESTURESTATUS:
                        m.Result = (IntPtr)DISABLE_MASK;
                        return;

                    case WM_GESTURENOTIFY:
                        // Tells the OS we are handling gestures, don't use defaults.
                        // Although RegisterTouchWindow usually stops this, sometimes this helps.
                        // By not calling base, we might stop the default gesture processing.
                        break;

                    case WM_GESTURE:
                        // Consume it so it doesn't bubble up
                        m.Result = IntPtr.Zero;
                        return;
                }
                base.WndProc(ref m);
            }
        }
    }
}
