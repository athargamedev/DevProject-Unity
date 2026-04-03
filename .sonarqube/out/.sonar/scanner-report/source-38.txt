using System;
using System.Runtime.CompilerServices;
using System.Text;
using UnityEngine;

namespace Network_Game.Diagnostics
{
    public readonly struct TraceContext
    {
        public static readonly TraceContext Empty = new TraceContext();

        public readonly string BootId;
        public readonly string FlowId;
        public readonly int RequestId;
        public readonly int ClientRequestId;
        public readonly ulong ClientId;
        public readonly ulong NetworkObjectId;
        public readonly string Phase;
        public readonly string Script;
        public readonly string Callback;

        public TraceContext(
            string bootId = null,
            string flowId = null,
            int requestId = 0,
            int clientRequestId = 0,
            ulong clientId = 0,
            ulong networkObjectId = 0,
            string phase = null,
            string script = null,
            string callback = null
        )
        {
            BootId = bootId ?? string.Empty;
            FlowId = flowId ?? string.Empty;
            RequestId = requestId;
            ClientRequestId = clientRequestId;
            ClientId = clientId;
            NetworkObjectId = networkObjectId;
            Phase = phase ?? string.Empty;
            Script = script ?? string.Empty;
            Callback = callback ?? string.Empty;
        }
    }

    public enum LogLevel
    {
        Debug = 0,
        Info = 1,
        Warning = 2,
        Error = 3,
    }

    /// <summary>
    /// Project logging facade.
    /// Keeps category-based messages while standardizing output through Unity's log system.
    /// </summary>
    public static class NGLog
    {
        private const string DataflowPrefix = "Dataflow";

        public static void Debug(string category, string message, UnityEngine.Object context = null)
        {
            Write(LogLevel.Debug, category, message, context);
        }

        public static void Info(string category, string message, UnityEngine.Object context = null)
        {
            Write(LogLevel.Info, category, message, context);
        }

        public static void Warn(string category, string message, UnityEngine.Object context = null)
        {
            Write(LogLevel.Warning, category, message, context);
        }

        public static void Error(string category, string message, UnityEngine.Object context = null)
        {
            Write(LogLevel.Error, category, message, context);
        }

        public static void Log(string category, string message, UnityEngine.Object context = null)
        {
            Info(category, message, context);
        }

        public static void LogWarning(
            string category,
            string message,
            UnityEngine.Object context = null
        )
        {
            Warn(category, message, context);
        }

        public static void LogError(
            string category,
            string message,
            UnityEngine.Object context = null
        )
        {
            Error(category, message, context);
        }

        public static string Format(string message, params (string key, object value)[] data)
        {
            if (data == null || data.Length == 0)
            {
                return message ?? string.Empty;
            }

            var sb = new StringBuilder(message ?? string.Empty);
            sb.Append(" | ");

            for (int i = 0; i < data.Length; i++)
            {
                (string key, object value) pair = data[i];
                string effectiveKey = string.IsNullOrWhiteSpace(pair.key) ? $"arg{i}" : pair.key;
                sb.Append(effectiveKey);
                sb.Append('=');
                sb.Append(pair.value != null ? pair.value : "null");

                if (i < data.Length - 1)
                {
                    sb.Append(", ");
                }
            }

            return sb.ToString();
        }

        public static IDisposable BeginScope(
            string category,
            string operation,
            int requestId = 0,
            UnityEngine.Object context = null,
            LogLevel startLevel = LogLevel.Debug,
            LogLevel endLevel = LogLevel.Debug,
            params (string key, object value)[] data
        )
        {
            return new LogScope(
                category,
                operation,
                requestId,
                context,
                startLevel,
                endLevel,
                data
            );
        }

        public static void LogDataflowStep(
            string category,
            string flow,
            string step,
            LogLevel level = LogLevel.Info,
            int requestId = 0,
            UnityEngine.Object context = null,
            params (string key, object value)[] data
        )
        {
            string message = $"{DataflowPrefix}:{flow}:{step}";
            if (requestId != 0)
            {
                message = Format(message, ("requestId", requestId));
            }

            if (data != null && data.Length > 0)
            {
                message = Format(message, data);
            }

            Write(level, category, message, context);
        }

        public static void Lifecycle(
            string category,
            string state,
            TraceContext traceContext,
            UnityEngine.Object context = null,
            LogLevel level = LogLevel.Info,
            [CallerMemberName] string caller = null,
            params (string key, object value)[] data
        )
        {
            string message = $"Lifecycle:{state ?? "unknown"}";
            Write(level, category, BuildTraceMessage(message, traceContext, context, caller, data), context);
        }

        public static void Transition(
            string category,
            string from,
            string to,
            TraceContext traceContext,
            UnityEngine.Object context = null,
            LogLevel level = LogLevel.Info,
            [CallerMemberName] string caller = null,
            params (string key, object value)[] data
        )
        {
            string source = string.IsNullOrWhiteSpace(from) ? "unknown" : from;
            string destination = string.IsNullOrWhiteSpace(to) ? "unknown" : to;
            string message = $"Transition:{source}->{destination}";
            Write(level, category, BuildTraceMessage(message, traceContext, context, caller, data), context);
        }

        public static void Publish(
            string category,
            string eventName,
            TraceContext traceContext,
            UnityEngine.Object context = null,
            LogLevel level = LogLevel.Info,
            [CallerMemberName] string caller = null,
            params (string key, object value)[] data
        )
        {
            string message = $"Publish:{eventName ?? "unknown"}";
            Write(level, category, BuildTraceMessage(message, traceContext, context, caller, data), context);
        }

        public static void Subscribe(
            string category,
            string eventName,
            TraceContext traceContext,
            UnityEngine.Object context = null,
            LogLevel level = LogLevel.Debug,
            [CallerMemberName] string caller = null,
            params (string key, object value)[] data
        )
        {
            string message = $"Subscribe:{eventName ?? "unknown"}";
            Write(level, category, BuildTraceMessage(message, traceContext, context, caller, data), context);
        }

        public static void Ready(
            string category,
            string readinessKey,
            bool ready,
            TraceContext traceContext,
            UnityEngine.Object context = null,
            LogLevel level = LogLevel.Info,
            [CallerMemberName] string caller = null,
            params (string key, object value)[] data
        )
        {
            string message = $"Ready:{readinessKey ?? "unknown"}";
            Write(
                level,
                category,
                BuildTraceMessage(message, traceContext, context, caller, data, ("ready", ready)),
                context
            );
        }

        public static void Trigger(
            string category,
            string triggerName,
            TraceContext traceContext,
            UnityEngine.Object context = null,
            LogLevel level = LogLevel.Info,
            [CallerMemberName] string caller = null,
            params (string key, object value)[] data
        )
        {
            string message = $"Trigger:{triggerName ?? "unknown"}";
            Write(level, category, BuildTraceMessage(message, traceContext, context, caller, data), context);
        }

        private static void Write(
            LogLevel level,
            string category,
            string message,
            UnityEngine.Object context
        )
        {
            string prefix = string.IsNullOrWhiteSpace(category) ? string.Empty : $"[{category}] ";
            string fullMessage = $"{prefix}{message ?? string.Empty}";

            switch (level)
            {
                case LogLevel.Error:
                    UnityEngine.Debug.LogError(fullMessage, context);
                    break;
                case LogLevel.Warning:
                    UnityEngine.Debug.LogWarning(fullMessage, context);
                    break;
                default:
                    UnityEngine.Debug.Log(fullMessage, context);
                    break;
            }
        }

        private static string BuildTraceMessage(
            string message,
            TraceContext traceContext,
            UnityEngine.Object context,
            string caller,
            (string key, object value)[] data,
            (string key, object value) extra = default
        )
        {
            string script =
                !string.IsNullOrWhiteSpace(traceContext.Script)
                    ? traceContext.Script
                    : context != null ? context.GetType().Name : string.Empty;
            string callback =
                !string.IsNullOrWhiteSpace(traceContext.Callback) ? traceContext.Callback : caller;
            bool hasContextName = context != null && !string.IsNullOrWhiteSpace(context.name);
            bool hasData = data != null && data.Length > 0;
            bool hasExtra = !string.IsNullOrWhiteSpace(extra.key);

            // Early-out: nothing to append beyond the base message
            if (string.IsNullOrWhiteSpace(traceContext.BootId)
                && string.IsNullOrWhiteSpace(traceContext.FlowId)
                && traceContext.RequestId == 0
                && traceContext.ClientRequestId == 0
                && traceContext.ClientId == 0
                && traceContext.NetworkObjectId == 0
                && string.IsNullOrWhiteSpace(traceContext.Phase)
                && string.IsNullOrWhiteSpace(script)
                && string.IsNullOrWhiteSpace(callback)
                && !hasContextName && !hasData && !hasExtra)
            {
                return message ?? string.Empty;
            }

            var sb = new StringBuilder(message ?? string.Empty);
            sb.Append(" | ");
            bool first = true;

            void AppendField(string key, object value)
            {
                if (!first) sb.Append(", ");
                first = false;
                sb.Append(key).Append('=').Append(value ?? (object)"null");
            }

            if (!string.IsNullOrWhiteSpace(traceContext.BootId)) AppendField("bootId", traceContext.BootId);
            if (!string.IsNullOrWhiteSpace(traceContext.FlowId)) AppendField("flowId", traceContext.FlowId);
            if (traceContext.RequestId != 0) AppendField("requestId", traceContext.RequestId);
            if (traceContext.ClientRequestId != 0) AppendField("clientRequestId", traceContext.ClientRequestId);
            if (traceContext.ClientId != 0) AppendField("clientId", traceContext.ClientId);
            if (traceContext.NetworkObjectId != 0) AppendField("networkObjectId", traceContext.NetworkObjectId);
            if (!string.IsNullOrWhiteSpace(traceContext.Phase)) AppendField("phase", traceContext.Phase);
            if (!string.IsNullOrWhiteSpace(script)) AppendField("script", script);
            if (!string.IsNullOrWhiteSpace(callback)) AppendField("callback", callback);
            if (hasContextName) AppendField("object", context.name);

            if (hasData)
            {
                for (int i = 0; i < data.Length; i++)
                {
                    (string key, object value) pair = data[i];
                    string effectiveKey = string.IsNullOrWhiteSpace(pair.key) ? $"arg{i}" : pair.key;
                    AppendField(effectiveKey, pair.value);
                }
            }

            if (hasExtra) AppendField(extra.key, extra.value);

            return sb.ToString();
        }

        private sealed class LogScope : IDisposable
        {
            private readonly string m_Category;
            private readonly string m_Operation;
            private readonly int m_RequestId;
            private readonly UnityEngine.Object m_Context;
            private readonly LogLevel m_EndLevel;
            private readonly float m_StartTime;
            private bool m_Disposed;

            public LogScope(
                string category,
                string operation,
                int requestId,
                UnityEngine.Object context,
                LogLevel startLevel,
                LogLevel endLevel,
                params (string key, object value)[] data
            )
            {
                m_Category = category;
                m_Operation = string.IsNullOrWhiteSpace(operation) ? "Scope" : operation;
                m_RequestId = requestId;
                m_Context = context;
                m_EndLevel = endLevel;
                m_StartTime = Time.realtimeSinceStartup;

                string beginMessage = $"{m_Operation} begin";
                if (requestId != 0)
                {
                    beginMessage = Format(beginMessage, ("requestId", requestId));
                }
                if (data != null && data.Length > 0)
                {
                    beginMessage = Format(beginMessage, data);
                }

                Write(startLevel, m_Category, beginMessage, m_Context);
            }

            public void Dispose()
            {
                if (m_Disposed)
                {
                    return;
                }
                m_Disposed = true;

                float elapsedMs = (Time.realtimeSinceStartup - m_StartTime) * 1000f;
                string endMessage = Format(
                    $"{m_Operation} end",
                    ("requestId", m_RequestId),
                    ("elapsedMs", Mathf.RoundToInt(elapsedMs))
                );
                Write(m_EndLevel, m_Category, endMessage, m_Context);
            }
        }
    }
}
