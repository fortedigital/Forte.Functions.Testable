using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using DurableTask.Core.History;
using Microsoft.Azure.WebJobs;
using Microsoft.Azure.WebJobs.Extensions.DurableTask;
using Microsoft.Extensions.DependencyInjection;

namespace Forte.Functions.Testable
{
    public static class TestUtil
    {
        public static void LogHistory(DurableOrchestrationStatus status, TextWriter writer, bool showExceptionDetails = false)
        {
            var history = status.History.ToObject<List<GenericHistoryEvent>>();

            writer.WriteLine("Instance ID.....: "  + status.InstanceId);
            writer.WriteLine("Runtime status..: " + Enum.GetName(typeof(OrchestrationRuntimeStatus), status.RuntimeStatus));

            if (null != status.CustomStatus)
                writer.WriteLine("Custom status...: " + status.CustomStatus.ToString().Take(100));

            writer.WriteLine("History ->");
            foreach (var ev in history)
            {
                writer.WriteLine("  " + ev.Timestamp.ToLocalTime().ToString("HH:mm:ss.fff") + " " 
                                 + $"[{ev.EventId}]  "
                                 + Enum.GetName(typeof(EventType),ev.EventType) 
                                 + (string.IsNullOrEmpty(ev.TaskScheduledId) ? "" : $" ({ev.TaskScheduledId})")
                                 + (string.IsNullOrEmpty(ev.Name) ? "" : $" [{ev.Name}]")
                                 + (string.IsNullOrEmpty(ev.Data) ? "" : $" ({ev.Data})"));

                if (string.IsNullOrEmpty(ev.Reason)) continue;

                writer.WriteLine("    " + ev.Reason);

                if (showExceptionDetails)
                    writer.WriteLine("    " + ev.Details);
            }
        }

        private class GenericHistoryEvent
        {
            public string Data { get; set; }

            public EventType EventType { get; set; }
            public DateTime Timestamp { get; set; }
            public string Name { get; set; }
            public string Details { get; set; }
            public string Reason { get; set; }
            public string EventId { get; set; }
            public string TaskScheduledId { get; set; }
        }

    }
}