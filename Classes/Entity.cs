namespace Lost_Ark_Packet_Capture
{
    public class Entity
    {
        public UInt64 Id;
        public String Name;
        public String ClassName = "";

        public static Entity GetEntityById(UInt64 Id, HashSet<Entity> EntitySet)
        {
            foreach (Entity c in EntitySet)
            {
                if (c.Id == Id)
                    return c;
            }
            return null;
        }
    }
}
