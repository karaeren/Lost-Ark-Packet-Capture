using K4os.Compression.LZ4;
using Snappy;
using System.Runtime.InteropServices;

namespace Lost_Ark_Packet_Capture
{
    public class PacketCapture
    {
        UInt32 currentIpAddr = 0xdeadbeef;
        Byte[] fragmentedPacket = new Byte[0];

        public void Start()
        {
            EZLogger.log("message", "Starting packet capture service.");

            Oodle.OodleInit();
            EZLogger.log("debug", "Oodle init done!");

            var use_npcap = true;
            // See if winpcap loads
            try
            {
                pcap_strerror(1);
            }
            catch (Exception ex)
            {
                EZLogger.log("message", ex.ToString());
                use_npcap = false; // Fall back to raw sockets
            }

            Machina.Infrastructure.NetworkMonitorType monitorType;
            var tcp = new Machina.TCPNetworkMonitor();
            tcp.Config.WindowClass = "EFLaunchUnrealUWindowsClient";
            if (use_npcap) monitorType = tcp.Config.MonitorType = Machina.Infrastructure.NetworkMonitorType.WinPCap;
            else monitorType = tcp.Config.MonitorType = Machina.Infrastructure.NetworkMonitorType.RawSocket;
            tcp.DataReceivedEventHandler += (Machina.Infrastructure.TCPConnection connection, byte[] data) => Device_OnPacketArrival(connection, data);
            tcp.Start();
            EZLogger.log("debug", "All connections started");

            EZLogger.log("message", "Connection is ready!");

            ElectronConnection.connection.Listen();
        }

#pragma warning disable CA2101 // Specify marshaling for P/Invoke string arguments
        [DllImport("wpcap.dll", CallingConvention = CallingConvention.Cdecl, CharSet = CharSet.Ansi)] static extern IntPtr pcap_strerror(int err);
#pragma warning restore CA2101 // Specify marshaling for P/Invoke string arguments

        void ProcessPacket(List<Byte> data)
        {
            var packets = data.ToArray();

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

                if (packets[5] != 1 || 6 > packets.Length || packetSize < 7)
                {
                    // not sure when this happens
                    fragmentedPacket = new Byte[0];
                    return;
                }
                if (packetSize > packets.Length)
                {
                    fragmentedPacket = packets.ToArray();
                    return;
                }

                var payload = packets.Skip(6).Take(packetSize - 6).ToArray();
                Xor.Cipher(payload, (UInt16)opcode, Environment.XorTable);

                switch (packets[4])
                {
                    case 1: //LZ4
                        var buffer = new byte[0x11ff2];
                        var result = LZ4Codec.Decode(payload, 0, payload.Length, buffer, 0,
                            0x11ff2);
                        if (result < 1)
                            throw new Exception("LZ4 output buffer too small");
                        payload = buffer.Take(result)
                            .ToArray(); //TODO: check LZ4 payload and see if we should skip some data
                        break;
                    case 2: //Snappy
                        //https://github.com/robertvazan/snappy.net
                        payload = SnappyCodec.Uncompress(payload).Skip(16).ToArray();
                        break;
                    case 3: //Oodle
                        payload = Oodle.OodleDecompress(payload).Skip(16).ToArray();
                        break;
                }

                if (opcode == OpCodes.PKTNewProjectile)
                {
                    //EZLogger.log("debug", "PKTNewProjectile");
                    //EZLogger.log("debug", Convert.ToHexString(payload));

                    UInt64 projectileId = BitConverter.ToUInt64(payload, 4);
                    UInt64 playerId = BitConverter.ToUInt64(payload, 12);

                    // new-projectile,playerId,projectileId
                    EZLogger.log("data", "new-projectile," + playerId + "," + projectileId);
                }
                else if (opcode == OpCodes.PKTNewPC)
                {
                    //EZLogger.log("debug", "PKTNewPC");
                    //EZLogger.log("debug", Convert.ToHexString(payload));

                    var pc = new PKTNewPC(payload);
                    var pcClass = Npc.GetPcClass(pc.ClassId);

                    // new-player,isYou,playerName,playerClass,playerId
                    EZLogger.log("data", "new-player,0," + pc.Name + "," + pcClass + "," + pc.PlayerId);
                }
                else if (opcode == OpCodes.PKTInitEnv)
                {
                    //EZLogger.log("debug", "PKTInitEnv");
                    //EZLogger.log("debug", Convert.ToHexString(payload));

                    var pc = new PKTInitEnv(payload);

                    EZLogger.log("data", "new-instance");
                    // new-player,isYou,playerName,playerClass,playerId
                    EZLogger.log("data", "new-player,1,You,UnknwonClass," + pc.PlayerId);
                    //foreach (var pid in pc.PlayerIds)
                    //{
                    //    EZLogger.log("data", "new-player,1,You,UnknwonClass," + pid);
                    //}
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

                        EZLogger.log("data",
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


                if (packets.Length < packetSize) EZLogger.log("error", "Bad packet maybe");
                packets = packets.Skip(packetSize).ToArray();
            }
        }

        void Device_OnPacketArrival(Machina.Infrastructure.TCPConnection connection, byte[] bytes)
        {
            if (connection.RemotePort != 6040) return;
            var srcAddr = connection.RemoteIP;
            if (srcAddr != currentIpAddr)
            {
                if (currentIpAddr == 0xdeadbeef || (bytes.Length > 4 && (OpCodes)BitConverter.ToUInt16(bytes, 2) == OpCodes.PKTAuthTokenResult && bytes[0] == 0x1e))
                {
                    currentIpAddr = srcAddr;
                }
                else return;
            }
            ProcessPacket(bytes.ToList());
        }
    }
}
