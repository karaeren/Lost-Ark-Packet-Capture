using System.Diagnostics;
using System.Security.Principal;

namespace Lost_Ark_Packet_Capture
{
    public partial class Program
    {
        static void Main()
        {
            if (!AdminRelauncher()) System.Environment.Exit(1);

            PacketCapture packetCapture = new PacketCapture();
            packetCapture.Start();
        }

        private static bool AdminRelauncher()
        {
            if (!new WindowsPrincipal(WindowsIdentity.GetCurrent()).IsInRole(WindowsBuiltInRole.Administrator))
            {
                var startInfo = new ProcessStartInfo
                {
                    UseShellExecute = true,
                    WorkingDirectory = System.Environment.CurrentDirectory,
                    FileName = System.Reflection.Assembly.GetExecutingAssembly().Location,
                    Verb = "runas"
                };
                try { Process.Start(startInfo); }
                // EZLogger is not initialized yet, this works for now...
                //catch (Exception ex) { Console.WriteLine("{\"type\":\"REQUEST\", \"request\":{ \"type\":\"error\", \"id\":\"35b96c9a-62bd-4c81-8b05-19fd3d4c24af\", \"args\":\"This program must be run as an administrator!\"}"); }
                catch(Exception ex) { System.Environment.Exit(1); }
                return false;
            }
            return true;
        }
    }
}