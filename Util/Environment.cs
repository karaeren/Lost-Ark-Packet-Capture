namespace Lost_Ark_Packet_Capture
{
    public static class Environment
    {
#if DEBUG
        public static bool debugMode = true;
#else
        public static bool debugMode = false;
#endif

        public static Byte[] XorTable = Convert.FromBase64String("HEmbc66I4wGqJ51SDaxtjQv8sHDek7Wrkhc2XwQuscBcQYyPmJ48i3+W3WwmlD7UtmHuh/JLcRtCWlX368npTVsRmZW/fsf+e3rLJA7FtGe8a1A3A9B9RBq4yiCDBuqtakPsgXwHJRg0R5yQ+pfiCLnaafUjE86pImWndG87iYRTzwkZdQzWSDjVpPMt4MS3kbIpeP2iPaX0TJ/5YuHlaErSHShXTk/D2zUV5gVyVr7XWUWgZsLfhlGAo806OWQsH3nnEAAe8Paa0Rb78bsSM/gK713tY4W96LMvD8HY3CpGxqYUYDEwMoIhim66r45YdyvkdgLI2aheVD9A0/+hzA==");

        public static HashSet<OpCodes> relevantOps = new HashSet<OpCodes>()
        {
            OpCodes.PKTNewProjectile,
            OpCodes.PKTNewPC,
            OpCodes.PKTInitEnv,
            OpCodes.PKTSkillDamageNotify,
            OpCodes.PKTNewNpc
        };
    }
}
