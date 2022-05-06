using System.Diagnostics;
using System.Net;
using System.Net.Sockets;
using System.Security.Principal;

namespace Lost_Ark_Packet_Capture
{
    public partial class Program
    {
        static void Main()
        {
            if (!IsAdministrator())
            {
                Console.WriteLine("Please run this program as administrator.");
                System.Environment.Exit(1);
            }
            else
            {
                EZLogger.log("message", "Admin rights are set!");
            }

            AttemptFirewallPrompt();

            PacketCapture packetCapture = new PacketCapture();
            packetCapture.Start();
        }

        private static void AttemptFirewallPrompt()
        {
            var ipAddress = Dns.GetHostEntry(Dns.GetHostName()).AddressList[0];
            var ipLocalEndPoint = new IPEndPoint(ipAddress, 12345);
            var t = new TcpListener(ipLocalEndPoint);
            t.Start();
            t.Stop();
        }

        public static bool IsAdministrator()
        {
            var identity = WindowsIdentity.GetCurrent();
            var principal = new WindowsPrincipal(identity);
            return principal.IsInRole(WindowsBuiltInRole.Administrator);
        }
    }
}