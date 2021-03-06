using System.Runtime.InteropServices;

namespace Lost_Ark_Packet_Capture
{
    public static class Oodle
    {
        [DllImport("oo2net_9_win64")] static extern bool OodleNetwork1UDP_Decode(byte[] state, byte[] shared, byte[] comp, int compLen, byte[] raw, int rawLen);
        [DllImport("oo2net_9_win64")] static extern bool OodleNetwork1UDP_State_Uncompact(byte[] state, byte[] compressorState);
        [DllImport("oo2net_9_win64")] static extern void OodleNetwork1_Shared_SetWindow(byte[] data, int length, byte[] data2, int length2);
        [DllImport("oo2net_9_win64")] static extern int OodleNetwork1UDP_State_Size();
        [DllImport("oo2net_9_win64")] static extern int OodleNetwork1_Shared_Size(int bits);

        static Byte[] oodleState;
        static Byte[] oodleSharedDict;
        const string oodleDll = "oo2net_9_win64.dll";
        public static void OodleInit()
        {
            if (!File.Exists("oo2net_9_win64.dll"))
            {
                var laLocation = Microsoft.Win32.Registry.LocalMachine.OpenSubKey(@"SOFTWARE\Microsoft\Windows\CurrentVersion\Uninstall\Steam App 1599340")?.GetValue("InstallLocation");
                if (laLocation != null)
                {
                    var oodleDll = Path.Combine(laLocation.ToString(), "Binaries", "Win64", "oo2net_9_win64.dll");
                    if (File.Exists(oodleDll))
                    {
                        File.Copy(oodleDll, "oo2net_9_win64.dll");
                    }
                }
                else if (File.Exists(@"C:\Program Files (x86)\Steam\steamapps\common\Lost Ark\Binaries\Win64\oo2net_9_win64.dll"))
                {
                    File.Copy(@"C:\Program Files (x86)\Steam\steamapps\common\Lost Ark\Binaries\Win64\oo2net_9_win64.dll", "oo2net_9_win64.dll");
                }
            }


            if (!File.Exists("oo2net_9_win64.dll"))
            {
                Console.WriteLine("Please copy oo2net_9_win64.dll from Lost Ark directory to current directory.");
                System.Environment.Exit(1);
            }

            var payload = ObjectSerialize.Decompress(Properties.Resources.oodle_state);
            var dict = payload.Skip(0x20).Take(0x800000).ToArray();
            var compressorSize = BitConverter.ToInt32(payload, 0x18);
            var compressorState = payload.Skip(0x20).Skip(0x800000).Take(compressorSize).ToArray();
            var stateSize = OodleNetwork1UDP_State_Size();
            oodleState = new Byte[stateSize];
            if (!OodleNetwork1UDP_State_Uncompact(oodleState, compressorState))
            {
                EZLogger.log("error", "oodle init failed");
                return;
            };
            oodleSharedDict = new Byte[OodleNetwork1_Shared_Size(0x13)];
            OodleNetwork1_Shared_SetWindow(oodleSharedDict, 0x13, dict, 0x800000);
        }

        public static Byte[] OodleDecompress(Byte[] decompressed)
        {
            var oodleSize = BitConverter.ToInt32(decompressed, 0);
            var payload = decompressed.Skip(4).ToArray();
            var tempPayload = new Byte[oodleSize];
            try
            {
                if (!OodleNetwork1UDP_Decode(oodleState, oodleSharedDict, payload, payload.Length, tempPayload, oodleSize))
                {
                    EZLogger.log("error", "oodle decompress failed");
                    return null;
                };
            }
            catch (Exception e)
            {
                EZLogger.log("message", "access excepted");
                Console.WriteLine("access excepted");
            }
            return tempPayload;
        }
    }
}
