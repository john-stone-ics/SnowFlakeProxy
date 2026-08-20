using System.Runtime.InteropServices;
using ASCOM.Utilities;

namespace ASCOM.SnowFlakeProxy
{
    [ComVisible(true)]
    [Guid("365A3B15-A96A-4FCC-9F11-8611652D5B02")]
    [ProgId(ProxyIdentity.proxy_prog_id_2)]
    [ServedClassName(ProxyIdentity.chooser_name_2)]
    [ClassInterface(ClassInterfaceType.None)]
    public class FilterWheel2 : FilterWheelBase
    {
        public FilterWheel2()
            : base(2)
        {
        }
    }
}
