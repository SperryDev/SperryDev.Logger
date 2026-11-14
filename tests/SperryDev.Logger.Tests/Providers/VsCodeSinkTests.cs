using Serilog;
using Serilog.Events;

using SperryDev.Logger.Configuration;
using SperryDev.Logger.Providers;

using System;
using System.IO;
using System.Text.Json;

using Xunit;

namespace SperryDev.Logger.Tests.Providers
{
    public sealed class VsCodeSinkTests
    {
        [Fact]
        public void Emit_WritesOneJsonLineWithChannelSourceAndMessage()
        {
            TextWriter original = Console.Out;
            using StringWriter captured = new StringWriter();
            Console.SetOut(captured);
            try
            {
                using Serilog.Core.Logger log = new LoggerConfiguration()
                    .Enrich.WithProperty("Channel", "TokenSaver")
                    .WriteTo.Sink(new VsCodeSink())
                    .CreateLogger();

                log.ForContext("SourceContext", "My.Category").Warning("hello {Name}", "world");
            }
            finally
            {
                Console.SetOut(original);
            }

            string line = captured.ToString().TrimEnd();
            Assert.DoesNotContain('\n', line);
            using JsonDocument doc = JsonDocument.Parse(line);
            Assert.Equal("WRN", doc.RootElement.GetProperty("lvl").GetString());
            Assert.Equal("TokenSaver", doc.RootElement.GetProperty("ch").GetString());
            Assert.Equal("My.Category", doc.RootElement.GetProperty("src").GetString());
            Assert.Equal("hello \"world\"", doc.RootElement.GetProperty("msg").GetString());
        }

        [Fact]
        public void BuildConfiguration_RejectsEmptyChannel()
        {
            _ = Assert.Throws<ArgumentException>(() => AppLoggerFactory.BuildConfiguration(" ", LogEventLevel.Information));
        }

        [Fact]
        public void EnsureLogDirectoryWritable_CreatesMissingDirectory()
        {
            string dir = Path.Combine(Path.GetTempPath(), "sperrydev-acl-" + Guid.NewGuid().ToString("N"));
            try
            {
                AppLoggerFactory.EnsureLogDirectoryWritableByAnyLaunchContext(dir);
                Assert.True(Directory.Exists(dir));
            }
            finally
            {
                Directory.Delete(dir, true);
            }
        }
    }
}
