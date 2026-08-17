using System;
using System.Collections;
using System.Collections.Generic;
using System.Runtime.InteropServices;
using System.Windows.Forms;
using ASCOM;
using ASCOM.DeviceInterface;
using ASCOM.LocalServer;
using ASCOM.Utilities;

namespace ASCOM.SnowFlakeProxy
{
    [ComVisible(true)]
    [Guid("5A253603-5EBC-4667-9041-F24445724985")]
    [ProgId("ASCOM.SnowFlakeProxy.FilterWheel")]
    [ServedClassName("Wanderer Snowflake Filter Wheel 1 (Proxy)")]
    [ClassInterface(ClassInterfaceType.None)]
    public class FilterWheel : ReferenceCountedObjectBase, IFilterWheelV3, IDisposable
    {
        private readonly Guid client_id;
        private readonly TraceLogger instance_logger;
        private bool connection_lease_held;
        private bool disposed_value;

        public FilterWheel()
        {
            client_id = Guid.NewGuid();
            instance_logger = new TraceLogger("", "SnowFlakeProxy.Driver");
            try
            {
                instance_logger.Enabled = FilterWheelHardware.Controller.Settings.trace_enabled;
            }
            catch (Exception)
            {
                instance_logger.Enabled = true;
            }
        }

        ~FilterWheel()
        {
            Dispose(false);
        }

        public void Dispose()
        {
            Dispose(true);
        }

        protected virtual void Dispose(bool disposing)
        {
            if (disposed_value)
            {
                return;
            }

            if (disposing)
            {
                try
                {
                    if (connection_lease_held)
                    {
                        FilterWheelHardware.Controller.ReleaseLease(client_id);
                        connection_lease_held = false;
                    }
                }
                catch (Exception)
                {
                }

                try
                {
                    instance_logger.Enabled = false;
                    instance_logger.Dispose();
                }
                catch (Exception)
                {
                }
            }

            disposed_value = true;
        }

        public void SetupDialog()
        {
            if (connection_lease_held)
            {
                MessageBox.Show("Already connected, just press OK");
                return;
            }

            FilterWheelHardware.Controller.Settings.trace_enabled = instance_logger.Enabled;
            using (SetupDialogForm form = new SetupDialogForm(FilterWheelHardware.Controller.Settings, FilterWheelHardware.Controller))
            {
                DialogResult result = form.ShowDialog();
                if (result == DialogResult.OK)
                {
                    ProxySettingsStore.Save(FilterWheelHardware.Controller.Settings);
                    instance_logger.Enabled = FilterWheelHardware.Controller.Settings.trace_enabled;
                }
            }
        }

        public ArrayList SupportedActions
        {
            get
            {
                return new ArrayList();
            }
        }

        public string Action(string actionName, string actionParameters)
        {
            throw new ActionNotImplementedException("Action " + actionName + " is not implemented by this driver");
        }

        public void CommandBlind(string command, bool raw)
        {
            throw new MethodNotImplementedException("CommandBlind");
        }

        public bool CommandBool(string command, bool raw)
        {
            throw new MethodNotImplementedException("CommandBool");
        }

        public string CommandString(string command, bool raw)
        {
            throw new MethodNotImplementedException("CommandString");
        }

        public void Connect()
        {
            if (connection_lease_held)
            {
                return;
            }

            FilterWheelHardware.Controller.ConnectAsync(client_id);
            connection_lease_held = true;
        }

        public void Disconnect()
        {
            if (!connection_lease_held)
            {
                return;
            }

            FilterWheelHardware.Controller.DisconnectAsync(client_id);
            connection_lease_held = false;
        }

        public bool Connected
        {
            get
            {
                return FilterWheelHardware.Controller.IsClientConnected(client_id);
            }
            set
            {
                if (value)
                {
                    FilterWheelHardware.Controller.ConnectBlocking(client_id);
                    connection_lease_held = true;
                }
                else
                {
                    FilterWheelHardware.Controller.DisconnectBlocking(client_id);
                    connection_lease_held = false;
                }
            }
        }

        public bool Connecting
        {
            get
            {
                return FilterWheelHardware.Controller.GetConnecting(client_id);
            }
        }

        public string Description
        {
            get
            {
                return FilterWheelHardware.Controller.Description;
            }
        }

        public string DriverInfo
        {
            get
            {
                return FilterWheelHardware.Controller.DriverInfo;
            }
        }

        public string DriverVersion
        {
            get
            {
                return FilterWheelHardware.Controller.DriverVersion;
            }
        }

        public short InterfaceVersion
        {
            get
            {
                return FilterWheelHardware.Controller.InterfaceVersion;
            }
        }

        public string Name
        {
            get
            {
                return FilterWheelHardware.Controller.Name;
            }
        }

        public IStateValueCollection DeviceState
        {
            get
            {
                List<StateValue> return_value = new List<StateValue>();
                return_value.Add(new StateValue(nameof(IFilterWheelV3.Position), FilterWheelHardware.Controller.GetDeviceStatePosition(client_id)));
                return_value.Add(new StateValue(DateTime.Now));
                return new StateValueCollection(return_value);
            }
        }

        public int[] FocusOffsets
        {
            get
            {
                return FilterWheelHardware.Controller.GetFocusOffsets(client_id);
            }
        }

        public string[] Names
        {
            get
            {
                return FilterWheelHardware.Controller.GetNames(client_id);
            }
        }

        public short Position
        {
            get
            {
                // IMPORTANT:
                // Never query the underlying Wanderer Position property here.
                // The vendor Position getter is synchronous and can block for several seconds.
                // ASCOM requires this proxy to return -1 immediately while movement is active.
                return FilterWheelHardware.Controller.GetPosition(client_id);
            }
            set
            {
                FilterWheelHardware.Controller.SetPosition(client_id, value);
            }
        }
    }
}
