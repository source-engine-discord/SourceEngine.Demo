using System;

namespace SourceEngine.Demo.Stats.App
{
    internal static class Debug
    {
        public static void Log(string msg, params object[] args)
        {
            Print(string.Format(msg, args), ConsoleColor.Gray, DebugLevel.High, "log");
        }

        public static void Info(string msg, params object[] args)
        {
            Print(string.Format(msg, args), ConsoleColor.White, DebugLevel.Normal, "INFO");
        }

        public static void Success(string msg, params object[] args)
        {
            Print(string.Format(msg, args), ConsoleColor.Cyan, DebugLevel.Low, "Success");
        }

        public static void Error(string msg, params object[] args)
        {
            Print(string.Format(msg, args), ConsoleColor.Red, DebugLevel.Low, "Error");
        }

        public static void Warn(string msg, params object[] args)
        {
            Print(string.Format(msg, args), ConsoleColor.DarkCyan, DebugLevel.Normal, "Warn");
        }

        public static void Headings(string msg, params object[] args)
        {
            Print(string.Format(msg, args), ConsoleColor.White, DebugLevel.Normal, "", false);
        }

        public static void Blue(string msg, params object[] args)
        {
            Print(string.Format(msg, args), ConsoleColor.DarkGray, DebugLevel.Normal, "", false, true);
        }

        public static void White(string msg, params object[] args)
        {
            Print(string.Format(msg, args), ConsoleColor.White, DebugLevel.Normal, "", false, true);
        }

        private static void Print(
            string message,
            ConsoleColor c,
            DebugLevel lvl = DebugLevel.Low,
            string prefix = "",
            bool usedate = true,
            bool bluepost = false)
        {
            if (bluepost)
            {
                Console.ForegroundColor = c;
                Console.Write(message);
                Console.ForegroundColor = ConsoleColor.Gray;
                return;
            }

            if (lvl <= DebugLevel.High)
            {
                Console.ForegroundColor = c;
                if (usedate)
                    Console.Write("[" + DateTime.Now.ToShortTimeString() + " | " + prefix + "] ");

                Console.Write(message + "\n");
                Console.ForegroundColor = ConsoleColor.Gray;
            }
        }

        private enum DebugLevel
        {
            Low,
            Normal,
            High,
        }
    }
}
