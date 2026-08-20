using System.Runtime.InteropServices;
using ASCOM.Utilities;

namespace ASCOM.SnowFlakeProxy
{
    [ComVisible(true)]
    [Guid("72421A3C-687A-4609-80D2-FB072AB9AC51")]
    [ProgId(ProxyIdentity.proxy_prog_id_3)]
    [ServedClassName(ProxyIdentity.chooser_name_3)]
    [ClassInterface(ClassInterfaceType.None)]
    public class FilterWheel3 : FilterWheelBase
    {
        public FilterWheel3()
            : base(3)
        {
        }
    }
}
