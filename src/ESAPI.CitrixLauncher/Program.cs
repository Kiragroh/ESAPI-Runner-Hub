using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Globalization;
using System.IO;
using System.Linq;
using System.Runtime.InteropServices;
using System.Text;
using System.Text.RegularExpressions;
using System.Windows.Forms;

namespace EsapiCitrixLauncher
{
    internal static class Program
    {
        private const string SuppressUiEnvironmentKey = "ESAPI_RUNNER_CITRIX_LAUNCHER_SUPPRESS_UI";
        private static readonly Regex PointerPattern = new Regex(
            @"\AESAPI-Runner-Hub\.v\d+\.\d+\.\d+\.exe\z",
            RegexOptions.CultureInvariant);

        [STAThread]
        private static int Main(string[] args)
        {
            string root = null;
            string logDirectory = null;
            try
            {
                var launcherDirectory = Path.GetFullPath(AppDomain.CurrentDomain.BaseDirectory);
                root = Directory.GetParent(launcherDirectory.TrimEnd(Path.DirectorySeparatorChar, Path.AltDirectorySeparatorChar))?.FullName;
                if (string.IsNullOrWhiteSpace(root))
                {
                    return Fail(24, "working_directory", "Das Installationsverzeichnis konnte nicht bestimmt werden.", null);
                }

                var pointerPath = Path.Combine(launcherDirectory, "current.txt");
                var settingsPath = Path.Combine(root, "dist", "settings.ini");
                logDirectory = ResolveLogDirectory(settingsPath, root);

                if (!File.Exists(pointerPath))
                {
                    return Fail(20, "pointer_missing", "Die Versionsauswahl current.txt fehlt.", logDirectory);
                }

                var pointer = File.ReadAllText(pointerPath).TrimEnd('\r', '\n');
                if (!PointerPattern.IsMatch(pointer))
                {
                    return Fail(21, "pointer_invalid", "current.txt enthält keinen gültigen Versions-Dateinamen.", logDirectory);
                }

                var versionsDirectory = Path.Combine(root, "dist", "versions");
                var targetPath = Path.Combine(versionsDirectory, pointer);
                if (!File.Exists(targetPath))
                {
                    return Fail(22, "target_missing", "Die ausgewählte Programmversion wurde nicht gefunden.", logDirectory);
                }

                if (!File.Exists(settingsPath))
                {
                    return Fail(23, "settings_missing", "Die gemeinsame settings.ini wurde nicht gefunden.", logDirectory);
                }

                var forwardedArguments = ExpandPackedCitrixArgument(args);
                var childArguments = new List<string> { "--settings", settingsPath };
                childArguments.AddRange(forwardedArguments);

                WriteLog(logDirectory, "START release=" + pointer);
                using (var process = new Process())
                {
                    process.StartInfo = new ProcessStartInfo
                    {
                        FileName = targetPath,
                        Arguments = BuildCommandLine(childArguments),
                        WorkingDirectory = versionsDirectory,
                        UseShellExecute = false,
                        CreateNoWindow = true
                    };
                    process.Start();
                    process.WaitForExit();
                    WriteLog(logDirectory, "EXIT release=" + pointer + " code=" + process.ExitCode.ToString(CultureInfo.InvariantCulture));
                    return process.ExitCode;
                }
            }
            catch (Exception exception)
            {
                WriteLog(logDirectory, "ERROR code=25 reason=launcher_failure type=" + exception.GetType().Name);
                ShowError("Der ESAPI Runner Hub konnte nicht gestartet werden.");
                return 25;
            }
        }

        private static int Fail(int code, string reason, string message, string logDirectory)
        {
            WriteLog(logDirectory, "ERROR code=" + code.ToString(CultureInfo.InvariantCulture) + " reason=" + reason);
            ShowError(message);
            return code;
        }

        private static void ShowError(string message)
        {
            if (string.Equals(Environment.GetEnvironmentVariable(SuppressUiEnvironmentKey), "1", StringComparison.Ordinal))
            {
                return;
            }

            MessageBox.Show(message, "ESAPI Runner Hub", MessageBoxButtons.OK, MessageBoxIcon.Error);
        }

        private static string ResolveLogDirectory(string settingsPath, string root)
        {
            var configured = ReadIniValue(settingsPath, "LogDirectory");
            var value = string.IsNullOrWhiteSpace(configured)
                ? @"%LOCALAPPDATA%\ESAPI-Runner-Hub\Logs"
                : configured.Trim();
            value = Environment.ExpandEnvironmentVariables(value);
            return Path.IsPathRooted(value) ? Path.GetFullPath(value) : Path.GetFullPath(Path.Combine(root, value));
        }

        private static string ReadIniValue(string path, string key)
        {
            if (!File.Exists(path))
            {
                return null;
            }

            foreach (var rawLine in File.ReadLines(path))
            {
                var line = rawLine.Trim();
                if (line.Length == 0 || line.StartsWith(";", StringComparison.Ordinal) || line.StartsWith("#", StringComparison.Ordinal))
                {
                    continue;
                }

                var separator = line.IndexOf('=');
                if (separator <= 0 || !string.Equals(line.Substring(0, separator).Trim(), key, StringComparison.OrdinalIgnoreCase))
                {
                    continue;
                }

                return line.Substring(separator + 1).Trim();
            }

            return null;
        }

        private static void WriteLog(string logDirectory, string message)
        {
            try
            {
                var directory = string.IsNullOrWhiteSpace(logDirectory)
                    ? Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData), "ESAPI-Runner-Hub", "Logs")
                    : logDirectory;
                Directory.CreateDirectory(directory);
                var localAppData = Path.GetFullPath(Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData));
                var resolvedDirectory = Path.GetFullPath(directory);
                var isLocal = resolvedDirectory.StartsWith(localAppData.TrimEnd(Path.DirectorySeparatorChar) + Path.DirectorySeparatorChar, StringComparison.OrdinalIgnoreCase);
                var fileName = isLocal ? "CitrixLauncher.log" : "CitrixLauncher-" + Environment.MachineName + ".log";
                File.AppendAllText(
                    Path.Combine(directory, fileName),
                    "[" + DateTime.Now.ToString("yyyy-MM-dd HH:mm:ss.fff", CultureInfo.InvariantCulture) + "] " + message + Environment.NewLine,
                    new UTF8Encoding(false));
            }
            catch
            {
                // Optional logging must never prevent launch or shutdown.
            }
        }

        private static string[] ExpandPackedCitrixArgument(string[] args)
        {
            if (args == null || args.Length != 1 || string.IsNullOrWhiteSpace(args[0]))
            {
                return args ?? Array.Empty<string>();
            }

            var packed = args[0].Trim();
            if (!packed.StartsWith("--", StringComparison.Ordinal) || packed.IndexOfAny(new[] { ' ', '\t', '"' }) < 0)
            {
                return args;
            }

            return ParseWindowsCommandLine(packed);
        }

        private static string[] ParseWindowsCommandLine(string commandLine)
        {
            var pointer = CommandLineToArgvW(commandLine, out var count);
            if (pointer == IntPtr.Zero)
            {
                throw new InvalidOperationException("The packed Citrix argument could not be parsed.");
            }

            try
            {
                var result = new string[count];
                for (var index = 0; index < count; index++)
                {
                    result[index] = Marshal.PtrToStringUni(Marshal.ReadIntPtr(pointer, index * IntPtr.Size));
                }
                return result;
            }
            finally
            {
                LocalFree(pointer);
            }
        }

        private static string BuildCommandLine(IEnumerable<string> arguments)
        {
            return string.Join(" ", arguments.Select(QuoteWindowsArgument));
        }

        private static string QuoteWindowsArgument(string value)
        {
            value = value ?? string.Empty;
            if (value.Length > 0 && value.All(character => !char.IsWhiteSpace(character) && character != '"'))
            {
                return value;
            }

            var builder = new StringBuilder("\"");
            var backslashes = 0;
            foreach (var character in value)
            {
                if (character == '\\')
                {
                    backslashes++;
                    continue;
                }

                if (character == '"')
                {
                    builder.Append('\\', backslashes * 2 + 1);
                    builder.Append('"');
                    backslashes = 0;
                    continue;
                }

                builder.Append('\\', backslashes);
                backslashes = 0;
                builder.Append(character);
            }

            builder.Append('\\', backslashes * 2);
            builder.Append('"');
            return builder.ToString();
        }

        [DllImport("shell32.dll", SetLastError = true)]
        private static extern IntPtr CommandLineToArgvW([MarshalAs(UnmanagedType.LPWStr)] string commandLine, out int argumentCount);

        [DllImport("kernel32.dll")]
        private static extern IntPtr LocalFree(IntPtr memory);
    }
}
