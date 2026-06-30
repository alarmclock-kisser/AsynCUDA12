using System;
using System.Collections.Concurrent;
using System.ComponentModel;
using System.IO;

namespace AsynCUDA.OpenCl
{
    /// <summary>
    /// Provides a thread-safe, static logging facility for the OpenCL runtime.
    /// Messages are kept in memory (for UI data-binding) and simultaneously appended to a
    /// timestamped log file. The log directory is resolved automatically relative to the assembly.
    /// CLI output is intentionally limited to successes, errors and warnings.
    /// </summary>
    public static class OpenClLogger
    {
        /// <summary>
        /// Holds every logged message keyed by the time it was recorded.
        /// </summary>
        public static readonly ConcurrentDictionary<DateTime, string> LogEntries = [];

        /// <summary>
        /// Holds the formatted log lines, exposed as a bindable list for UI consumers.
        /// </summary>
        public static readonly BindingList<string> LogMessages = [];

        /// <summary>
        /// Synchronization object guarding concurrent writes to the in-memory collections and the log file.
        /// </summary>
        private static readonly object _logLock = new();

        /// <summary>
        /// Gets the full path of the log file that the logger is currently writing to.
        /// </summary>
        public static string LogFilePath { get; private set; } = string.Empty;

        /// <summary>
        /// Gets or sets a value indicating whether log lines are echoed to the console.
        /// </summary>
        public static bool EchoToConsole { get; set; } = true;



        // Ctor (static)
        /// <summary>
        /// Initializes the <see cref="OpenClLogger"/> type by resolving the log directory and creating a fresh log file.
        /// </summary>
        static OpenClLogger()
        {
            string logDir = EnsureLogDirectory();
            CreateLog(logDir, clearExisting: true);
        }



        /// <summary>
        /// Attempts to create a directory at the specified path.
        /// </summary>
        private static bool TryCreateDirectory(string path, out string createdPath)
        {
            try
            {
                Directory.CreateDirectory(path);
                createdPath = path;
                return true;
            }
            catch
            {
                createdPath = string.Empty;
                return false;
            }
        }

        /// <summary>
        /// Resolves a writable directory for log files, preferring a "Logs" folder inside the
        /// <c>AsynCUDA.OpenCl</c> project/assembly location and falling back to the base directory
        /// or the system temp folder when necessary.
        /// </summary>
        private static string EnsureLogDirectory(string? differentPath = null)
        {
            if (!string.IsNullOrWhiteSpace(differentPath) && TryCreateDirectory(differentPath, out var customDir))
            {
                return customDir;
            }

            DirectoryInfo? current = new(AppContext.BaseDirectory);
            for (int i = 0; i < 10 && current != null; i++)
            {
                if (string.Equals(current.Name, "AsynCUDA.OpenCl", StringComparison.OrdinalIgnoreCase))
                {
                    string runtimeDir = Path.Combine(current.FullName, "Logs");
                    if (TryCreateDirectory(runtimeDir, out var createdRuntimeDir))
                    {
                        return createdRuntimeDir;
                    }
                }

                current = current.Parent;
            }

            if (TryCreateDirectory(Path.Combine(AppContext.BaseDirectory, "Logs"), out var assemblyDir))
            {
                return assemblyDir;
            }

            string fallbackPath = Path.Combine(Path.GetTempPath(), "AsynCUDA.OpenCl", "Logs");
            Directory.CreateDirectory(fallbackPath);
            return fallbackPath;
        }

        /// <summary>
        /// Creates a new timestamped log file in the given directory, optionally deleting pre-existing log files.
        /// </summary>
        private static void CreateLog(string logDir, string dateFormat = "yyyy-MM-dd_HH-mm-ss", bool clearExisting = false)
        {
            LogEntries.Clear();
            LogMessages.Clear();

            string ensuredDir = TryCreateDirectory(logDir, out var createdDir)
                ? createdDir
                : EnsureLogDirectory(logDir);

            if (clearExisting)
            {
                try
                {
                    foreach (var logFile in Directory.GetFiles(ensuredDir, "*.log"))
                    {
                        try
                        {
                            File.Delete(logFile);
                        }
                        catch
                        {
                        }
                    }
                }
                catch
                {
                }
            }

            string logFileName = $"log_{DateTime.Now.ToString(dateFormat)}.log";
            LogFilePath = Path.Combine(ensuredDir, logFileName);

            try
            {
                using (File.Create(LogFilePath))
                {
                }
            }
            catch
            {
                string tempDir = EnsureLogDirectory(Path.Combine(Path.GetTempPath(), "AsynCUDA.OpenCl", "Logs"));
                LogFilePath = Path.Combine(tempDir, logFileName);
                try
                {
                    using (File.Create(LogFilePath))
                    {
                    }
                }
                catch
                {
                }
            }
        }

        /// <summary>
        /// Writes a single classified line to the in-memory collections, the log file and (optionally) the console.
        /// </summary>
        private static void Write(string level, string message, string format)
        {
            DateTime now = DateTime.Now;
            string line = $"[{now.ToString(format)}] [{level}] {message}";

            lock (_logLock)
            {
                LogEntries[now] = line;
                LogMessages.Add(line);

                try
                {
                    if (!string.IsNullOrEmpty(LogFilePath))
                    {
                        File.AppendAllText(LogFilePath, line + Environment.NewLine);
                    }
                }
                catch
                {
                }

                if (EchoToConsole)
                {
                    Console.WriteLine(line);
                }
            }
        }

        /// <summary>
        /// Logs an informational message.
        /// </summary>
        public static void Log(string message, string format = "HH:mm:ss.fff")
        {
            Write("INFO", message, format);
        }

        /// <summary>
        /// Logs a success message.
        /// </summary>
        public static void LogSuccess(string message, string format = "HH:mm:ss.fff")
        {
            Write("OK", message, format);
        }

        /// <summary>
        /// Logs a warning message.
        /// </summary>
        public static void LogWarning(string message, string format = "HH:mm:ss.fff")
        {
            Write("WARN", message, format);
        }

        /// <summary>
        /// Logs an error message.
        /// </summary>
        public static void LogError(string message, string format = "HH:mm:ss.fff")
        {
            Write("ERROR", message, format);
        }

        /// <summary>
        /// Logs an exception as an error.
        /// </summary>
        public static void Log(Exception ex, string format = "HH:mm:ss.fff")
        {
            Write("ERROR", ex.Message, format);
        }

        /// <summary>
        /// Logs a message together with the associated exception as an error.
        /// </summary>
        public static void Log(string message, Exception ex, string format = "HH:mm:ss.fff")
        {
            Write("ERROR", $"{message} - {ex.Message}", format);
        }
    }
}
