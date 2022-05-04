﻿using System.Net;
using System.Net.Sockets;

namespace Lost_Ark_Packet_Capture
{
    public class PacketCapture
    {
        // Matches projectiles with owners. Used for connection players with their created projectiles.
        Dictionary<UInt64, Entity> Projectiles = new Dictionary<UInt64, Entity>();
        // Character and Target entity list.
        HashSet<Entity> Characters = new HashSet<Entity>();
        HashSet<Entity> Targets = new HashSet<Entity>();

        // Hashset for Sockets. For each available IP, a socket connection will be open if possible.
        HashSet<Socket> Sockets = new HashSet<Socket>();
        // Packet Buffer
        Byte[] packetBuffer = new Byte[0x10000];

        // Queue for packets to later be dissassembled.
        private Queue<Packet> packetQueue = new Queue<Packet>();

        public void Start()
        {
            Oodle.OodleInit();
            EZLogger.log("debug", "Oodle init done!");

            FirewallManager.AllowFirewall();
            EZLogger.log("debug", "Firewall rules set up!");

            UpdateXorTableFromRemote();
            EZLogger.log("debug", "Got the Xor Table");

            StartConnection();
            EZLogger.log("debug", "All connections started");

            Thread workerThread = new Thread(new ThreadStart(backgroundPacketProcessor));
            workerThread.Start();
            EZLogger.log("debug", "Running background worker!");

            EZLogger.log("message", "Connection is ready!");

            ElectronConnection.connection.Listen();
        }

        public void UpdateXorTableFromRemote()
        {
            try
            {
                System.Net.WebClient wc = new System.Net.WebClient();
                byte[] raw = wc.DownloadData("https://gitcdn.link/cdn/karaeren/Lost-Ark-Packet-Capture/master/Data/xor.txt");

                string webData = System.Text.Encoding.UTF8.GetString(raw);
                Environment.XorTable = Convert.FromBase64String(webData);
            }
            catch (Exception e)
            {
                EZLogger.log("error", "An error happened while trying to retrieve remote Xor table.");
            }
        }

        public void StartConnection()
        {
            var host = Dns.GetHostEntry(Dns.GetHostName());
            foreach (var ip in host.AddressList)
            {
                if (ip.AddressFamily != AddressFamily.InterNetwork) continue;

                try
                {
                    var socket = new Socket(AddressFamily.InterNetwork, SocketType.Raw, ProtocolType.IP);
                    socket.Bind(new IPEndPoint(ip, 0));
                    socket.SetSocketOption(SocketOptionLevel.IP, SocketOptionName.HeaderIncluded, true);
                    socket.IOControl(IOControlCode.ReceiveAll, BitConverter.GetBytes(1), BitConverter.GetBytes(0));
                    socket.BeginReceive(packetBuffer, 0, packetBuffer.Length, SocketFlags.None, new AsyncCallback(OnReceive), Sockets.Count);
                    Sockets.Add(socket);
                    EZLogger.log("debug", "Connection on " + ip);
                }
                catch
                {
                    EZLogger.log("debug", "No connection on " + ip);
                }
            }
        }

        private void OnReceive(IAsyncResult ar)
        {
            try
            {
                var bytesRead = Sockets.ElementAt((int)ar.AsyncState).EndReceive(ar);
                if (bytesRead > 0)
                {
                    Device_OnPacketArrival(packetBuffer.Take((int)bytesRead).ToArray());
                    packetBuffer = new Byte[packetBuffer.Length];
                }
            }
            catch (Exception ex)
            {
                EZLogger.log("error", ex.Message);
            }
            Sockets.ElementAt((int)ar.AsyncState).BeginReceive(packetBuffer, 0, packetBuffer.Length, SocketFlags.None, new AsyncCallback(OnReceive), (int)ar.AsyncState);
        }
        void Xor(byte[] data, int seed, byte[] xorKey)
        {
            for (int i = 0; i < data.Length; i++) data[i] = (byte)(data[i] ^ xorKey[seed++ % xorKey.Length]);
        }

        UInt32 currentIpAddr = 0xdeadbeef;
        Byte[] fragmentedPacket = new Byte[0];

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
                Xor(payload, (UInt16)opcode, Environment.XorTable);
                if (packets[4] == 3) payload = Oodle.OodleDecompress(payload).Skip(16).ToArray();

                if (Environment.relevantOps.Contains(opcode))
                {
                    Packet p = new Packet();
                    p.payload = payload;
                    p.op = opcode;
                    packetQueue.Enqueue(p);
                }

                if (packets.Length < packetSize) EZLogger.log("error", "Bad packet maybe");
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
                        Entity c = Entity.GetEntityById(playerId, Characters);
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
                        EZLogger.log("message", "new instance");
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

                            Entity c = Entity.GetEntityById(ownerId, Characters);
                            if (c != null)
                            {
                                if (className == "UnknownClass" && c.ClassName == "")
                                {
                                    continue;
                                }
                                else if (c.ClassName == "" && className != "UnknownClass")
                                    c.ClassName = className;

                                Entity t = Entity.GetEntityById(dmgEvent.TargetId, Characters);
                                var targetName = t != null ? t.Name : dmgEvent.TargetId.ToString("X");
                                EZLogger.log("data", DateTime.Now.ToString("yy:MM:dd:HH:mm:ss.f") + "," + c.Name + " (" + c.ClassName + ")" + "," + targetName + "," + skillName + "," + dmgEvent.Damage + "," + (((dmgEvent.FlagsMaybe & 0x81) > 0) ? "1" : "0") + "," + (((dmgEvent.FlagsMaybe & 0x10) > 0) ? "1" : "0") + "," + (((dmgEvent.FlagsMaybe & 0x20) > 0) ? "1" : "0"));
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
