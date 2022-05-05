using System.Collections.Concurrent;
using System.Net;
using System.Net.Sockets;

namespace Lost_Ark_Packet_Capture
{
    public class PacketCapture
    {
        Socket socket;
        Byte[] packetBuffer = new Byte[0x10000];

        public void Start()
        {
            Oodle.OodleInit();
            EZLogger.log("debug", "Oodle init done!");

            FirewallManager.AllowFirewall();
            EZLogger.log("debug", "Firewall rules set up!");

            var localIP = GetLocalIPAddress();
            socket = new Socket(AddressFamily.InterNetwork, SocketType.Raw, ProtocolType.IP);
            socket.Bind(new IPEndPoint(localIP, 6040));
            socket.SetSocketOption(SocketOptionLevel.IP, SocketOptionName.HeaderIncluded, true);
            socket.IOControl(IOControlCode.ReceiveAll, BitConverter.GetBytes(1), BitConverter.GetBytes(0));
            byte[] packetBuffer1 = packetBuffer;
            socket.BeginReceive(packetBuffer1, 0, packetBuffer1.Length, SocketFlags.None, new AsyncCallback(OnReceive), null);
            EZLogger.log("debug", "Socket is set up at " + localIP.ToString() + "!");

            Task.Run(() =>
            {
                while (!packetQueue.IsCompleted)
                {
                    if (packetQueue.TryTake(out Byte[] packet))
                    {
                        Device_OnPacketArrival(packet);
                        Thread.Sleep(10);
                    }
                }
            });

            EZLogger.log("debug", "All connections started");

            EZLogger.log("message", "Connection is ready!");

            ElectronConnection.connection.Listen();
        }

        public static IPAddress GetLocalIPAddress()
        {
            try
            {
                var tempSocket = new Socket(AddressFamily.InterNetwork, SocketType.Dgram, ProtocolType.Udp);
                var ipEndpoint = new IPEndPoint(IPAddress.Parse("8.8.8.8"), 6040).Serialize();
                var optionIn = new Byte[ipEndpoint.Size];
                for (int i = 0; i < ipEndpoint.Size; i++) optionIn[i] = ipEndpoint[i];
                var optionOut = new Byte[optionIn.Length];
                tempSocket.IOControl(IOControlCode.RoutingInterfaceQuery, optionIn, optionOut);
                tempSocket.Close();
                return new IPAddress(optionOut.Skip(4).Take(4).ToArray());
            }
            catch
            {
                var host = Dns.GetHostEntry(Dns.GetHostName());
                var ipAddress = host.AddressList.FirstOrDefault(ip => ip.AddressFamily == AddressFamily.InterNetwork);
                return ipAddress;
            }
        }

        BlockingCollection<Byte[]> packetQueue = new BlockingCollection<Byte[]>();
        private void OnReceive(IAsyncResult ar)
        {
            try
            {
                var bytesRead = socket?.EndReceive(ar);
                if (bytesRead > 0)
                {
                    var packets = new Byte[(int)bytesRead];
                    Array.Copy(packetBuffer, packets, (int)bytesRead);
                    packetQueue.Add(packets);
                }
            }
            catch (Exception ex)
            {
                EZLogger.log("error", ex.Message);
            }
            packetBuffer = new Byte[packetBuffer.Length];
            socket?.BeginReceive(packetBuffer, 0, packetBuffer.Length, SocketFlags.None, new AsyncCallback(OnReceive), ar);
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
                Xor.Cipher(payload, (UInt16)opcode, Environment.XorTable);
                if (packets[4] == 3) payload = Oodle.OodleDecompress(payload).Skip(16).ToArray();

                if (Environment.relevantOps.Contains(opcode))
                {
                    if (opcode == OpCodes.PKTNewProjectile)
                    {
                        //EZLogger.log("debug", "PKTNewProjectile");
                        //EZLogger.log("debug", Convert.ToHexString(payload));

                        UInt64 projectileId = BitConverter.ToUInt64(payload, 4);
                        UInt64 playerId = BitConverter.ToUInt64(payload, 12);

                        // new-projectile,playerId,projectileId
                        EZLogger.log("data-v2", "new-projectile," + playerId + "," + projectileId);
                    }
                    else if (opcode == OpCodes.PKTNewPC)
                    {
                        //EZLogger.log("debug", "PKTNewPC");
                        //EZLogger.log("debug", Convert.ToHexString(payload));

                        var pc = new PKTNewPC(payload);
                        var pcClass = Npc.GetPcClass(pc.ClassId);

                        // new-player,isYou,playerName,playerClass,playerId
                        EZLogger.log("data-v2", "new-player,0," + pc.Name + "," + pcClass + "," + pc.PlayerId);
                    }
                    else if (opcode == OpCodes.PKTInitEnv)
                    {
                        //EZLogger.log("debug", "PKTInitEnv");
                        //EZLogger.log("debug", Convert.ToHexString(payload));

                        var pc = new PKTInitEnv(payload);

                        EZLogger.log("data-v2", "new-instance");
                        // new-player,isYou,playerName,playerClass,playerId
                        EZLogger.log("data-v2", "new-player,1,You,UnknwonClass," + pc.PlayerId);
                    }
                    else if (opcode == OpCodes.PKTSkillDamageNotify)
                    {
                        //EZLogger.log("debug", "PKTSkillDamageNotify");
                        //EZLogger.log("debug", Convert.ToHexString(payload));

                        var damage = new PKTSkillDamageNotify(payload);

                        foreach (var dmgEvent in damage.Events)
                        {
                            var className = Skill.GetClassFromSkill(damage.SkillId);
                            var skillName = Skill.GetSkillName(damage.SkillId, damage.SkillIdWithState);
                            // if a projectile, get the owner's ID

                            EZLogger.log("data-v2",
                                    "skill-damage-notify," + // flag
                                                             //DateTime.Now.ToString("yy:MM:dd:HH:mm:ss.f") + "," + // date
                                    damage.PlayerId + "," + // damage playerId
                                    className + "," + // character class
                                    dmgEvent.TargetId + "," + // target name
                                    skillName + "," + // skill name
                                    dmgEvent.Damage + "," + // damage amount
                                    (((dmgEvent.FlagsMaybe & 0x81) > 0) ? "1" : "0") + "," + // crit flag
                                    (((dmgEvent.FlagsMaybe & 0x10) > 0) ? "1" : "0") + "," + // back attack flag
                                    (((dmgEvent.FlagsMaybe & 0x20) > 0) ? "1" : "0")); // front attack flag
                        }
                    }
                    else if (opcode == OpCodes.PKTSkillDamageAbnormalMoveNotify)
                    {
                        var damage = new PKTSkillDamageAbnormalMoveNotify(payload);
                        //for (var i = 0; i < payload.Length - 4; i++)
                        //    Console.WriteLine(i + " : " + BitConverter.ToUInt32(payload, i) + " : " + BitConverter.ToUInt32(payload, i).ToString("X"));
                        // normal mobs when skills make them move. not needed for boss tracking, since guardians don't get moved by abilities. this will show more damage taken by players
                    }
                }

                if (packets.Length < packetSize) EZLogger.log("error", "Bad packet maybe");
                packets = packets.Skip(packetSize).ToArray();
            }
        }

        TcpReconstruction tcpReconstruction;

        void Device_OnPacketArrival(Byte[] bytes)
        {
            if ((ProtocolType)bytes[9] == ProtocolType.Tcp) // 6
            {
                var tcp = new PacketDotNet.TcpPacket(new PacketDotNet.Utils.ByteArraySegment(bytes.Skip(20).ToArray()));
                if (tcp.SourcePort != 6040) return; // this filter should be moved up before parsing to TcpPacket for performance
                var srcAddr = BitConverter.ToUInt32(bytes, 12);
                if (srcAddr != currentIpAddr)
                {
                    // end session here
                    if (currentIpAddr == 0xdeadbeef || tcp.PayloadData.Length > 4 && (OpCodes)BitConverter.ToUInt16(tcp.PayloadData, 2) == OpCodes.PKTAuthTokenResult && tcp.PayloadData[0] == 0x1e)
                    {
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
