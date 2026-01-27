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
	public static class CudaLogger
	{
		public static readonly ConcurrentDictionary<DateTime, string> LogEntries = [];
		public static readonly BindingList<string> LogMessages = [];
		private static readonly object _logLock = new();

		public static string LogFilePath { get; private set; } = string.Empty;



		// Ctor (static)
		static CudaLogger()
		{
			string logDir = EnsureLogDirectory();
			CreateLog(logDir, clearExisting: true);
		}


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

		public static void Log(Exception ex, string format = "HH:mm:ss.fff")
		{
			string message = ex.Message;
			Exception? inner = ex.InnerException;
			int count = 0;
			while (inner != null && count < 10)
			{
				message += $" ({inner.Message}";
				inner = inner.InnerException;
				count++;
			}
			message += new string(')', count);
			Log(message, format);
		}

		public static Task LogAsync(Exception ex, string format = "HH:mm:ss.fff", CancellationToken cancellationToken = default)
		{
			if (ex is null)
			{
				return Task.CompletedTask;
			}

			string message = ex.Message;
			Exception? inner = ex.InnerException;
			int count = 0;
			while (inner != null && count < 10)
			{
				message += $" ({inner.Message}";
				inner = inner.InnerException;
				count++;
			}
			message += new string(')', count);
			return LogAsync(message, format, cancellationToken);
		}

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
