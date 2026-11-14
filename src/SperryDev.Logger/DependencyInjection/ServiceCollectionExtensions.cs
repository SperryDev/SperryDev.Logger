using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Logging.Abstractions;

using Serilog;

using System;

using SperryDev.Logger.Providers;
using SperryDev.Logger.Services;

namespace SperryDev.Logger.DependencyInjection
{
    public static class ServiceCollectionExtensions
    {
        private const int DefaultInMemoryBufferCapacity = 5000;


        /// <summary>
        /// Adds the in-memory log buffer to the dependency injection container as a singleton.
        /// The buffer is registered with the default capacity of 5000 entries.
        /// </summary>
        public static IServiceCollection AddInMemoryLogBuffer(this IServiceCollection services)
        {
            _ = services.AddSingleton(new InMemoryLogBuffer(capacity: DefaultInMemoryBufferCapacity));
            return services;
        }

        /// <summary>
        /// Adds the in-memory log buffer to the dependency injection container as a singleton with custom capacity.
        /// </summary>
        public static IServiceCollection AddInMemoryLogBuffer(this IServiceCollection services, int capacity)
        {
            if (capacity <= 0)
            {
                throw new ArgumentException("Capacity must be greater than 0", nameof(capacity));
            }

            _ = services.AddSingleton(new InMemoryLogBuffer(capacity: capacity));
            return services;
        }

        /// <summary>
        /// Wires the static Serilog Log.Logger into the logging pipeline. Optionally enables Windows Event Log
        /// and/or Linux syslog for unattended-service visibility. Also registers the in-memory buffer provider.
        ///
        /// NOTE: configure Log.Logger (file sinks etc.) BEFORE calling this method.
        /// </summary>
        public static ILoggingBuilder AddVideoForensicsLogging(
            this ILoggingBuilder logging,
            bool enableEventLog = false,
            bool enableSyslog = false,
            bool enableNamedPipeLogger = false,
            Action<ILoggingBuilder>? eventLogProvider = null,
            NamedPipeLogGate? namedPipeGate = null,
            string syslogAppName = "SperryDev")
        {
            _ = logging.AddSerilog(dispose: true);

            if (enableEventLog && OperatingSystem.IsWindows() && eventLogProvider is not null)
            {
                eventLogProvider(logging);
            }

            if (enableSyslog && OperatingSystem.IsLinux())
            {
                // Additional syslog configuration (independent of file sinks).
                Serilog.Core.Logger syslogLogger = new LoggerConfiguration()
                    .WriteTo.LocalSyslog(appName: syslogAppName)
                    .CreateLogger();
                _ = logging.AddSerilog(syslogLogger, dispose: true);
            }

            if (enableNamedPipeLogger && OperatingSystem.IsWindows())
            {
                _ = logging.AddProvider(new NamedPipeLoggerProvider(namedPipeGate));
            }

            // Resolve the buffer lazily from the real container so registration order doesn't matter and the
            // provider writes to the same singleton the endpoints read (no throwaway service provider).
            _ = logging.Services.AddSingleton<ILoggerProvider>(sp =>
                sp.GetService<InMemoryLogBuffer>() is { } buffer
                    ? new InMemoryLogBufferProvider(buffer, sp.GetService<LogLevelSource>())
                    : NullLoggerProvider.Instance);
            return logging;
        }

        /// <summary>
        /// Adds the NamedPipeLoggerProvider for Logger Viewer client consumption.
        /// Windows-only; no-op on other platforms.
        /// </summary>
        public static ILoggingBuilder AddNamedPipeLogger(this ILoggingBuilder logging)
        {
            if (OperatingSystem.IsWindows())
            {
                _ = logging.AddProvider(new NamedPipeLoggerProvider());
            }

            return logging;
        }
    }
}
