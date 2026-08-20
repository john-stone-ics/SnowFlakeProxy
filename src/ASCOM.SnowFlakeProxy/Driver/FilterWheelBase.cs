using System;
using System.Collections;
using System.Collections.Generic;
using System.Globalization;
using System.Windows.Forms;
using ASCOM;
using ASCOM.DeviceInterface;
using ASCOM.LocalServer;
using ASCOM.Utilities;

namespace ASCOM.SnowFlakeProxy
{
    public abstract class FilterWheelBase : ReferenceCountedObjectBase, IFilterWheelV3, IDisposable
    {
        private readonly int slot;
        private readonly ProxySlotIdentity identity;
        private readonly Guid client_id;
        private readonly TraceLogger instance_logger;
        private bool connection_lease_held;
        private bool disposed_value;

        protected FilterWheelBase(int slot)
        {
            this.slot = slot;
            identity = ProxyIdentity.ForSlot(slot);
            client_id = Guid.NewGuid();
            instance_logger = new TraceLogger("", "SnowFlakeProxy.Driver." + slot.ToString(CultureInfo.InvariantCulture));
            try
            {
                instance_logger.Enabled = Controller.Settings.trace_enabled;
            }
            catch (Exception)
            {
                instance_logger.Enabled = true;
            }
        }

        private SnowflakeProxyController Controller
        {
            get
            {
                return FilterWheelHardware.ControllerFor(slot);
            }
        }

        ~FilterWheelBase()
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
                        Controller.ReleaseLease(client_id);
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

            Controller.Settings.trace_enabled = instance_logger.Enabled;
            using (SetupDialogForm form = new SetupDialogForm(Controller.Settings, Controller, identity))
            {
                DialogResult result = form.ShowDialog();
                if (result == DialogResult.OK)
                {
                    ProxySettingsStore.Save(Controller.Settings, identity.proxy_prog_id);
                    instance_logger.Enabled = Controller.Settings.trace_enabled;
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

            Controller.ConnectAsync(client_id);
            connection_lease_held = true;
        }

        public void Disconnect()
        {
            if (!connection_lease_held)
            {
                return;
            }

            Controller.DisconnectAsync(client_id);
            connection_lease_held = false;
        }

        public bool Connected
        {
            get
            {
                return Controller.IsClientConnected(client_id);
            }
            set
            {
                if (value)
                {
                    Controller.ConnectBlocking(client_id);
                    connection_lease_held = true;
                }
                else
                {
                    Controller.DisconnectBlocking(client_id);
                    connection_lease_held = false;
                }
            }
        }

        public bool Connecting
        {
            get
            {
                return Controller.GetConnecting(client_id);
            }
        }

        public string Description
        {
            get
            {
                return Controller.Description;
            }
        }

        public string DriverInfo
        {
            get
            {
                return Controller.DriverInfo;
            }
        }

        public string DriverVersion
        {
            get
            {
                return Controller.DriverVersion;
            }
        }

        public short InterfaceVersion
        {
            get
            {
                return Controller.InterfaceVersion;
            }
        }

        public string Name
        {
            get
            {
                return Controller.Name;
            }
        }

        public IStateValueCollection DeviceState
        {
            get
            {
                List<StateValue> return_value = new List<StateValue>();
                return_value.Add(new StateValue(nameof(IFilterWheelV3.Position), Controller.GetDeviceStatePosition(client_id)));
                return_value.Add(new StateValue(DateTime.Now));
                return new StateValueCollection(return_value);
            }
        }

        public int[] FocusOffsets
        {
            get
            {
                return Controller.GetFocusOffsets(client_id);
            }
        }

        public string[] Names
        {
            get
            {
                return Controller.GetNames(client_id);
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
                return Controller.GetPosition(client_id);
            }
            set
            {
                Controller.SetPosition(client_id, value);
            }
        }
    }
}
