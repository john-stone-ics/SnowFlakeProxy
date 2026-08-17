using System;
using System.Collections.Generic;
using System.Threading;
using ASCOM.SnowFlakeProxy;

namespace SnowFlakeProxy.Tests
{
    internal sealed class FakeUnderlyingFilterWheel : IUnderlyingFilterWheel
    {
        private readonly object fake_lock = new object();
        private int concurrent_vendor_call_count;
        private int maximum_concurrent_vendor_call_count;
        private int position_get_call_count;
        private int position_set_call_count;
        private int connect_call_count;
        private int disconnect_call_count;
        private bool connected;
        private short position;
        private short commanded_position;
        private int stale_reads_remaining;
        private Queue<short> queued_get_results = new Queue<short>();

        internal int position_get_delay_ms;
        internal int position_set_delay_ms;
        internal int connect_delay_ms;
        internal int stale_reads_after_set;
        internal bool throw_on_connect;
        internal bool throw_on_get;
        internal bool throw_on_set;
        internal string[] names = new string[]
        {
            "Filter 1 (L)",
            "Filter 2 (R)",
            "Filter 3 (G)",
            "Filter 4 (B)",
            "Filter 5 (H)",
            "Filter 6 (S)",
            "Filter 7 (O)",
            "Filter 8 (D)"
        };
        internal int[] focus_offsets = new int[] { 0, 0, 0, 0, 0, 0, 0, 0 };

        internal int ConcurrentVendorCallCount
        {
            get
            {
                return concurrent_vendor_call_count;
            }
        }

        internal int MaximumConcurrentVendorCallCount
        {
            get
            {
                return maximum_concurrent_vendor_call_count;
            }
        }

        internal int PositionGetCallCount
        {
            get
            {
                return position_get_call_count;
            }
        }

        internal int PositionSetCallCount
        {
            get
            {
                return position_set_call_count;
            }
        }

        internal int ConnectCallCount
        {
            get
            {
                return connect_call_count;
            }
        }

        internal int DisconnectCallCount
        {
            get
            {
                return disconnect_call_count;
            }
        }

        public bool Connected
        {
            get
            {
                using (CallScope scope = EnterCall())
                {
                    return connected;
                }
            }
            set
            {
                using (CallScope scope = EnterCall())
                {
                    if (value)
                    {
                        Interlocked.Increment(ref connect_call_count);
                        if (connect_delay_ms > 0)
                        {
                            Thread.Sleep(connect_delay_ms);
                        }

                        if (throw_on_connect)
                        {
                            throw new InvalidOperationException("Fake connect failed.");
                        }

                        connected = true;
                    }
                    else
                    {
                        Interlocked.Increment(ref disconnect_call_count);
                        connected = false;
                    }
                }
            }
        }

        public string Name
        {
            get
            {
                return "Wanderer Snowflake Filter Wheel 1";
            }
        }

        public string Description
        {
            get
            {
                return "Wanderer Snowflake Filter Wheel 1";
            }
        }

        public string DriverVersion
        {
            get
            {
                return "1.0";
            }
        }

        public string DriverInfo
        {
            get
            {
                return "Firmware:20260124 ID:0";
            }
        }

        public short InterfaceVersion
        {
            get
            {
                return 3;
            }
        }

        public string[] Names
        {
            get
            {
                using (CallScope scope = EnterCall())
                {
                    return (string[])names.Clone();
                }
            }
        }

        public int[] FocusOffsets
        {
            get
            {
                using (CallScope scope = EnterCall())
                {
                    return (int[])focus_offsets.Clone();
                }
            }
        }

        public short Position
        {
            get
            {
                using (CallScope scope = EnterCall())
                {
                    Interlocked.Increment(ref position_get_call_count);
                    if (position_get_delay_ms > 0)
                    {
                        Thread.Sleep(position_get_delay_ms);
                    }

                    if (throw_on_get)
                    {
                        throw new InvalidOperationException("Fake Position getter failed.");
                    }

                    lock (fake_lock)
                    {
                        if (queued_get_results.Count > 0)
                        {
                            return queued_get_results.Dequeue();
                        }

                        if (stale_reads_remaining > 0)
                        {
                            stale_reads_remaining--;
                            return position;
                        }

                        position = commanded_position;
                        return position;
                    }
                }
            }
            set
            {
                using (CallScope scope = EnterCall())
                {
                    Interlocked.Increment(ref position_set_call_count);
                    if (position_set_delay_ms > 0)
                    {
                        Thread.Sleep(position_set_delay_ms);
                    }

                    if (throw_on_set)
                    {
                        throw new InvalidOperationException("Fake Position setter failed.");
                    }

                    lock (fake_lock)
                    {
                        commanded_position = value;
                        stale_reads_remaining = stale_reads_after_set;
                    }
                }
            }
        }

        public void SetupDialog()
        {
            using (CallScope scope = EnterCall())
            {
            }
        }

        public void Dispose()
        {
        }

        internal void EnqueueGetResult(short value)
        {
            lock (fake_lock)
            {
                queued_get_results.Enqueue(value);
            }
        }

        internal void SetStationaryPosition(short value)
        {
            lock (fake_lock)
            {
                position = value;
                commanded_position = value;
            }
        }

        private CallScope EnterCall()
        {
            int now = Interlocked.Increment(ref concurrent_vendor_call_count);
            int snapshot;
            int original;
            do
            {
                snapshot = maximum_concurrent_vendor_call_count;
                if (now <= snapshot)
                {
                    break;
                }

                original = Interlocked.CompareExchange(ref maximum_concurrent_vendor_call_count, now, snapshot);
            }
            while (original != snapshot);

            return new CallScope(this);
        }

        private void ExitCall()
        {
            Interlocked.Decrement(ref concurrent_vendor_call_count);
        }

        private sealed class CallScope : IDisposable
        {
            private readonly FakeUnderlyingFilterWheel owner;

            internal CallScope(FakeUnderlyingFilterWheel owner)
            {
                this.owner = owner;
            }

            public void Dispose()
            {
                owner.ExitCall();
            }
        }
    }
}
