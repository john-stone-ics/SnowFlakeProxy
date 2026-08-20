using System.Runtime.InteropServices;
using ASCOM.Utilities;

namespace ASCOM.SnowFlakeProxy
{
    [ComVisible(true)]
    [Guid("5A253603-5EBC-4667-9041-F24445724985")]
    [ProgId(ProxyIdentity.proxy_prog_id_1)]
    [ServedClassName(ProxyIdentity.chooser_name_1)]
    [ClassInterface(ClassInterfaceType.None)]
    public class FilterWheel : FilterWheelBase
    {
        public FilterWheel()
            : base(1)
        {
        }
    }
}
