using System;
using System.Collections.Generic;
using System.Threading;
using System.Threading.Tasks;
using ASCOM;

namespace ASCOM.SnowFlakeProxy
{
    internal sealed class SnowflakeProxyController : IDisposable
    {
        private readonly object state_lock;
        private readonly HardwareWorker hardware_worker;
        private readonly ProxySettings settings;
        private readonly ProxyLogger logger;
        private readonly HashSet<Guid> connection_leases;
        private readonly Dictionary<Guid, ClientConnectTracker> client_trackers;

        private ConnectionState connection_state;
        private MoveState move_state;
        private short cached_position;
        private short target_position;
        private string[] cached_names;
        private int[] cached_focus_offsets;
        private Exception last_connection_error;
        private Exception last_move_error;
        private DateTime last_position_confirmation_utc;
        private long move_sequence;
        private long vendor_position_read_count;
        private long vendor_position_set_count;
        private long public_position_get_count;
        private long public_position_get_while_moving_count;
        private string vendor_driver_version;
        private TaskCompletionSource<object> in_flight_connect;
        private bool worker_owns_move;
        private Timer move_watchdog;
        private long watchdog_move_sequence;
        private bool disposed;

        private sealed class ClientConnectTracker
        {
            internal bool connecting;
            internal Exception connection_exception;
        }

        internal SnowflakeProxyController(
            Func<IUnderlyingFilterWheel> create_underlying,
            ProxySettings settings,
            ProxyLogger logger)
        {
            this.state_lock = new object();
            this.settings = settings;
            this.logger = logger;
            this.connection_leases = new HashSet<Guid>();
            this.client_trackers = new Dictionary<Guid, ClientConnectTracker>();
            this.connection_state = ConnectionState.Disconnected;
            this.move_state = MoveState.Idle;
            this.cached_names = new string[0];
            this.cached_focus_offsets = new int[0];
            this.hardware_worker = new HardwareWorker(create_underlying, this, settings, logger);
            this.hardware_worker.Start();
        }

        internal ProxySettings Settings
        {
            get
            {
                return settings;
            }
        }

        internal long VendorPositionReadCount
        {
            get
            {
                lock (state_lock)
                {
                    return vendor_position_read_count;
                }
            }
        }

        internal long VendorPositionSetCount
        {
            get
            {
                lock (state_lock)
                {
                    return vendor_position_set_count;
                }
            }
        }

        internal bool IsAnyClientConnected()
        {
            lock (state_lock)
            {
                return connection_leases.Count > 0 && connection_state == ConnectionState.Connected;
            }
        }

        internal bool IsClientConnected(Guid client_id)
        {
            lock (state_lock)
            {
                return connection_leases.Contains(client_id) && connection_state == ConnectionState.Connected;
            }
        }

        internal bool GetConnecting(Guid client_id)
        {
            lock (state_lock)
            {
                ClientConnectTracker tracker = GetTracker_NoLock(client_id);
                if (tracker.connection_exception != null)
                {
                    throw WrapAsDriverException(tracker.connection_exception, "Connect/Disconnect failed: " + tracker.connection_exception.Message);
                }

                return tracker.connecting;
            }
        }

        internal string Name
        {
            get
            {
                return ProxyIdentity.ProxyName;
            }
        }

        internal string Description
        {
            get
            {
                return ProxyIdentity.Description;
            }
        }

        internal string DriverVersion
        {
            get
            {
                return ProxyIdentity.DriverVersion;
            }
        }

        internal short InterfaceVersion
        {
            get
            {
                return ProxyIdentity.InterfaceVersion;
            }
        }

        internal string DriverInfo
        {
            get
            {
                string info = "SnowFlakeProxy " + ProxyIdentity.DriverVersion + "; proxies " + ProxyIdentity.VendorProgId;
                lock (state_lock)
                {
                    if (!string.IsNullOrEmpty(vendor_driver_version))
                    {
                        info = info + "; vendor DriverVersion=" + vendor_driver_version;
                    }
                }

                return info;
            }
        }

        internal string[] GetNames(Guid client_id)
        {
            EnsureClientConnected(client_id, "Names");
            lock (state_lock)
            {
                return (string[])cached_names.Clone();
            }
        }

        internal int[] GetFocusOffsets(Guid client_id)
        {
            EnsureClientConnected(client_id, "FocusOffsets");
            lock (state_lock)
            {
                return (int[])cached_focus_offsets.Clone();
            }
        }

        internal short GetPosition(Guid client_id)
        {
            // IMPORTANT:
            // Never query the underlying Wanderer Position property here.
            // The vendor Position getter is synchronous and can block for several seconds.
            // ASCOM requires this proxy to return -1 immediately while movement is active.
            EnsureClientConnected(client_id, "Position");
            lock (state_lock)
            {
                public_position_get_count++;
                if (move_state == MoveState.Starting || move_state == MoveState.Moving)
                {
                    public_position_get_while_moving_count++;
                    return -1;
                }

                if (move_state == MoveState.Faulted)
                {
                    throw CreateMoveDriverException();
                }

                return cached_position;
            }
        }

        internal void SetPosition(Guid client_id, short value)
        {
            EnsureClientConnected(client_id, "Position");

            TaskCompletionSource<object> setter_accepted = null;
            HardwareRequest request = null;
            int accept_timeout_ms;
            long issued_sequence = 0;

            lock (state_lock)
            {
                if (cached_names == null || cached_names.Length == 0)
                {
                    throw new InvalidValueException("Position", value.ToString(), "0 to (unknown slot count)");
                }

                if (value < 0 || value >= cached_names.Length)
                {
                    throw new InvalidValueException("Position", value.ToString(), "0 to " + (cached_names.Length - 1).ToString());
                }

                if (move_state == MoveState.Idle && value == cached_position)
                {
                    logger.Log("Position Set", "client=" + client_id + " requested=" + value + " already at cached position");
                    return;
                }

                if ((move_state == MoveState.Starting || move_state == MoveState.Moving) && value == target_position)
                {
                    logger.Log("Position Set", "client=" + client_id + " requested=" + value + " identical in-flight move");
                    return;
                }

                if (move_state == MoveState.Starting || move_state == MoveState.Moving)
                {
                    throw new DriverException("Filter wheel is currently moving to position " + target_position + "; a new move to position " + value + " cannot be started until the current movement completes.");
                }

                if (move_state == MoveState.Faulted && worker_owns_move)
                {
                    throw new DriverException("A previous vendor operation has not yet returned; a new move cannot be started.");
                }

                target_position = value;
                move_state = MoveState.Starting;
                last_move_error = null;
                move_sequence++;
                issued_sequence = move_sequence;
                worker_owns_move = true;
                setter_accepted = new TaskCompletionSource<object>();
                request = new HardwareRequest();
                request.command = HardwareCommand.StartMove;
                request.target_position = value;
                request.move_sequence = issued_sequence;
                request.completion = new TaskCompletionSource<object>();
                request.setter_accepted = setter_accepted;
                accept_timeout_ms = settings.setter_accept_timeout_ms;
                logger.Log("Position Set", "client=" + client_id + " move=" + issued_sequence + " requested=" + value + " state Idle/Faulted -> Starting");
            }

            // NEVER hold state_lock while synchronously waiting for a worker completion task.
            hardware_worker.Enqueue(request);
            WaitForSetterAcceptance(setter_accepted, accept_timeout_ms, issued_sequence);
        }

        internal void ConnectBlocking(Guid client_id)
        {
            Task connect_task = BeginConnectCore(client_id, true);
            if (connect_task == null)
            {
                return;
            }

            try
            {
                // NEVER hold state_lock while synchronously waiting for a worker completion task.
                WaitForTask(connect_task, settings.connect_timeout_ms, "Physical connect");
            }
            finally
            {
                FinishConnectWait(client_id, connect_task);
            }
        }

        internal void DisconnectBlocking(Guid client_id)
        {
            Task disconnect_task = BeginDisconnectCore(client_id, true);
            if (disconnect_task == null)
            {
                return;
            }

            try
            {
                // NEVER hold state_lock while synchronously waiting for a worker completion task.
                WaitForTask(disconnect_task, settings.connect_timeout_ms, "Physical disconnect");
            }
            finally
            {
                lock (state_lock)
                {
                    ClientConnectTracker tracker = GetTracker_NoLock(client_id);
                    tracker.connecting = false;
                    if (disconnect_task.IsFaulted)
                    {
                        tracker.connection_exception = Unwrap(disconnect_task.Exception);
                    }
                }
            }
        }

        internal void ConnectAsync(Guid client_id)
        {
            BeginConnectCore(client_id, false);
        }

        internal void DisconnectAsync(Guid client_id)
        {
            BeginDisconnectCore(client_id, false);
        }

        internal void ReleaseLease(Guid client_id)
        {
            DisconnectBlocking(client_id);
        }

        internal short GetDeviceStatePosition(Guid client_id)
        {
            return GetPosition(client_id);
        }

        internal void OpenVendorSetup()
        {
            lock (state_lock)
            {
                if (connection_leases.Count > 0 || connection_state == ConnectionState.Connected || connection_state == ConnectionState.Connecting)
                {
                    throw new DriverException("Disconnect all clients before opening the Wanderer setup dialog.");
                }
            }

            HardwareRequest request = new HardwareRequest();
            request.command = HardwareCommand.OpenVendorSetup;
            request.completion = new TaskCompletionSource<object>();
            // NEVER hold state_lock while synchronously waiting for a worker completion task.
            WaitForTask(hardware_worker.Enqueue(request), settings.connect_timeout_ms, "Open Wanderer Setup");
        }

        internal void CompletePhysicalConnect(string[] vendor_names, int[] vendor_offsets, short stationary_position, string vendor_version, string vendor_info)
        {
            string[] normalized = FilterNameNormalizer.NormalizeAll(vendor_names, settings.normalize_filter_names);
            int[] offsets_copy;
            if (vendor_offsets == null || vendor_offsets.Length != normalized.Length)
            {
                logger.Log("Connect", "Vendor FocusOffsets length inconsistent with Names; using a same-length zero array. names=" + normalized.Length + " offsets=" + (vendor_offsets == null ? -1 : vendor_offsets.Length));
                offsets_copy = new int[normalized.Length];
            }
            else
            {
                offsets_copy = (int[])vendor_offsets.Clone();
            }

            lock (state_lock)
            {
                cached_names = normalized;
                cached_focus_offsets = offsets_copy;
                cached_position = stationary_position;
                target_position = stationary_position;
                move_state = MoveState.Idle;
                connection_state = ConnectionState.Connected;
                last_connection_error = null;
                last_position_confirmation_utc = DateTime.UtcNow;
                vendor_driver_version = vendor_version;
                logger.Log("Connect", "Physical connect complete position=" + stationary_position + " names=" + string.Join(",", cached_names));
            }
        }

        internal void FailPhysicalConnect(Exception error)
        {
            lock (state_lock)
            {
                connection_state = ConnectionState.Disconnected;
                last_connection_error = error;
                connection_leases.Clear();
                logger.Log("Connect", "Physical connect failed: " + error);
            }
        }

        internal void CompletePhysicalDisconnect()
        {
            lock (state_lock)
            {
                connection_state = ConnectionState.Disconnected;
                move_state = MoveState.Idle;
                worker_owns_move = false;
                logger.Log("Disconnect", "Physical disconnect complete");
            }
        }

        internal void NotifyMoveSetterSucceeded(long sequence)
        {
            lock (state_lock)
            {
                if (sequence != move_sequence)
                {
                    return;
                }

                vendor_position_set_count++;
                move_state = MoveState.Moving;
                StartWatchdog_NoLock(sequence);
                logger.Log("Position", "move=" + sequence + " state Starting -> Moving");
            }
        }

        internal void NotifyMoveSetterFailed(long sequence, Exception error)
        {
            lock (state_lock)
            {
                if (sequence != move_sequence)
                {
                    return;
                }

                move_state = MoveState.Faulted;
                last_move_error = error;
                worker_owns_move = false;
                StopWatchdog_NoLock();
                logger.Log("Position", "move=" + sequence + " setter failed, state -> Faulted: " + error.Message);
            }
        }

        internal void NotifyMoveConfirmed(long sequence, short position)
        {
            lock (state_lock)
            {
                if (sequence != move_sequence)
                {
                    return;
                }

                cached_position = position;
                move_state = MoveState.Idle;
                last_move_error = null;
                last_position_confirmation_utc = DateTime.UtcNow;
                worker_owns_move = false;
                StopWatchdog_NoLock();
                logger.Log("Position", "move=" + sequence + " public polls=" + public_position_get_count + " moving_returns=" + public_position_get_while_moving_count);
                logger.Log("Position", "move=" + sequence + " state Moving/Faulted -> Idle cached_position=" + position);
            }
        }

        internal void NotifyMoveMonitorFailed(long sequence, Exception error)
        {
            lock (state_lock)
            {
                if (sequence != move_sequence)
                {
                    return;
                }

                move_state = MoveState.Faulted;
                last_move_error = error;
                worker_owns_move = false;
                StopWatchdog_NoLock();
                logger.Log("Position", "move=" + sequence + " monitor failed, state -> Faulted: " + error.Message);
            }
        }

        internal void NotifyMoveTimeout(long sequence)
        {
            lock (state_lock)
            {
                if (sequence != move_sequence)
                {
                    return;
                }

                if (move_state == MoveState.Idle)
                {
                    return;
                }

                move_state = MoveState.Faulted;
                last_move_error = new DriverException("Filter wheel move to position " + target_position + " timed out after " + settings.move_timeout_ms + " ms.");
                worker_owns_move = false;
                StopWatchdog_NoLock();
                logger.Log("Position", "move=" + sequence + " timeout, state -> Faulted");
            }
        }

        internal void IncrementVendorPositionRead()
        {
            lock (state_lock)
            {
                vendor_position_read_count++;
            }
        }

        public void Dispose()
        {
            if (disposed)
            {
                return;
            }

            disposed = true;
            StopWatchdog_NoLockSafe();
            hardware_worker.Dispose();
            logger.Dispose();
        }

        private Task BeginConnectCore(Guid client_id, bool blocking)
        {
            TaskCompletionSource<object> shared = null;
            bool already_done = false;
            bool wait_existing = false;

            lock (state_lock)
            {
                ClientConnectTracker tracker = GetTracker_NoLock(client_id);
                tracker.connection_exception = null;

                if (connection_leases.Contains(client_id) && connection_state == ConnectionState.Connected)
                {
                    already_done = true;
                }
                else if (connection_state == ConnectionState.Connected)
                {
                    connection_leases.Add(client_id);
                    already_done = true;
                    logger.Log("Connect", "client=" + client_id + " acquired lease; hardware already connected; leases=" + connection_leases.Count);
                }
                else if (connection_state == ConnectionState.Connecting && in_flight_connect != null)
                {
                    shared = in_flight_connect;
                    wait_existing = true;
                    if (!blocking)
                    {
                        tracker.connecting = true;
                    }
                }
                else
                {
                    connection_state = ConnectionState.Connecting;
                    shared = new TaskCompletionSource<object>();
                    in_flight_connect = shared;
                    if (!blocking)
                    {
                        tracker.connecting = true;
                    }
                }
            }

            if (already_done)
            {
                return null;
            }

            if (!wait_existing)
            {
                HardwareRequest request = new HardwareRequest();
                request.command = HardwareCommand.Connect;
                request.completion = shared;
                hardware_worker.Enqueue(request);
            }

            if (blocking)
            {
                return shared.Task;
            }

            shared.Task.ContinueWith(delegate (Task<object> completed)
            {
                FinishConnectWait(client_id, completed);
            }, TaskScheduler.Default);
            return shared.Task;
        }

        private void FinishConnectWait(Guid client_id, Task completed)
        {
            lock (state_lock)
            {
                if (!completed.IsCompleted)
                {
                    return;
                }

                ClientConnectTracker tracker = GetTracker_NoLock(client_id);
                tracker.connecting = false;
                if (completed.IsFaulted)
                {
                    Exception error = Unwrap(completed.Exception);
                    tracker.connection_exception = error;
                    last_connection_error = error;
                }
                else if (connection_state == ConnectionState.Connected)
                {
                    connection_leases.Add(client_id);
                    tracker.connection_exception = null;
                    logger.Log("Connect", "client=" + client_id + " lease acquired; leases=" + connection_leases.Count);
                }

                in_flight_connect = null;
            }
        }

        private Task BeginDisconnectCore(Guid client_id, bool blocking)
        {
            TaskCompletionSource<object> disconnect_tcs = null;
            bool need_physical = false;

            lock (state_lock)
            {
                ClientConnectTracker tracker = GetTracker_NoLock(client_id);
                tracker.connection_exception = null;
                if (!connection_leases.Contains(client_id))
                {
                    return null;
                }

                connection_leases.Remove(client_id);
                logger.Log("Disconnect", "client=" + client_id + " released lease; leases=" + connection_leases.Count);
                if (connection_leases.Count > 0)
                {
                    return null;
                }

                connection_state = ConnectionState.Disconnecting;
                need_physical = true;
                disconnect_tcs = new TaskCompletionSource<object>();
                if (!blocking)
                {
                    tracker.connecting = true;
                }
            }

            if (!need_physical)
            {
                return null;
            }

            HardwareRequest request = new HardwareRequest();
            request.command = HardwareCommand.Disconnect;
            request.completion = disconnect_tcs;
            hardware_worker.Enqueue(request);

            if (!blocking)
            {
                disconnect_tcs.Task.ContinueWith(delegate (Task<object> completed)
                {
                    lock (state_lock)
                    {
                        ClientConnectTracker tracker = GetTracker_NoLock(client_id);
                        tracker.connecting = false;
                        if (completed.IsFaulted)
                        {
                            tracker.connection_exception = Unwrap(completed.Exception);
                        }
                    }
                }, TaskScheduler.Default);
            }

            return disconnect_tcs.Task;
        }

        private void EnsureClientConnected(Guid client_id, string operation)
        {
            lock (state_lock)
            {
                if (!connection_leases.Contains(client_id) || connection_state != ConnectionState.Connected)
                {
                    throw new NotConnectedException(ProxyIdentity.ProxyName + " is not connected: " + operation);
                }
            }
        }

        private ClientConnectTracker GetTracker_NoLock(Guid client_id)
        {
            ClientConnectTracker tracker;
            if (!client_trackers.TryGetValue(client_id, out tracker))
            {
                tracker = new ClientConnectTracker();
                client_trackers[client_id] = tracker;
            }

            return tracker;
        }

        private void WaitForSetterAcceptance(TaskCompletionSource<object> setter_accepted, int timeout_ms, long sequence)
        {
            // NEVER hold state_lock while synchronously waiting for a worker completion task.
            try
            {
                if (!setter_accepted.Task.Wait(timeout_ms))
                {
                    throw new DriverException("The vendor driver did not acknowledge the move command in time (move " + sequence + ", timeout " + timeout_ms + " ms).");
                }
            }
            catch (AggregateException aggregate)
            {
                throw WrapAsDriverException(Unwrap(aggregate), "Vendor Position setter failed.");
            }
        }

        private static void WaitForTask(Task task, int timeout_ms, string operation)
        {
            // NEVER hold state_lock while synchronously waiting for a worker completion task.
            try
            {
                if (!task.Wait(timeout_ms))
                {
                    throw new DriverException(operation + " timed out after " + timeout_ms + " ms.");
                }
            }
            catch (AggregateException aggregate)
            {
                throw WrapAsDriverException(Unwrap(aggregate), operation + " failed.");
            }
        }

        private DriverException CreateMoveDriverException()
        {
            if (last_move_error != null)
            {
                return WrapAsDriverException(last_move_error, last_move_error.Message);
            }

            return new DriverException("Filter wheel movement is in a faulted state.");
        }

        private static DriverException WrapAsDriverException(Exception error, string message)
        {
            DriverException driver_exception = error as DriverException;
            if (driver_exception != null)
            {
                return driver_exception;
            }

            return new DriverException(message, error);
        }

        private static Exception Unwrap(Exception error)
        {
            AggregateException aggregate = error as AggregateException;
            if (aggregate != null && aggregate.InnerException != null)
            {
                return aggregate.InnerException;
            }

            return error;
        }

        private void StartWatchdog_NoLock(long sequence)
        {
            StopWatchdog_NoLock();
            watchdog_move_sequence = sequence;
            move_watchdog = new Timer(WatchdogFired, sequence, settings.move_timeout_ms, Timeout.Infinite);
        }

        private void StopWatchdog_NoLock()
        {
            if (move_watchdog != null)
            {
                move_watchdog.Dispose();
                move_watchdog = null;
            }
        }

        private void StopWatchdog_NoLockSafe()
        {
            lock (state_lock)
            {
                StopWatchdog_NoLock();
            }
        }

        private void WatchdogFired(object state)
        {
            long sequence = (long)state;
            lock (state_lock)
            {
                if (sequence != move_sequence || sequence != watchdog_move_sequence)
                {
                    return;
                }

                if (move_state == MoveState.Starting || move_state == MoveState.Moving)
                {
                    move_state = MoveState.Faulted;
                    last_move_error = new DriverException("Filter wheel move to position " + target_position + " timed out after " + settings.move_timeout_ms + " ms.");
                    logger.Log("Position", "move=" + sequence + " watchdog timeout, public state -> Faulted");
                }
            }
        }
    }
}
