using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.IO;
using System.Linq;
using System.Text;
using FluentAssertions;
using Microsoft.VisualStudio.TestTools.UnitTesting;
using Newtonsoft.Json;
using Serilog.Events;
using Serilog.Parsing;

namespace Serilog.LogglyBulkSink.Tests
{
    [TestClass]
    public class SerilogLogglyBulkSinkTests
    {
        [TestMethod]
        public void TestAddIfContains()
        {
            var dictionary = new Dictionary<string, string>()
            {
                {"hello", "world"}
            };
            LogglySink.AddIfNotContains(dictionary, "hello", "another world");
            dictionary.ContainsKey("hello").Should().BeTrue();
            dictionary["hello"].Should().Be("world");


            LogglySink.AddIfNotContains(dictionary, "newkey", "orange");
            dictionary.ContainsKey("newkey").Should().BeTrue();
            dictionary["newkey"].Should().Be("orange");
        }

        [TestMethod]
        public void PackageContentsTest()
        {
            var jsons = new[]
            {
                "{'fruit': 'orange'}",
                "{'fruit': 'apple'}",
                "{'fruit': 'banana'}",
            }.ToList();

            //changing to remove diagnostics parameter to show that the default version is false, and that this test ensures backwards API compatibility. Don't add it back w/o cutting major version!
            var noDiagContent = LogglySink.PackageContent(jsons, Encoding.UTF8.GetByteCount(string.Join("\n", jsons)), 0); 
            var stringContent = LogglySink.PackageContent(jsons, Encoding.UTF8.GetByteCount(string.Join("\n", jsons)), 0, true);
            stringContent.Should().NotBeNull();
            noDiagContent.Should().NotBeNull();
            var result = stringContent.ReadAsStringAsync().GetAwaiter().GetResult();
            var resultNoDiag = noDiagContent.ReadAsStringAsync().GetAwaiter().GetResult();
            result.Split('\n').Length.Should().Be(4);
            resultNoDiag.Split('\n').Length.Should().Be(3);
        }

        [TestMethod]
        public void TestRender()
        {
            var logEvent = new LogEvent(DateTimeOffset.UtcNow,
                LogEventLevel.Debug, null, new MessageTemplate(Enumerable.Empty<MessageTemplateToken>()), new []
                {
                    new LogEventProperty("test1", new ScalarValue("answer1")),
                    new LogEventProperty("0", new ScalarValue("this should be missing")),
                    new LogEventProperty("key", new ScalarValue("value"))
                });
            var result = LogglySink.EventToJson(logEvent);
            var json = JsonConvert.DeserializeObject<dynamic>(result);
            (json["test1"].Value as string).Should().Be("answer1");
            bool hasZero = (json["0"] == null);
            hasZero.Should().Be(true);
            (json["key"].Value as string).Should().Be("value");
        }

        [TestMethod]
        public void IncludeDiagnostics_WhenEnabled_IncludesDiagnosticsEvent()
        {
            var logEvent = new LogEvent(DateTimeOffset.UtcNow,
                LogEventLevel.Debug, null, new MessageTemplate(Enumerable.Empty<MessageTemplateToken>()), new[]
                {
                    new LogEventProperty("Field1", new ScalarValue("Value1")),
                });
            var result = new List<string>{LogglySink.EventToJson(logEvent)};

            var package = LogglySink.PackageContent(result, 1024, 5, true);

            var packageStringTask = package.ReadAsStringAsync();
            packageStringTask.Wait();
            var packageString = packageStringTask.Result;

            (result.Count == 2).Should().BeTrue();
            result[1].Contains("LogglyDiagnostics").Should().BeTrue();
            packageString.Contains("LogglyDiagnostics").Should().BeTrue();
        }

        [TestMethod]
        public void IncludeDiagnostics_WhenDisbled_DoesNotIncludeDiagnosticsEvent()
        {
            var logEvent = new LogEvent(DateTimeOffset.UtcNow,
                LogEventLevel.Debug, null, new MessageTemplate(Enumerable.Empty<MessageTemplateToken>()), new[]
                {
                    new LogEventProperty("Field1", new ScalarValue("Value1")),
                });
            var result = new List<string> { LogglySink.EventToJson(logEvent) };

            var package = LogglySink.PackageContent(result, 1024, 5);

            var packageStringTask = package.ReadAsStringAsync();
            packageStringTask.Wait();
            var packageString = packageStringTask.Result;

            (result.Count == 1).Should().BeTrue();
            packageString.Contains("LogglyDiagnostics").Should().BeFalse();
        }

        [TestMethod]
        [TestCategory( "Integration")]
        public void WhenInvalidApiKeyProvided_OnSinkSend_TraceAndSerilogSelfLogPopulated()
        {
            var serilogSelfLogWriter = new StringWriter();
            Debugging.SelfLog.Enable(serilogSelfLogWriter);

            var traceWriter = new StringWriter();
            Trace.Listeners.Add(new TextWriterTraceListener(traceWriter));

            var logger = new LoggerConfiguration()
                .WriteTo.LogglyBulk("!!FAKE KEY!!", new[] {"!!FAKE TAG!!"}).CreateLogger();

            logger.Fatal("!!FAKE MESSAGE!!");

            logger.Dispose();

            var traceResult = traceWriter.ToString();
            string.IsNullOrWhiteSpace(traceResult).Should().BeFalse();

            var selfLogResult = serilogSelfLogWriter.ToString();
            string.IsNullOrWhiteSpace(selfLogResult).Should().BeFalse();

            traceResult.Contains(
                "Exception posting to loggly System.Net.Http.HttpRequestException: Response status code does not indicate success")
                .Should()
                .BeTrue();

            selfLogResult.Contains(
                "Exception posting to loggly System.Net.Http.HttpRequestException: Response status code does not indicate success")
                .Should()
                .BeTrue();

            Console.WriteLine(traceResult);
            Console.WriteLine(selfLogResult);
        }

        [TestMethod]
        [TestCategory("Integration")]
        public void WhenInvalidApiKeyProvided_AndSelfLogOrTraceIsNotConfigured_EverythingIsOkay()
        {
            Trace.Listeners.Clear();
            Debugging.SelfLog.Disable();
            var logger = new LoggerConfiguration()
                .WriteTo.LogglyBulk("!!FAKE KEY!!", new[] { "!!FAKE TAG!!" }).CreateLogger();

            logger.Fatal("!!FAKE MESSAGE!!");

            logger.Dispose();
        }

    }
}
