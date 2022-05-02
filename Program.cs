namespace Lost_Ark_Packet_Capture
{
    public partial class Program
    {
        static void Main()
        {
            PacketCapture packetCapture = new PacketCapture();
            packetCapture.Start();
        }
    }
}