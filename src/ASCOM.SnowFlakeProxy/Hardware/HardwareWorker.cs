using System;
using System.Collections.Concurrent;
using System.Threading;
using System.Threading.Tasks;
using ASCOM;

namespace ASCOM.SnowFlakeProxy
{
    internal sealed class HardwareWorker : IDisposable
    {
        private readonly BlockingCollection<HardwareRequest> request_queue;
        private readonly Thread worker_thread;
        private readonly Func<IUnderlyingFilterWheel> create_underlying;
        private readonly SnowflakeProxyController controller;
        private readonly ProxySettings settings;
        private readonly ProxyLogger logger;
        private IUnderlyingFilterWheel underlying;
        private bool disposed;

        internal HardwareWorker(
            Func<IUnderlyingFilterWheel> create_underlying,
            SnowflakeProxyController controller,
            ProxySettings settings,
            ProxyLogger logger,
            ProxySlotIdentity identity)
        {
            this.create_underlying = create_underlying;
            this.controller = controller;
            this.settings = settings;
            this.logger = logger;
            this.request_queue = new BlockingCollection<HardwareRequest>();
            this.worker_thread = new Thread(WorkerLoop);
            this.worker_thread.Name = "SnowFlakeProxy Hardware Worker " + identity.slot.ToString();
            this.worker_thread.IsBackground = true;
            this.worker_thread.SetApartmentState(ApartmentState.STA);
        }

        internal void Start()
        {
            worker_thread.Start();
        }

        internal Task Enqueue(HardwareRequest request)
        {
            request_queue.Add(request);
            return request.completion.Task;
        }

        private void WorkerLoop()
        {
            foreach (HardwareRequest request in request_queue.GetConsumingEnumerable())
            {
                try
                {
                    switch (request.command)
                    {
                        case HardwareCommand.Connect:
                            ExecuteConnect(request);
                            break;
                        case HardwareCommand.Disconnect:
                            ExecuteDisconnect(request);
                            break;
                        case HardwareCommand.StartMove:
                            ExecuteStartMove(request);
                            break;
                        case HardwareCommand.OpenVendorSetup:
                            ExecuteOpenVendorSetup(request);
                            break;
                        case HardwareCommand.Shutdown:
                            ExecuteShutdown();
                            request.completion.TrySetResult(null);
                            return;
                        default:
                            throw new InvalidOperationException("Unknown hardware command: " + request.command);
                    }

                    request.completion.TrySetResult(null);
                }
                catch (Exception ex)
                {
                    logger.Log("HardwareWorker", "Request " + request.command + " failed: " + ex);
                    if (request.setter_accepted != null)
                    {
                        request.setter_accepted.TrySetException(ex);
                    }

                    request.completion.TrySetException(ex);
                }
            }
        }

        private void ExecuteConnect(HardwareRequest request)
        {
            logger.Log("HardwareWorker", "Connect begin");
            IUnderlyingFilterWheel created = null;
            try
            {
                created = create_underlying();
                created.Connected = true;

                string[] vendor_names = created.Names;
                int[] vendor_offsets = created.FocusOffsets;
                short stationary_position = ReadStationaryPosition(created);

                underlying = created;
                controller.CompletePhysicalConnect(vendor_names, vendor_offsets, stationary_position, created.DriverVersion, created.DriverInfo);
                logger.Log("HardwareWorker", "Connect complete position=" + stationary_position);
            }
            catch (Exception ex)
            {
                try
                {
                    if (created != null)
                    {
                        created.Dispose();
                    }
                }
                catch (Exception)
                {
                }

                underlying = null;
                controller.FailPhysicalConnect(ex);
                throw;
            }
        }

        private short ReadStationaryPosition(IUnderlyingFilterWheel wheel)
        {
            DateTime deadline = DateTime.UtcNow.AddMilliseconds(settings.connect_timeout_ms);
            while (true)
            {
                // This is the only code path permitted to read the vendor Position property
                // during connection. The call may block for many seconds.
                short actual = wheel.Position;
                if (actual >= 0)
                {
                    return actual;
                }

                if (DateTime.UtcNow >= deadline)
                {
                    throw new DriverException("Timed out waiting for a stationary filter-wheel position during connection.");
                }

                Thread.Sleep(settings.position_retry_delay_ms);
            }
        }

        private void ExecuteDisconnect(HardwareRequest request)
        {
            logger.Log("HardwareWorker", "Disconnect begin");
            if (underlying != null)
            {
                try
                {
                    underlying.Connected = false;
                }
                catch (Exception ex)
                {
                    logger.Log("HardwareWorker", "Vendor Connected=false failed: " + ex);
                }

                try
                {
                    underlying.Dispose();
                }
                catch (Exception ex)
                {
                    logger.Log("HardwareWorker", "Vendor Dispose failed: " + ex);
                }

                underlying = null;
            }

            controller.CompletePhysicalDisconnect();
            logger.Log("HardwareWorker", "Disconnect complete");
        }

        private void ExecuteStartMove(HardwareRequest request)
        {
            long move_sequence = request.move_sequence;
            short target = request.target_position;
            logger.Log("HardwareWorker", "move=" + move_sequence + " vendor Position SET begin target=" + target);

            if (underlying == null)
            {
                Exception missing = new DriverException("Cannot move because the vendor filter wheel is not connected.");
                controller.NotifyMoveSetterFailed(move_sequence, missing);
                if (request.setter_accepted != null)
                {
                    request.setter_accepted.TrySetException(missing);
                }

                throw missing;
            }

            DateTime setter_start = DateTime.UtcNow;
            try
            {
                underlying.Position = target;
            }
            catch (Exception ex)
            {
                logger.Log("HardwareWorker", "move=" + move_sequence + " vendor Position SET failed: " + ex);
                controller.NotifyMoveSetterFailed(move_sequence, ex);
                if (request.setter_accepted != null)
                {
                    request.setter_accepted.TrySetException(WrapVendorException(ex, "Wanderer Position setter failed while commanding position " + target + ": " + ex.Message));
                }

                return;
            }

            TimeSpan setter_duration = DateTime.UtcNow - setter_start;
            logger.Log("HardwareWorker", "move=" + move_sequence + " vendor Position SET completed duration_ms=" + (int)setter_duration.TotalMilliseconds);
            controller.NotifyMoveSetterSucceeded(move_sequence);
            if (request.setter_accepted != null)
            {
                request.setter_accepted.TrySetResult(null);
            }

            DateTime monitor_start = DateTime.UtcNow;
            while (true)
            {
                short actual;
                DateTime get_start = DateTime.UtcNow;
                try
                {
                    // This is the only code path permitted to read the vendor Position property.
                    // The call may block for many seconds.
                    logger.Log("HardwareWorker", "move=" + move_sequence + " vendor Position GET begin");
                    actual = underlying.Position;
                    TimeSpan get_duration = DateTime.UtcNow - get_start;
                    logger.Log("HardwareWorker", "move=" + move_sequence + " vendor Position GET end result=" + actual + " duration_ms=" + (int)get_duration.TotalMilliseconds);
                    controller.IncrementVendorPositionRead();
                }
                catch (Exception ex)
                {
                    logger.Log("HardwareWorker", "move=" + move_sequence + " vendor Position GET failed: " + ex);
                    controller.NotifyMoveMonitorFailed(move_sequence, WrapVendorException(ex, "Wanderer Position getter failed while monitoring move to position " + target + ": " + ex.Message));
                    return;
                }

                if (actual == target)
                {
                    controller.NotifyMoveConfirmed(move_sequence, target);
                    logger.Log("HardwareWorker", "move=" + move_sequence + " cached_position=" + target);
                    return;
                }

                if ((DateTime.UtcNow - monitor_start).TotalMilliseconds > settings.move_timeout_ms)
                {
                    logger.Log("HardwareWorker", "move=" + move_sequence + " monitor timeout");
                    controller.NotifyMoveTimeout(move_sequence);
                    return;
                }

                if (actual == -1)
                {
                    Thread.Sleep(settings.position_retry_delay_ms);
                    continue;
                }

                if (actual >= 0)
                {
                    logger.Log("HardwareWorker", "move=" + move_sequence + " stale vendor position=" + actual);
                    Thread.Sleep(settings.position_retry_delay_ms);
                    continue;
                }

                Exception unexpected = new DriverException("Wanderer Position getter returned unexpected value " + actual + " while moving to " + target + ".");
                controller.NotifyMoveMonitorFailed(move_sequence, unexpected);
                return;
            }
        }

        private void ExecuteOpenVendorSetup(HardwareRequest request)
        {
            logger.Log("HardwareWorker", "OpenVendorSetup begin");
            IUnderlyingFilterWheel temporary = null;
            try
            {
                if (underlying != null)
                {
                    underlying.SetupDialog();
                }
                else
                {
                    temporary = create_underlying();
                    temporary.SetupDialog();
                }
            }
            finally
            {
                if (temporary != null)
                {
                    temporary.Dispose();
                }
            }

            logger.Log("HardwareWorker", "OpenVendorSetup complete");
        }

        private void ExecuteShutdown()
        {
            logger.Log("HardwareWorker", "Shutdown begin");
            if (underlying != null)
            {
                try
                {
                    underlying.Dispose();
                }
                catch (Exception)
                {
                }

                underlying = null;
            }
        }

        private static Exception WrapVendorException(Exception ex, string message)
        {
            DriverException driver_exception = ex as DriverException;
            if (driver_exception != null)
            {
                return new DriverException(message, driver_exception);
            }

            return new DriverException(message, ex);
        }

        public void Dispose()
        {
            if (disposed)
            {
                return;
            }

            disposed = true;
            try
            {
                HardwareRequest shutdown = new HardwareRequest();
                shutdown.command = HardwareCommand.Shutdown;
                shutdown.completion = new TaskCompletionSource<object>();
                request_queue.Add(shutdown);
            }
            catch (Exception)
            {
            }

            request_queue.CompleteAdding();
            if (!worker_thread.Join(5000))
            {
                logger.Log("HardwareWorker", "Worker thread did not exit within 5 seconds (V1 hang limitation).");
            }

            request_queue.Dispose();
        }
    }
}
