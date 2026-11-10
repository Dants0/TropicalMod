using System;
using System.IO;

namespace TropicalMod.Core
{
    /// <summary>Log em GTAV\scripts\TropicalMod.log.</summary>
    public static class Log
    {
        /// <summary>Pasta onde o TropicalMod.dll está (GTAV\scripts).</summary>
        public static readonly string ModDir = Path.GetDirectoryName(typeof(Log).Assembly.Location);

        private static readonly string FilePath = Path.Combine(ModDir, "TropicalMod.log");

        public static void Info(string msg) => Write("INFO", msg);
        public static void Error(string msg, Exception ex = null) => Write("ERRO", ex == null ? msg : msg + "\n" + ex);

        private static void Write(string level, string msg)
        {
            try
            {
                File.AppendAllText(FilePath, $"[{DateTime.Now:HH:mm:ss}] {level} {msg}\n");
            }
            catch
            {
                // log nunca pode derrubar o jogo
            }
        }
    }
}
