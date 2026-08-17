using System;
using ASCOM.Utilities;

namespace ASCOM.SnowFlakeProxy
{
    internal sealed class ProxyLogger
    {
        private readonly TraceLogger trace_logger;
        private readonly object log_lock;
        private readonly Action<string, string> sink;

        internal ProxyLogger(TraceLogger trace_logger)
        {
            this.trace_logger = trace_logger;
            this.log_lock = new object();
            this.sink = null;
        }

        internal ProxyLogger(Action<string, string> sink)
        {
            this.trace_logger = null;
            this.log_lock = new object();
            this.sink = sink;
        }

        internal static ProxyLogger CreateNull()
        {
            return new ProxyLogger(delegate (string identifier, string message) { });
        }

        internal void Log(string identifier, string message)
        {
            lock (log_lock)
            {
                if (trace_logger != null)
                {
                    trace_logger.LogMessageCrLf(identifier, message);
                }

                if (sink != null)
                {
                    sink(identifier, message);
                }
            }
        }

        internal void Dispose()
        {
            if (trace_logger != null)
            {
                try
                {
                    trace_logger.Enabled = false;
                    trace_logger.Dispose();
                }
                catch (Exception)
                {
                }
            }
        }
    }
}
