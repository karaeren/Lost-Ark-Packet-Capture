namespace Lost_Ark_Packet_Capture
{
    public class SkillEffect
    {
        public static Dictionary<Int32, String> Items = (Dictionary<Int32, String>)ObjectSerialize.Deserialize(Properties.Resources.SkillEffect);
    }
}
