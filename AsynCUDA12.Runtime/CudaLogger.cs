using System;
using System.Collections.Concurrent;
using System.Collections.Generic;
using System.ComponentModel;
using System.IO;
using System.Linq;
using System.Text;
using System.Threading;
using System.Threading.Tasks;

namespace AsynCUDA12.Runtime
{
	/// <summary>
	/// Provides a thread-safe, static logging facility for the AsynCUDA12 runtime.
	/// Messages are kept in memory (for UI data-binding) and simultaneously appended to a timestamped log file.
	/// The log file is created once per process and the directory is resolved automatically relative to the runtime assembly.
	/// </summary>
	public static class CudaLogger
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



		// Ctor (static)
		/// <summary>
		/// Initializes the <see cref="CudaLogger"/> type by resolving the log directory and creating a fresh log file,
		/// clearing any previously existing log files in that directory.
		/// </summary>
		static CudaLogger()
		{
			string logDir = EnsureLogDirectory();
			CreateLog(logDir, clearExisting: true);
		}


		/// <summary>
		/// Attempts to create a directory at the specified path.
		/// </summary>
		/// <param name="path">The path of the directory to create.</param>
		/// <param name="createdPath">When this method returns, contains the created path on success; otherwise an empty string.</param>
		/// <returns><c>true</c> if the directory was created (or already exists); otherwise <c>false</c>.</returns>
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
		/// <c>AsynCUDA12.Runtime</c> project/assembly location and falling back to the base directory
		/// or the system temp folder when necessary.
		/// </summary>
		/// <param name="differentPath">An optional explicit directory to use instead of the auto-resolved path.</param>
		/// <returns>The path to a usable log directory.</returns>
		private static string EnsureLogDirectory(string? differentPath = null)
		{
			if (!string.IsNullOrWhiteSpace(differentPath) && TryCreateDirectory(differentPath, out var customDir))
			{
				return customDir;
			}

			DirectoryInfo? current = new(AppContext.BaseDirectory);
			for (int i = 0; i < 10 && current != null; i++)
			{
				if (string.Equals(current.Name, "AsynCUDA12.Runtime", StringComparison.OrdinalIgnoreCase))
				{
					string runtimeDir = Path.Combine(current.FullName, "Logs");
					if (TryCreateDirectory(runtimeDir, out var createdRuntimeDir))
					{
						return createdRuntimeDir;
					}
				}

				string siblingRuntime = Path.Combine(current.FullName, "AsynCUDA12.Runtime");
				if (Directory.Exists(siblingRuntime))
				{
					string candidate = Path.Combine(siblingRuntime, "Logs");
					if (TryCreateDirectory(candidate, out var created))
					{
						return created;
					}
				}

				current = current.Parent;
			}

			if (TryCreateDirectory(Path.Combine(AppContext.BaseDirectory, "Logs"), out var assemblyDir))
			{
				return assemblyDir;
			}

			string fallbackPath = Path.Combine(Path.GetTempPath(), "AsynCUDA12.Runtime", "Logs");
			Directory.CreateDirectory(fallbackPath);
			return fallbackPath;
		}

		/// <summary>
		/// Creates a new timestamped log file in the given directory, optionally deleting pre-existing log files,
		/// and writes a header containing assembly, runtime, OS and CUDA environment information.
		/// </summary>
		/// <param name="logDir">The directory in which to create the log file.</param>
		/// <param name="dateFormat">The date/time format used to build the log file name.</param>
		/// <param name="clearExisting">If <c>true</c>, deletes any existing <c>*.log</c> files in <paramref name="logDir"/> before creating the new file.</param>
		private static void CreateLog(string logDir, string dateFormat = "yyyy-MM-dd_HH-mm-ss", bool clearExisting = false)
		{
			LogEntries.Clear();
			LogMessages.Clear();

			string ensuredDir = TryCreateDirectory(logDir, out var createdDir)
				? createdDir
				: EnsureLogDirectory(logDir);

			if (clearExisting)
			{
				var existingLogs = Directory.GetFiles(ensuredDir, "*.log");
				foreach (var logFile in existingLogs)
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

			string logFileName = $"log_{DateTime.Now.ToString(dateFormat)}.log";
			LogFilePath = Path.Combine(ensuredDir, logFileName);

			try
			{
				using (var fs = File.Create(LogFilePath))
				{
				}
			}
			catch
			{
				string fallbackDir = EnsureLogDirectory(Path.Combine(AppContext.BaseDirectory, "Logs"));
				LogFilePath = Path.Combine(fallbackDir, logFileName);
				try
				{
					using (var fs = File.Create(LogFilePath))
					{
					}
				}
				catch
				{
					string tempDir = EnsureLogDirectory(Path.Combine(Path.GetTempPath(), "AsynCUDA12.Runtime", "Logs"));
					LogFilePath = Path.Combine(tempDir, logFileName);
				}
			}

			try
			{
				string timestamp;
				try
				{
					timestamp = DateTime.Now.ToString(dateFormat);
				}
				catch
				{
					timestamp = DateTime.Now.ToString("yyyy-MM-dd HH:mm:ss.fff");
				}
				string assemblyInfo = System.Reflection.Assembly.GetExecutingAssembly().GetName().ToString();
				string dotnetVersion = System.Environment.Version.ToString();
				string buildDate = File.GetLastWriteTime(System.Reflection.Assembly.GetExecutingAssembly().Location).ToString("yyyy-MM-dd HH:mm:ss.fff");
				string osInfo = System.Runtime.InteropServices.RuntimeInformation.OSDescription;
				string cudaInfo = $"Version: {CudaService.CudaDriverVersion}, Available Devices: {CudaService.DeviceCount}";

				using (var writer = new StreamWriter(LogFilePath, append: true))
				{
					writer.WriteLine("=== AsynCUDA12 Log ===");
					writer.WriteLine($"Log Created: {timestamp}");
					writer.WriteLine($"Assembly Info: {assemblyInfo}");
					writer.WriteLine($".NET Version: {dotnetVersion}");
					writer.WriteLine($"Build Date: {buildDate}");
					writer.WriteLine($"OS Info: {osInfo}");
					writer.WriteLine($"CUDA Info: {cudaInfo}");
					writer.WriteLine();
					writer.WriteLine("--- Log Start ---");
					writer.WriteLine("");
				}
			}
			catch
			{
			}
		}

		/// <summary>
		/// Formats the current local time using the supplied format string, falling back to a default
		/// time format if the supplied format is invalid.
		/// </summary>
		/// <param name="format">The date/time format string.</param>
		/// <returns>The formatted current timestamp.</returns>
		private static string FormatTimestamp(string format)
		{
			try
			{
				return DateTime.Now.ToString(format);
			}
			catch
			{
				return DateTime.Now.ToString("HH:mm:ss.fff");
			}
		}

		/// <summary>
		/// Asynchronously records a message: stores it in memory and appends a timestamped line to the log file.
		/// </summary>
		/// <param name="message">The message to log. A <c>null</c> message is ignored.</param>
		/// <param name="format">The date/time format used for the line timestamp.</param>
		/// <param name="cancellationToken">A token to cancel the asynchronous file write.</param>
		/// <returns>A task representing the asynchronous logging operation.</returns>
		public static async Task LogAsync(string message, string format = "HH:mm:ss.fff", CancellationToken cancellationToken = default)
		{
			if (message is null)
			{
				return;
			}

			string timestamp = FormatTimestamp(format);
			string line = $"[{timestamp}] {message}";
			string path;
			DateTime now = DateTime.Now;

			lock (_logLock)
			{
				LogEntries[now] = message;
				if (!CudaService.SilenceLogging)
				{
					LogMessages.Add(line);
				}

				if (string.IsNullOrWhiteSpace(LogFilePath))
				{
					string dir = EnsureLogDirectory();
					LogFilePath = Path.Combine(dir, $"log_{DateTime.Now:yyyy-MM-dd_HH-mm-ss}.log");
				}

				string? dirPath = Path.GetDirectoryName(LogFilePath);
				if (!string.IsNullOrWhiteSpace(dirPath))
				{
					Directory.CreateDirectory(dirPath);
				}

				path = LogFilePath;
			}

			try
			{
				await File.AppendAllTextAsync(path, line + Environment.NewLine, cancellationToken).ConfigureAwait(false);
			}
			catch
			{
			}
		}

		/// <summary>
		/// Synchronously records a message: stores it in memory and appends a timestamped line to the log file.
		/// </summary>
		/// <param name="message">The message to log. A <c>null</c> message is ignored.</param>
		/// <param name="format">The date/time format used for the line timestamp.</param>
		public static void Log(string message, string format = "HH:mm:ss.fff")
		{
			if (message is null)
			{
				return;
			}

			string timestamp = FormatTimestamp(format);
			string line = $"[{timestamp}] {message}";

			lock (_logLock)
			{
				LogEntries[DateTime.Now] = message;
				if (!CudaService.SilenceLogging)
				{
					LogMessages.Add(line);
				}

				try
				{
					if (string.IsNullOrWhiteSpace(LogFilePath))
					{
						string dir = EnsureLogDirectory();
						LogFilePath = Path.Combine(dir, $"log_{DateTime.Now:yyyy-MM-dd_HH-mm-ss}.log");
					}

					string? dirPath = Path.GetDirectoryName(LogFilePath);
					if (!string.IsNullOrWhiteSpace(dirPath))
					{
						Directory.CreateDirectory(dirPath);
					}

					File.AppendAllText(LogFilePath, line + Environment.NewLine);
				}
				catch
				{
				}
			}
		}

		/// <summary>
		/// Logs an exception by flattening its message together with up to ten nested inner-exception messages.
		/// </summary>
		/// <param name="ex">The exception to log.</param>
		/// <param name="format">The date/time format used for the line timestamp.</param>
		public static void Log(Exception ex, string format = "HH:mm:ss.fff")
		{
			var sb = new StringBuilder();
			sb.Append(ex.Message);
			Exception? inner = ex.InnerException;
			int count = 0;
			while (inner != null && count < 10)
			{
				sb.Append(" -> ");
				sb.Append(inner.Message);
				inner = inner.InnerException;
				count++;
			}
			Log(sb.ToString(), format);
		}

		/// <summary>
		/// Asynchronously logs an exception by flattening its message together with up to ten nested inner-exception messages.
		/// </summary>
		/// <param name="ex">The exception to log. A <c>null</c> exception results in a completed no-op task.</param>
		/// <param name="format">The date/time format used for the line timestamp.</param>
		/// <param name="cancellationToken">A token to cancel the asynchronous file write.</param>
		/// <returns>A task representing the asynchronous logging operation.</returns>
		public static Task LogAsync(Exception ex, string format = "HH:mm:ss.fff", CancellationToken cancellationToken = default)
		{
			if (ex is null)
			{
				return Task.CompletedTask;
			}

			var sb = new StringBuilder();
			sb.Append(ex.Message);
			Exception? inner = ex.InnerException;
			int count = 0;
			while (inner != null && count < 10)
			{
				sb.Append(" -> ");
				sb.Append(inner.Message);
				inner = inner.InnerException;
				count++;
			}
			return LogAsync(sb.ToString(), format, cancellationToken);
		}

		/// <summary>
		/// Logs a contextual message combined with an exception and its chain of inner-exception messages.
		/// </summary>
		/// <param name="message">The contextual message describing where/why the error occurred.</param>
		/// <param name="ex">The exception to append. If <c>null</c>, only <paramref name="message"/> is logged.</param>
		/// <param name="format">The date/time format used for the line timestamp.</param>
		public static void Log(string message, Exception ex, string format = "HH:mm:ss.fff")
		{
			if (ex is null)
			{
				Log(message, format);
				return;
			}

			var sb = new StringBuilder();
			sb.Append(message);
			sb.Append(':');
			sb.Append(' ');
			sb.Append(ex.Message);
			Exception? inner = ex.InnerException;
			int count = 0;
			while (inner != null && count < 10)
			{
				sb.Append(" -> ");
				sb.Append(inner.Message);
				inner = inner.InnerException;
				count++;
			}

			Log(sb.ToString(), format);
		}

		/// <summary>
		/// Asynchronously logs a contextual message combined with an exception and its chain of inner-exception messages.
		/// </summary>
		/// <param name="message">The contextual message describing where/why the error occurred.</param>
		/// <param name="ex">The exception to append. If <c>null</c>, only <paramref name="message"/> is logged.</param>
		/// <param name="format">The date/time format used for the line timestamp.</param>
		/// <param name="cancellationToken">A token to cancel the asynchronous file write.</param>
		/// <returns>A task representing the asynchronous logging operation.</returns>
		public static Task LogAsync(string message, Exception ex, string format = "HH:mm:ss.fff", CancellationToken cancellationToken = default)
		{
			if (ex is null)
			{
				return LogAsync(message, format, cancellationToken);
			}

			var sb = new StringBuilder();
			sb.Append(message);
			sb.Append(':');
			sb.Append(' ');
			sb.Append(ex.Message);
			Exception? inner = ex.InnerException;
			int count = 0;
			while (inner != null && count < 10)
			{
				sb.Append(" -> ");
				sb.Append(inner.Message);
				inner = inner.InnerException;
				count++;
			}

			return LogAsync(sb.ToString(), format, cancellationToken);
		}
	}
}
