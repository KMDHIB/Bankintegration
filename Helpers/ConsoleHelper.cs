using System.Diagnostics;

namespace BankIntegration
{
    public static class ConsoleHelper
    {
        /// <summary>
        /// Initializes the console window.
        /// </summary>
        public static void Init()
        {
            Console.ForegroundColor = ConsoleColor.Cyan;
            Console.BackgroundColor = ConsoleColor.DarkBlue;
            if (!Debugger.IsAttached)
            {
                try
                {
                    Console.Clear();
                }
                catch
                {
                    // Ignore clear errors when output is piped
                }
            }
        }

        /// <summary>
        /// Writes a string to the console window.
        /// </summary>
        /// <param name="writestuff"></param>
        /// <param name="color"></param>
        public static void Write(string writestuff, ConsoleColor color = ConsoleColor.Cyan)
        {
            if (color != ConsoleColor.Cyan)
            {
                Console.ForegroundColor = color;
            }

            foreach (char c in writestuff)
            {
                Console.Write(c);
            }

            Console.WriteLine();

            if (Console.ForegroundColor != ConsoleColor.Cyan)
            {
                Console.ForegroundColor = ConsoleColor.Cyan;
            }
        }
    }
}