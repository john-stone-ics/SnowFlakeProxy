using System.Threading.Tasks;

namespace ASCOM.SnowFlakeProxy
{
    internal enum HardwareCommand
    {
        Connect,
        Disconnect,
        StartMove,
        OpenVendorSetup,
        Shutdown
    }

    internal sealed class HardwareRequest
    {
        internal HardwareCommand command;
        internal short target_position;
        internal long move_sequence;
        internal TaskCompletionSource<object> completion;
        internal TaskCompletionSource<object> setter_accepted;
    }
}
