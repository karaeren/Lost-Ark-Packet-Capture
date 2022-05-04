using ElectronCgi.DotNet;
using System.Net;
using System.Net.Sockets;
using System.Runtime.InteropServices;
using System.Threading;

namespace Lost_Ark_Packet_Capture
{
    public class PacketCapture
    {
#if DEBUG
        bool debugMode = true;
#else
        bool debugMode = false;
#endif
        public class Entity
        {
            public UInt64 Id;
            public String Name;
            public String ClassName = "";
        }

        public static Entity GetEntityById(UInt64 Id)
        {
            foreach (Entity c in Characters)
            {
                if (c.Id == Id)
                    return c;
            }
            return null;
        }

        public class Packet
        {
            public byte[] payload;
            public OpCodes op;
        }

        public Dictionary<UInt64, Entity> Projectiles = new Dictionary<UInt64, Entity>();
        public static HashSet<Entity> Characters = new HashSet<Entity>();
        public static HashSet<Entity> Targets = new HashSet<Entity>();

        Socket socket;
        Byte[] packetBuffer = new Byte[0x10000]; // Packet Buffer
        Connection connection;

        private Queue<Packet> packetQueue = new Queue<Packet>();
        private HashSet<OpCodes> relevantOps = new HashSet<OpCodes>()
        {
            OpCodes.PKTNewProjectile,
            OpCodes.PKTNewPC,
            OpCodes.PKTInitEnv,
            OpCodes.PKTSkillDamageNotify,
            OpCodes.PKTNewNpc
        };

        public void Start()
        {
            connection = new ConnectionBuilder().WithLogging().Build();

            OodleInit();
            if(debugMode)
                connection.Send("message", "Oodle init done!");

            FirewallManager.AllowFirewall();
            if(debugMode)
                connection.Send("message", "Firewall rules set up!");

            socket = new Socket(AddressFamily.InterNetwork, SocketType.Raw, ProtocolType.IP);
            socket.Bind(new IPEndPoint(GetLocalIPAddress(), 0));
            socket.SetSocketOption(SocketOptionLevel.IP, SocketOptionName.HeaderIncluded, true);
            socket.IOControl(IOControlCode.ReceiveAll, BitConverter.GetBytes(1), BitConverter.GetBytes(0));
            byte[] packetBuffer1 = packetBuffer;
            socket.BeginReceive(packetBuffer1, 0, packetBuffer1.Length, SocketFlags.None, new AsyncCallback(OnReceive), null);
            if(debugMode)
                connection.Send("message", "Socket is set up at " + GetLocalIPAddress().ToString() + "!");

            Thread workerThread = new Thread(new ThreadStart(backgroundPacketProcessor));
            workerThread.Start();
            if(debugMode)
                connection.Send("message", "Running background worker!");

            connection.Send("message", "Connection is ready!");
            connection.Listen();
        }

        public static IPAddress GetLocalIPAddress()
        {
            // TODO: find a better way to get ethernet/wifi adapter address
            var host = Dns.GetHostEntry(Dns.GetHostName());
            //var activeDevice = NetworkInterface.GetAllNetworkInterfaces().First(n => n.OperationalStatus == OperationalStatus.Up && n.NetworkInterfaceType != NetworkInterfaceType.Loopback);
            //var activeDeviceIpProp = activeDevice.GetIPProperties().UnicastAddresses.Select(a => a.Address.AddressFamily == AddressFamily.InterNetwork);
            var ipAddress = host.AddressList.FirstOrDefault(ip => ip.AddressFamily == AddressFamily.InterNetwork);
            return ipAddress;
        }

        private void OnReceive(IAsyncResult ar)
        {
            try
            {
                var bytesRead = socket?.EndReceive(ar);
                if (bytesRead > 0)
                {
                    Device_OnPacketArrival(packetBuffer.Take((int)bytesRead).ToArray());
                    packetBuffer = new Byte[packetBuffer.Length];
                }
            }
            catch (Exception ex)
            {
                connection.Send("error", ex.Message);
            }
            socket?.BeginReceive(packetBuffer, 0, packetBuffer.Length, SocketFlags.None, new AsyncCallback(OnReceive), null);
        }

        public Dictionary<UInt64, UInt64> ProjectileOwner = new Dictionary<UInt64, UInt64>();
        public Dictionary<UInt64, String> IdToName = new Dictionary<UInt64, String>();
        public Dictionary<String, String> NameToClass = new Dictionary<String, String>();
        UInt32 currentIpAddr = 0xdeadbeef;
        Byte[] fragmentedPacket = new Byte[0];
        Byte[] XorTable = Convert.FromBase64String("lhs9nuO0tqJVKLVNYzeXhClXrz44CESmP/HiNeeIfO6kLCEiwJEPkE7G9uEA0mv+AV0ET2aUgka30RFvL229aQbV9bvI+JtFcxRLHMKTlWFcs3Qtz0BCFaOAuF4uq3oK140JeyAfB6pIwR3e97o7jJIQvhmnyo6JnDFD3DDqEw7/hRc2zYflUXhU5J1QpW6B9DN9TIa8AypWEtZ++iNJ78vD/Dx16cRost8W05kYmOgmauuLOVrzxUry0INHU4rOcXfM8PlgKzpYYiSaC+DYZ/u5/VlSrqGgZDTUW92tv2wC2agncrDHBdvmDSUeMrGs7XCpeX9BGsmfZezaX48Mdg==");
        
        void Xor(byte[] data, int seed, byte[] xorKey)
        {
            for (int i = 0; i < data.Length; i++) data[i] = (byte)(data[i] ^ xorKey[seed++ % xorKey.Length]);
        }

        [DllImport("oo2net_9_win64")] static extern bool OodleNetwork1UDP_Decode(byte[] state, byte[] shared, byte[] comp, int compLen, byte[] raw, int rawLen);
        [DllImport("oo2net_9_win64")] static extern bool OodleNetwork1UDP_State_Uncompact(byte[] state, byte[] compressorState);
        [DllImport("oo2net_9_win64")] static extern void OodleNetwork1_Shared_SetWindow(byte[] data, int length, byte[] data2, int length2);
        [DllImport("oo2net_9_win64")] static extern int OodleNetwork1UDP_State_Size();
        [DllImport("oo2net_9_win64")] static extern int OodleNetwork1_Shared_Size(int bits);
     
        Byte[] oodleState;
        Byte[] oodleSharedDict;

        void OodleInit()
        {
            while (!File.Exists("oo2net_9_win64.dll"))
            {
                if (File.Exists(@"C:\Program Files (x86)\Steam\steamapps\common\Lost Ark\Binaries\Win64\oo2net_9_win64.dll"))
                {
                    File.Copy(@"C:\Program Files (x86)\Steam\steamapps\common\Lost Ark\Binaries\Win64\oo2net_9_win64.dll", "oo2net_9_win64.dll");
                    continue;
                }
                connection.Send("error", "Please copy oo2net_9_win64 from LostArk directory to current directory.");
            }
            var payload = ObjectSerialize.Decompress(Properties.Resources.oodle_state);
            var dict = payload.Skip(0x20).Take(0x800000).ToArray();
            var compressorSize = BitConverter.ToInt32(payload, 0x18);
            var compressorState = payload.Skip(0x20).Skip(0x800000).Take(compressorSize).ToArray();
            var stateSize = OodleNetwork1UDP_State_Size();
            oodleState = new Byte[stateSize];
            if (!OodleNetwork1UDP_State_Uncompact(oodleState, compressorState)) throw new Exception("oodle init fail");
            oodleSharedDict = new Byte[OodleNetwork1_Shared_Size(0x13) * 2];
            OodleNetwork1_Shared_SetWindow(oodleSharedDict, 0x13, dict, 0x800000);
        }

        Byte[] OodleDecompress(Byte[] decompressed)
        {
            var oodleSize = BitConverter.ToInt32(decompressed, 0);
            var payload = decompressed.Skip(4).ToArray();
            var tempPayload = new Byte[oodleSize];
            if (!OodleNetwork1UDP_Decode(oodleState, oodleSharedDict, payload, payload.Length, tempPayload, oodleSize))
            {
                OodleInit();
                if (!OodleNetwork1UDP_Decode(oodleState, oodleSharedDict, payload, payload.Length, tempPayload, oodleSize))
                    throw new Exception("oodle decompress fail");
            }
            return tempPayload;
        }

        void ProcessPacket(List<Byte> data)
        {
            var packets = data.ToArray();
            var packetWithTimestamp = BitConverter.GetBytes(DateTime.UtcNow.ToBinary()).ToArray().Concat(data);
            while (packets.Length > 0)
            {
                if (fragmentedPacket.Length > 0)
                {
                    packets = fragmentedPacket.Concat(packets).ToArray();
                    fragmentedPacket = new Byte[0];
                }
                if (6 > packets.Length)
                {
                    fragmentedPacket = packets.ToArray();
                    continue;
                }

                var opcode = (OpCodes)BitConverter.ToUInt16(packets, 2);
                var packetSize = BitConverter.ToUInt16(packets.ToArray(), 0);
                if (packetSize > packets.Length)
                {
                    fragmentedPacket = packets.ToArray();
                    continue;
                }

                if (packets[5] != 1 || 6 > packets.Length || packetSize < 7) return;
                var payload = packets.Skip(6).Take(packetSize - 6).ToArray();
                Xor(payload, (UInt16)opcode, XorTable);
                if (packets[4] == 3) payload = OodleDecompress(payload).Skip(16).ToArray();

                if (relevantOps.Contains(opcode))
                {
                    Packet p = new Packet();
                    p.payload = payload;
                    p.op = opcode;
                    packetQueue.Enqueue(p);
                }

                if (packets.Length < packetSize) connection.Send("error", "Bad packet maybe");
                packets = packets.Skip(packetSize).ToArray();
            }
        }
        private void backgroundPacketProcessor()
        {
            while (true)
            {
                Thread.Sleep(10);

                var count = packetQueue.Count;
                if (count == 0)
                    continue;

                var newPackets = new List<Packet>();
                for (int i = 0; i < count; ++i)
                {
                    Packet packet;
                    lock (packetQueue)
                        packet = packetQueue.Dequeue();

                    if (packet == null)
                        continue;

                    newPackets.Add(packet);
                }


                foreach (var packet in newPackets)
                {
                    if (packet.op == OpCodes.PKTNewProjectile)
                    {
                        UInt64 projectileId = BitConverter.ToUInt64(packet.payload, 4);
                        UInt64 playerId = BitConverter.ToUInt64(packet.payload, 12);
                        Entity c = GetEntityById(playerId);
                        //connection.Send("message", "new projectile from " + playerId);
                        if (c != null)
                            Projectiles[projectileId] = c;
                    }
                    else if (packet.op == OpCodes.PKTNewPC)
                    {
                        var pc = new PKTNewPC(packet.payload);
                        var pcClass = Npc.GetPcClass(pc.ClassId);
                        Entity c = new Entity();
                        c.Name = pc.Name;
                        c.Id = pc.PlayerId;
                        c.ClassName = pcClass;
                        //connection.Send("message", "new player: " + pc.Name + " " + pc.PlayerId + " " + pcClass);
                        Characters.Add(c);
                    }
                    else if (packet.op == OpCodes.PKTInitEnv)
                    {
                        Characters.Clear();
                        Projectiles.Clear();
                        Targets.Clear();

                        var pc = new PKTInitEnv(packet.payload);
                        Entity c = new Entity();
                        c.Id = pc.PlayerId;
                        c.Name = "$You";
                        //connection.Send("message", "new instance, your id: " + pc.PlayerId);
                        connection.Send("message", "new instance");
                        Characters.Add(c);
                    }
                    else if (packet.op == OpCodes.PKTSkillDamageNotify)
                    {
                        var damage = new PKTSkillDamageNotify(packet.payload);

                        foreach (var dmgEvent in damage.Events)
                        {
                            var className = Skill.GetClassFromSkill(damage.SkillId);

                            var skillName = Skill.GetSkillName(damage.SkillId, damage.SkillIdWithState);
                            // if a projectile, get the owner's ID
                            var ownerId = Projectiles.ContainsKey(damage.PlayerId) ? Projectiles[damage.PlayerId].Id : damage.PlayerId;

                            Entity c = GetEntityById(ownerId);
                            if (c != null)
                            {
                                if (className == "UnknownClass" && c.ClassName == "")
                                {
                                    continue;
                                }
                                else if (c.ClassName == "" && className != "UnknownClass")
                                    c.ClassName = className;
                                
                                Entity t = GetEntityById(dmgEvent.TargetId);
                                var targetName = t != null ? t.Name : dmgEvent.TargetId.ToString("X");
                                connection.Send("data", DateTime.Now.ToString("yy:MM:dd:HH:mm:ss.f") + "," + c.Name + " (" + c.ClassName + ")" + "," + targetName + "," + skillName + "," + dmgEvent.Damage + "," + (((dmgEvent.FlagsMaybe & 0x81) > 0) ? "1" : "0") + "," + (((dmgEvent.FlagsMaybe & 0x10) > 0) ? "1" : "0") + "," + (((dmgEvent.FlagsMaybe & 0x20) > 0) ? "1" : "0"));
                            }
                        }
                    }
                }
            }
        }

        TcpReconstruction tcpReconstruction;
        void EndCapture()
        {
            currentIpAddr = 0xdeadbeef;
        }
        void Device_OnPacketArrival(Byte[] bytes)
        {
            if ((ProtocolType)bytes[9] == ProtocolType.Tcp) // 6
            {
                var tcp = new PacketDotNet.TcpPacket(new PacketDotNet.Utils.ByteArraySegment(bytes.Skip(20).ToArray()));
                if (tcp.SourcePort != 6040) return; // this filter should be moved up before parsing to TcpPacket for performance
                var srcAddr = BitConverter.ToUInt32(bytes, 12);
                if (srcAddr != currentIpAddr)
                {
                    if (tcp.PayloadData.Length > 4 && (OpCodes)BitConverter.ToUInt16(tcp.PayloadData, 2) == OpCodes.PKTAuthTokenResult && tcp.PayloadData[0] == 0x1e)
                    {
                        EndCapture();
                        currentIpAddr = srcAddr;
                        tcpReconstruction = new TcpReconstruction(ProcessPacket);
                    }
                    else return;
                }
                tcpReconstruction.ReassemblePacket(tcp);
            }
        }
    }
}
