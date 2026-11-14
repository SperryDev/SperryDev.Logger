using Microsoft.Extensions.Hosting;

using Serilog;
using Serilog.Core;
using Serilog.Events;

using System;

namespace SperryDev.Logger.Configuration
{
    public static class HostBuilderExtensions
    {
        /// <summary>
        /// Replaces the host logging with Serilog configured for the channel (see <see cref="AppLoggerFactory"/>).
        /// </summary>
        /// <param name="configure">Optional tweaks applied before the logger is built, for example a MinimumLevel.Override for a noisy category.</param>
        public static IHostBuilder UseAppLogging(
            this IHostBuilder host,
            string channel,
            LogEventLevel minimumLevel = LogEventLevel.Information,
            Action<LoggerConfiguration>? configure = null)
        {
            LoggerConfiguration config = AppLoggerFactory.BuildConfiguration(channel, minimumLevel);
            configure?.Invoke(config);

            // Pass the built logger straight to UseSerilog. Wrapping it as a sink of an outer logger skips its
            // MinimumLevel.Override checks, which would make `configure` overrides silently dead.
            Serilog.Core.Logger built = config.CreateLogger();
            return host.UseSerilog(built, dispose: true);
        }
    }
}
