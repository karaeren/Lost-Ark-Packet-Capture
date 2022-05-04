﻿namespace Lost_Ark_Packet_Capture
{
    public static class Environment
    {
#if DEBUG
        public static bool debugMode = true;
#else
        public static bool debugMode = false;
#endif

        public static Byte[] XorTable = Convert.FromBase64String("lhs9nuO0tqJVKLVNYzeXhClXrz44CESmP/HiNeeIfO6kLCEiwJEPkE7G9uEA0mv+AV0ET2aUgka30RFvL229aQbV9bvI+JtFcxRLHMKTlWFcs3Qtz0BCFaOAuF4uq3oK140JeyAfB6pIwR3e97o7jJIQvhmnyo6JnDFD3DDqEw7/hRc2zYflUXhU5J1QpW6B9DN9TIa8AypWEtZ++iNJ78vD/Dx16cRost8W05kYmOgmauuLOVrzxUry0INHU4rOcXfM8PlgKzpYYiSaC+DYZ/u5/VlSrqGgZDTUW92tv2wC2agncrDHBdvmDSUeMrGs7XCpeX9BGsmfZezaX48Mdg==");

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