using ElectronCgi.DotNet;

namespace Lost_Ark_Packet_Capture
{
    public static class ElectronConnection
    {
        public static Connection connection = new ConnectionBuilder().WithLogging().Build();
    }
}
