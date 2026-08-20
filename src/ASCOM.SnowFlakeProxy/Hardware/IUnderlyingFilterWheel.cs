namespace ASCOM.SnowFlakeProxy
{
    internal interface IUnderlyingFilterWheel
    {
        bool Connected { get; set; }

        string Name { get; }

        string Description { get; }

        string DriverVersion { get; }

        string DriverInfo { get; }

        short InterfaceVersion { get; }

        string[] Names { get; }

        int[] FocusOffsets { get; }

        short Position { get; set; }

        void SetupDialog();

        void Dispose();
    }
}
