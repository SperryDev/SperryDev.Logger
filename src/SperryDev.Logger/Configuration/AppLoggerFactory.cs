using Microsoft.Extensions.Logging;

using Serilog;
using Serilog.Core;
using Serilog.Events;
using Serilog.Extensions.Logging;

using System;
using System.IO;
using System.Security.AccessControl;
using System.Security.Principal;

using SperryDev.Logger.Providers;

namespace SperryDev.Logger.Configuration
{
    /// <summary>
    /// Builds a Serilog configuration for a named channel (an app or subsystem, for example "TokenSaver").
    /// Sinks: console (every line prefixed with [channel]); a rolling daily file under
    /// %ProgramData%\&lt;channel&gt;\logs, kept 14 days; the Windows Application Event Log under the channel's source;
    /// and <see cref="VsCodeSink"/> when the VSCODE_LOGS environment variable is set.
    /// </summary>
    public static class AppLoggerFactory
    {
        private const int RetainedFileCount = 14;

        /// <summary>Builds the logger configuration for the channel. Callers may add overrides before creating the logger.</summary>
        public static LoggerConfiguration BuildConfiguration(string channel, LogEventLevel minimumLevel = LogEventLevel.Information)
        {
            if (string.IsNullOrWhiteSpace(channel))
            {
                throw new ArgumentException("Channel must not be empty.", nameof(channel));
            }

            LoggerConfiguration cfg = new LoggerConfiguration()
                .MinimumLevel.Is(minimumLevel)
                .Enrich.WithProperty("Channel", channel)
                .Enrich.FromLogContext();

            _ = cfg.WriteTo.Console(
                outputTemplate: "[" + channel + "] {Timestamp:HH:mm:ss} {Level:u3} {SourceContext:l}: {Message:lj}{NewLine}{Exception}",
                restrictedToMinimumLevel: minimumLevel);

            // shared: true so a dev-loop run and the installed service cannot fail on a file lock when both touch the same file.
            string logDirectory = GetLogDirectory(channel);
            EnsureLogDirectoryWritableByAnyLaunchContext(logDirectory);
            _ = cfg.WriteTo.File(
                Path.Combine(logDirectory, channel.ToLowerInvariant() + "-.log"),
                outputTemplate: "{Timestamp:yyyy-MM-dd HH:mm:ss.fff zzz} [{Level:u3}] {SourceContext:l}: {Message:lj}{NewLine}{Exception}",
                restrictedToMinimumLevel: minimumLevel,
                rollingInterval: RollingInterval.Day,
                retainedFileCountLimit: RetainedFileCount,
                shared: true);

            if (OperatingSystem.IsWindows())
            {
                _ = cfg.WriteTo.EventLog(
                    channel,
                    "Application",
                    manageEventSource: false,
                    restrictedToMinimumLevel: minimumLevel);
            }

            if (!string.IsNullOrEmpty(Environment.GetEnvironmentVariable("VSCODE_LOGS")))
            {
                _ = cfg.WriteTo.Sink(new VsCodeSink(), minimumLevel);
            }

            return cfg;
        }

        /// <summary>Builds the configuration and returns an <see cref="ILoggerFactory"/> that owns the Serilog logger.</summary>
        public static ILoggerFactory Create(string channel, LogEventLevel minimumLevel = LogEventLevel.Information)
        {
            Serilog.Core.Logger logger = BuildConfiguration(channel, minimumLevel).CreateLogger();
            return new SerilogLoggerFactory(logger, true);
        }

        /// <summary>The log folder for a channel: %ProgramData%\&lt;channel&gt;\logs.</summary>
        public static string GetLogDirectory(string channel)
        {
            return Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.CommonApplicationData), channel, "logs");
        }

        /// <summary>
        /// The log folder can be created by three launch contexts that do not share a Windows identity: the installed
        /// service (SYSTEM), an elevated run, or a plain user run. Whichever creates it first gives it its own default ACL,
        /// which for SYSTEM or an elevated creator grants a plain user only Read. Later writes from another context then
        /// fail with UnauthorizedAccessException, which Serilog's file sink swallows, so file logging goes dark silently.
        /// This grants Users Modify at creation time; the ACL cannot be repaired afterwards from a lower-privileged context.
        /// </summary>
        public static void EnsureLogDirectoryWritableByAnyLaunchContext(string logDirectory)
        {
            if (Directory.Exists(logDirectory))
            {
                return;
            }

            _ = Directory.CreateDirectory(logDirectory);

            if (OperatingSystem.IsWindows())
            {
                TryGrantUsersModify(logDirectory);
            }
        }

        [System.Runtime.Versioning.SupportedOSPlatform("windows")]
        private static void TryGrantUsersModify(string directory)
        {
            try
            {
                DirectoryInfo info = new DirectoryInfo(directory);
                DirectorySecurity security = info.GetAccessControl();
                SecurityIdentifier users = new SecurityIdentifier(WellKnownSidType.BuiltinUsersSid, null);
                FileSystemAccessRule rule = new FileSystemAccessRule(
                    users,
                    FileSystemRights.Modify,
                    InheritanceFlags.ObjectInherit | InheritanceFlags.ContainerInherit,
                    PropagationFlags.None,
                    AccessControlType.Allow);
                security.AddAccessRule(rule);
                info.SetAccessControl(security);
            }
            catch (Exception)
            {
                // Best effort: fall back to the inherited permissions. A logging nicety must never block startup.
            }
        }
    }
}
