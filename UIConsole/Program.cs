using System.Runtime.InteropServices;
using System.Timers;
using Timer = System.Timers.Timer;

class HumanMouseSimulator
{
    private static Timer timer;
    private static Random random = new Random();
    private static readonly int screenWidth = GetSystemMetrics(0);
    private static readonly int screenHeight = GetSystemMetrics(1);

    // WinAPI импорты
    [DllImport("user32.dll")]
    private static extern uint SendInput(uint nInputs, INPUT[] pInputs, int cbSize);

    [DllImport("user32.dll")]
    private static extern bool GetCursorPos(out POINT lpPoint);

    [DllImport("user32.dll")]
    private static extern int GetSystemMetrics(int nIndex);

    // Структуры данных
    [StructLayout(LayoutKind.Sequential)]
    public struct INPUT
    {
        public uint type;
        public InputUnion u;
    }

    [StructLayout(LayoutKind.Explicit)]
    public struct InputUnion
    {
        [FieldOffset(0)] public MOUSEINPUT mi;
        [FieldOffset(0)] public KEYBDINPUT ki;
    }

    [StructLayout(LayoutKind.Sequential)]
    public struct MOUSEINPUT
    {
        public int dx;
        public int dy;
        public uint mouseData;
        public uint dwFlags;
        public uint time;
        public IntPtr dwExtraInfo;
    }

    [StructLayout(LayoutKind.Sequential)]
    public struct KEYBDINPUT
    {
        public ushort wVk;
        public ushort wScan;
        public uint dwFlags;
        public uint time;
        public IntPtr dwExtraInfo;
    }

    [StructLayout(LayoutKind.Sequential)]
    public struct POINT
    {
        public int X;
        public int Y;
    }

    // Константы
    const int INPUT_MOUSE = 0;
    const int INPUT_KEYBOARD = 1;
    const uint MOUSEEVENTF_MOVE = 0x0001;
    const uint MOUSEEVENTF_ABSOLUTE = 0x8000;
    const uint KEYEVENTF_KEYUP = 0x0002;
    const ushort VK_ESCAPE = 0x1B;
    const ushort VK_NUMLOCK = 0x90;

    static void Main()
    {
        timer = new Timer();
        timer.Elapsed += TimerAction;
        SetNewInterval();
        timer.AutoReset = true;
        timer.Start();

        Console.WriteLine("Программа запущена. Нажмите Enter для выхода...");
        Console.ReadLine();
    }

    private static void TimerAction(object sender, ElapsedEventArgs e)
    {
        SmoothMoveMouse();
        PressKey(VK_ESCAPE);
        ToggleNumLock();
        SetNewInterval();
    }

    private static void SetNewInterval()
    {
        int interval = random.Next(10, 30) * 1000;
        timer.Interval = interval;
        Console.WriteLine($"Следующее действие через {interval / 1000} сек.");
    }

    private static void SmoothMoveMouse()
    {
        int targetX = random.Next(0, screenWidth);
        int targetY = random.Next(0, screenHeight);
        HumanMoveTo(targetX, targetY);
    }

    private static void HumanMoveTo(int targetX, int targetY)
    {
        const int baseSteps = 50;
        int complexity = random.Next(3, 8);
        int steps = baseSteps * complexity;

        POINT start = GetCursorPosition();
        POINT end = new POINT { X = targetX, Y = targetY };

        POINT control1 = RandomOffset(start, 100);
        POINT control2 = RandomOffset(end, 100);

        for (int i = 0; i <= steps; i++)
        {
            double t = (double)i / steps;
            t = EaseInOutCubic(t);

            POINT current = CalculateBezierPoint(t, start, control1, control2, end);
            current = ApplyHumanJitter(current, i, steps);

            SetMousePosition(current.X, current.Y);
            Thread.Sleep(random.Next(10, 20));
        }
    }

    private static POINT CalculateBezierPoint(double t, POINT p0, POINT p1, POINT p2, POINT p3)
    {
        double u = 1 - t;
        double tt = t * t;
        double uu = u * u;
        double uuu = uu * u;
        double ttt = tt * t;

        return new POINT
        {
            X = (int)(uuu * p0.X + 3 * uu * t * p1.X + 3 * u * tt * p2.X + ttt * p3.X),
            Y = (int)(uuu * p0.Y + 3 * uu * t * p1.Y + 3 * u * tt * p2.Y + ttt * p3.Y)
        };
    }

    private static POINT ApplyHumanJitter(POINT point, int step, int totalSteps)
    {
        int jitter = random.Next(-2, 3);
        int noiseX = (int)(jitter * Math.Sin(step * 0.3));
        int noiseY = (int)(jitter * Math.Cos(step * 0.3));

        double progress = (double)step / totalSteps;
        double intensity = 1.5 * Math.Sin(progress * Math.PI);

        return new POINT
        {
            X = point.X + (int)(noiseX * intensity),
            Y = point.Y + (int)(noiseY * intensity)
        };
    }

    private static double EaseInOutCubic(double t)
    {
        return t < 0.5 ? 4 * t * t * t : 1 - Math.Pow(-2 * t + 2, 3) / 2;
    }

    private static POINT RandomOffset(POINT point, int maxOffset)
    {
        return new POINT
        {
            X = point.X + random.Next(-maxOffset, maxOffset),
            Y = point.Y + random.Next(-maxOffset, maxOffset)
        };
    }

    private static POINT GetCursorPosition()
    {
        GetCursorPos(out POINT pt);
        return pt;
    }

    private static void SetMousePosition(int x, int y)
    {
        INPUT[] inputs = new INPUT[1];
        inputs[0] = new INPUT
        {
            type = INPUT_MOUSE,
            u = new InputUnion
            {
                mi = new MOUSEINPUT
                {
                    dx = (int)((x * 65535.0) / screenWidth),
                    dy = (int)((y * 65535.0) / screenHeight),
                    dwFlags = MOUSEEVENTF_ABSOLUTE | MOUSEEVENTF_MOVE,
                    time = 0,
                    dwExtraInfo = IntPtr.Zero
                }
            }
        };
        SendInput(1, inputs, Marshal.SizeOf(typeof(INPUT)));
    }

    private static void PressKey(ushort keyCode)
    {
        INPUT[] inputs = new INPUT[2];
        inputs[0] = CreateKeyInput(keyCode, false);
        inputs[1] = CreateKeyInput(keyCode, true);
        SendInput(2, inputs, Marshal.SizeOf(typeof(INPUT)));
    }

    private static INPUT CreateKeyInput(ushort keyCode, bool keyUp)
    {
        return new INPUT
        {
            type = INPUT_KEYBOARD,
            u = new InputUnion
            {
                ki = new KEYBDINPUT
                {
                    wVk = keyCode,
                    dwFlags = keyUp ? KEYEVENTF_KEYUP : 0u,
                    time = 0,
                    dwExtraInfo = IntPtr.Zero
                }
            }
        };
    }

    private static void ToggleNumLock()
    {
        INPUT[] inputs = new INPUT[2];
        inputs[0] = CreateKeyInput(VK_NUMLOCK, false);
        inputs[1] = CreateKeyInput(VK_NUMLOCK, true);
        SendInput(2, inputs, Marshal.SizeOf(typeof(INPUT)));
    }
}