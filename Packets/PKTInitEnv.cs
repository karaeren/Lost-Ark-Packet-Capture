namespace Lost_Ark_Packet_Capture
{
    public class PKTInitEnv
    {
        //01-00-00-FC-FF-FF-FF-00-00-00-00-37-5C-2C-07-00-00-00-00-03-00-55-00-54-00-43-00-E1-89-05-21-00-00-00-00-E6-47-76-BE-06-5B-00-00
        public String UTC;
        //public UInt64 PlayerId;
        public HashSet<UInt64> PlayerIds;
        public PKTInitEnv(Byte[] bytes)
        {
            PlayerIds = new HashSet<UInt64>();
            // seems to be the packets are different for some people
            // this is a temp fix until there's a solid way to find packets...
            //PlayerId = BitConverter.ToUInt32(bytes, 15);
            PlayerIds.Add(BitConverter.ToUInt32(bytes, 15));
            PlayerIds.Add(BitConverter.ToUInt64(bytes, 10));
        }
    }
}
