using System.Diagnostics;
using System.Runtime.InteropServices;

namespace EmptyLobby
{
    internal static class Program
    {
        private static bool wasOnCommandLine;

        //Most of the code comes from here
        //https://stackoverflow.com/questions/71257/suspend-process-in-c-sharp
        [Flags]
        internal enum ThreadAccess : int
        {
            TERMINATE = (0x0001),
            SUSPEND_RESUME = (0x0002),
            GET_CONTEXT = (0x0008),
            SET_CONTEXT = (0x0010),
            SET_INFORMATION = (0x0020),
            QUERY_INFORMATION = (0x0040),
            SET_THREAD_TOKEN = (0x0080),
            IMPERSONATE = (0x0100),
            DIRECT_IMPERSONATION = (0x0200)
        }

        [DllImport("kernel32.dll")]
        static extern IntPtr OpenThread(ThreadAccess desiredAccess, bool inheritHandle, uint threadId);
        [DllImport("kernel32.dll")]
        static extern uint SuspendThread(IntPtr thread);
        [DllImport("kernel32.dll")]
        static extern int ResumeThread(IntPtr thread);
        [DllImport("kernel32", CharSet = CharSet.Auto, SetLastError = true)]
        static extern bool CloseHandle(IntPtr handle);

        /// <summary>
        /// Struct containing data to store a position of the cursor on the console
        /// </summary>
        struct ConsolePosition
        {
            public short left;
            public short top;

            public static ConsolePosition Get()
            {
                ConsolePosition p;
                p.left = (short)Console.CursorLeft;
                p.top = (short)Console.CursorTop;
                return p;
            }
        }

        /// <summary>
        /// Pause the program for a set amount of seconds, and notify the user of how long is remaining
        /// </summary>
        /// <param name="seconds">How many seconds to wait</param>
        private static void CountDown(int seconds)
        {
            Console.CursorVisible = false;
            const int milli = 1000;

            int top = Console.CursorTop;
            int left = Console.CursorLeft;

            for (int i = 0; i < seconds; i++)
            {
                Console.Write($"Waiting: {seconds - i}");
                Thread.Sleep(milli);

                //Clear the console ready for drawing
                //In order to not remove all existing info on console, we can move the cursor back to where we were just drawing and draw again
                ClearLine(top);
                Console.SetCursorPosition(left, top);
            }

            Console.CursorVisible = true;
        }

        /// <summary>
        /// Clear a specific line of the console
        /// </summary>
        /// <param name="top">The line to clear. 0 being the very top line of the console</param>
        private static void ClearLine(int top)
        {
            Console.SetCursorPosition(0, top);
            Console.Write(new string(' ', Console.BufferWidth));
        }

        /// <summary>
        /// Pauses the application until any key is pressed
        /// </summary>
        private static void Pause()
        {
            if (wasOnCommandLine) return;

            Console.WriteLine("Press any key");
            Console.ReadKey();
        }

        private static void Main()
        {
            //Bit of a bodge, but we don't want to pause if this was ran on the command line, so we check if the console position is 0, 0
            wasOnCommandLine = Console.GetCursorPosition() != (0, 0);

            try
            {
                ConsolePosition pos = ConsolePosition.Get();

                Console.WriteLine("Looking For 'GTA5'");
                Process[] processes = Process.GetProcessesByName("GTA5");
                if (processes.Length == 0) throw new("No process called 'GTA5' was found. Make sure GTA V is running");

                ClearLine(pos.top);
                Console.SetCursorPosition(pos.left, pos.top);

                Process process = processes[0];
                Console.WriteLine($"Connected To Process [0x{process.MainModule.BaseAddress:X8}]");

                Console.WriteLine($"Suspending Threads ({process.Threads.Count} threads)");
                foreach (ProcessThread thread in process.Threads)
                {
                    IntPtr openThread = OpenThread(ThreadAccess.SUSPEND_RESUME, false, (uint)thread.Id);

                    if (openThread == IntPtr.Zero) continue;

                    _ = SuspendThread(openThread);
                    CloseHandle(openThread);
                }

                CountDown(9);

                Console.WriteLine($"Resuming Threads ({process.Threads.Count} threads)");
                foreach (ProcessThread thread in process.Threads)
                {
                    IntPtr openThread = OpenThread(ThreadAccess.SUSPEND_RESUME, false, (uint)thread.Id);

                    if (openThread == IntPtr.Zero) continue;

                    _ = ResumeThread(openThread);
                    CloseHandle(openThread);
                }
            }
            //If something went wrong, just give the user an error message to hopefully help them debug the issue
            catch (Exception e)
            {
                Console.ForegroundColor = ConsoleColor.Red;
                Console.WriteLine($"Failed To Run Due To Exception:\n{e.Message}");
                Console.ResetColor();
            }

            Pause();
        }
    }
}