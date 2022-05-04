using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace Lost_Ark_Packet_Capture
{
    public static class EZLogger
    {
        public static void log(String requestType, String message)
        {
            if (requestType == "debug" && !Environment.debugMode) return;

            if (Environment.debugMode)
                Console.WriteLine(requestType + ": " + message);
            else
            {
                ElectronConnection.connection.Send(requestType, message);
            }
        }
    }
}
