using System;

namespace ASCOM.SnowFlakeProxy
{
    internal sealed class WandererFilterWheelAdapter : IUnderlyingFilterWheel
    {
        // The underlying ASCOM driver is thread-affine to the hardware worker.
        // Do not access this object from any other thread.
        private readonly ASCOM.DriverAccess.FilterWheel wanderer;

        internal WandererFilterWheelAdapter(string vendor_prog_id)
        {
            wanderer = new ASCOM.DriverAccess.FilterWheel(vendor_prog_id);
        }

        public bool Connected
        {
            get
            {
                return wanderer.Connected;
            }
            set
            {
                wanderer.Connected = value;
            }
        }

        public string Name
        {
            get
            {
                return wanderer.Name;
            }
        }

        public string Description
        {
            get
            {
                return wanderer.Description;
            }
        }

        public string DriverVersion
        {
            get
            {
                return wanderer.DriverVersion;
            }
        }

        public string DriverInfo
        {
            get
            {
                return wanderer.DriverInfo;
            }
        }

        public short InterfaceVersion
        {
            get
            {
                return wanderer.InterfaceVersion;
            }
        }

        public string[] Names
        {
            get
            {
                return wanderer.Names;
            }
        }

        public int[] FocusOffsets
        {
            get
            {
                return wanderer.FocusOffsets;
            }
        }

        public short Position
        {
            get
            {
                return wanderer.Position;
            }
            set
            {
                wanderer.Position = value;
            }
        }

        public void SetupDialog()
        {
            wanderer.SetupDialog();
        }

        public void Dispose()
        {
            try
            {
                if (wanderer.Connected)
                {
                    wanderer.Connected = false;
                }
            }
            catch (Exception)
            {
            }

            wanderer.Dispose();
        }
    }
}
