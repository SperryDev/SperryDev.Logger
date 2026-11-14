using Serilog.Core;
using Serilog.Events;

using System;
using System.Globalization;
using System.IO;
using System.Text;
using System.Text.Encodings.Web;
using System.Text.Json;

namespace SperryDev.Logger.Providers
{
    /// <summary>
    /// Writes structured JSON log lines to stdout. The VS Code extension reads the child process's stdout stream
    /// and routes entries to the correct output channel from the "ch" field.
    /// One JSON object per line: {"ts":"2026-06-26T12:00:00Z","lvl":"INF","ch":"TokenSaver","src":"Category","msg":"..."}.
    /// </summary>
    public sealed class VsCodeSink : ILogEventSink
    {
        private static readonly JsonWriterOptions JsonOptions = new JsonWriterOptions { Encoder = JavaScriptEncoder.UnsafeRelaxedJsonEscaping };

        /// <summary>Writes one entry as a single JSON line.</summary>
        public void Emit(LogEvent logEvent)
        {
            using MemoryStream ms = new MemoryStream();
            using (Utf8JsonWriter writer = new Utf8JsonWriter(ms, JsonOptions))
            {
                writer.WriteStartObject();
                writer.WriteString("ts", logEvent.Timestamp.UtcDateTime.ToString("O", CultureInfo.InvariantCulture));
                writer.WriteString("lvl", LevelCode(logEvent.Level));

                if (logEvent.Properties.TryGetValue("Channel", out LogEventPropertyValue? channel))
                {
                    writer.WriteString("ch", channel.ToString().Trim('"'));
                }

                if (logEvent.Properties.TryGetValue("SourceContext", out LogEventPropertyValue? source))
                {
                    writer.WriteString("src", source.ToString().Trim('"'));
                }

                writer.WriteString("msg", logEvent.RenderMessage(CultureInfo.InvariantCulture));

                if (logEvent.Exception is not null)
                {
                    writer.WriteString("ex", logEvent.Exception.ToString());
                }

                writer.WriteEndObject();
            }

            // One write so the extension can split on newlines.
            Console.WriteLine(Encoding.UTF8.GetString(ms.ToArray()));
        }

        private static string LevelCode(LogEventLevel level)
        {
            return level switch
            {
                LogEventLevel.Verbose => "VRB",
                LogEventLevel.Debug => "DBG",
                LogEventLevel.Information => "INF",
                LogEventLevel.Warning => "WRN",
                LogEventLevel.Error => "ERR",
                LogEventLevel.Fatal => "FTL",
                _ => "INF",
            };
        }
    }
}
