using System;
using System.Runtime.InteropServices;
using System.Threading;

namespace TypeSimulator.Utilities
{
    /// <summary>
    /// 提供键盘输入模拟功能
    /// </summary>
    public static class KeyboardSimulator
    {
        [DllImport("user32.dll")]
        private static extern void keybd_event(byte bVk, byte bScan, int dwFlags, int dwExtraInfo);

        private const int KEYEVENTF_KEYDOWN = 0x0000;
        private const int KEYEVENTF_KEYUP = 0x0002;

        // 互斥锁，确保同一时间只发送一个键盘输入
        private static readonly object _lockObject = new object();

        // 事件系统，用于暂时禁用键盘钩子
        public static event EventHandler SimulationStarting;
        public static event EventHandler SimulationCompleted;

        /// <summary>
        /// 模拟键盘按下和释放单个按键
        /// </summary>
        public static void PressKey(byte keyCode)
        {
            lock (_lockObject)
            {
                try
                {
                    OnSimulationStarting();
                    keybd_event(keyCode, 0, KEYEVENTF_KEYDOWN, 0);
                    Thread.Sleep(5);
                    keybd_event(keyCode, 0, KEYEVENTF_KEYUP, 0);
                }
                catch (Exception ex)
                {
                    Console.WriteLine($"按键模拟失败: {ex.Message}");
                }
                finally
                {
                    OnSimulationCompleted();
                }
            }
        }

        /// <summary>
        /// 模拟按下退格键
        /// </summary>
        public static void PressBackspace()
        {
            lock (_lockObject)
            {
                try
                {
                    OnSimulationStarting();
                    System.Windows.Forms.SendKeys.SendWait("{BACKSPACE}");
                }
                catch (Exception ex)
                {
                    Console.WriteLine($"退格键模拟失败: {ex.Message}");
                }
                finally
                {
                    OnSimulationCompleted();
                }
            }
        }

        /// <summary>
        /// 模拟键入单个字符
        /// </summary>
        public static void TypeCharacter(char character)
        {
            lock (_lockObject)
            {
                try
                {
                    OnSimulationStarting();
                    string keyString;

                    switch (character)
                    {
                        case '{': keyString = @"{{}}"; break;
                        case '}': keyString = @"{}}"; break;
                        case '(': keyString = @"{(}"; break;
                        case ')': keyString = @"{)}"; break;
                        case '[': keyString = @"{[}"; break;
                        case ']': keyString = @"{]}"; break;
                        case '+': keyString = @"{+}"; break;
                        case '^': keyString = @"{^}"; break;
                        case '%': keyString = @"{%}"; break;
                        case '~': keyString = @"{~}"; break;
                        case '\n': keyString = @"{ENTER}"; break;
                        case '\t': keyString = @"{TAB}"; break;
                        default: keyString = character.ToString(); break;
                    }

                    System.Windows.Forms.SendKeys.SendWait(keyString);
                }
                catch (Exception ex)
                {
                    Console.WriteLine($"字符输入失败 '{character}': {ex.Message}");

                    try
                    {
                        System.Windows.Forms.SendKeys.Send(character.ToString());
                    }
                    catch
                    {
                        // ignore fallback errors
                    }
                }
                finally
                {
                    OnSimulationCompleted();
                }
            }
        }

        /// <summary>
        /// 模拟键入一段文本
        /// </summary>
        public static void TypeText(string text, int delayBetweenCharsMs = 0)
        {
            if (string.IsNullOrEmpty(text))
                return;

            lock (_lockObject)
            {
                try
                {
                    OnSimulationStarting();

                    foreach (char c in text)
                    {
                        TypeCharacter(c);

                        if (delayBetweenCharsMs > 0)
                        {
                            Thread.Sleep(delayBetweenCharsMs);
                        }
                    }
                }
                catch (Exception ex)
                {
                    Console.WriteLine($"文本输入失败: {ex.Message}");
                }
                finally
                {
                    OnSimulationCompleted();
                }
            }
        }

        private static void OnSimulationStarting()
        {
            SimulationStarting?.Invoke(null, EventArgs.Empty);
        }

        private static void OnSimulationCompleted()
        {
            SimulationCompleted?.Invoke(null, EventArgs.Empty);
        }
    }
}
