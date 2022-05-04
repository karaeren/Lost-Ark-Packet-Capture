﻿namespace Lost_Ark_Packet_Capture
{
    public class Npc
    {
        public static Dictionary<String, Tuple<String, String>> Items = (Dictionary<String, Tuple<String, String>>)ObjectSerialize.Deserialize(Properties.Resources.Npc);
        public static String GetNpcName(UInt32 id)
        {
            var npcName = "";
            if (Items.ContainsKey(id.ToString()))
            {
                var npc = Items[id.ToString()];
                if (npc.Item1.Length > 0)
                {
                    if (GameMsg_English.Items.ContainsKey(npc.Item1)) npcName = GameMsg_English.Items[npc.Item1];
                }
                if (npcName.Length == 0) npcName = npc.Item2;
            }
            return npcName;
        }
        public static String GetPcClass(UInt32 id)
        {
            if (id == 101) return "Warrior";
            if (id == 201) return "Mage";
            if (id == 301) return "MartialArtist";
            if (id == 401) return "Assassin";
            if (id == 501) return "Gunner";
            if (id == 102) return "Berserker";
            if (id == 104) return "Gunlancer";
            if (id == 105) return "Paladin";
            if (id == 204) return "Bard";
            if (id == 205) return "Sorceress";
            if (id == 302) return "Wardancer";
            if (id == 303) return "Scrapper";
            if (id == 304) return "Soulfist";
            if (id == 305) return "Glavier";
            if (id == 402) return "Deathblade";
            if (id == 403) return "Shadowhunter";
            if (id == 502) return "Sharpshooter";
            if (id == 503) return "Deadeye";
            if (id == 504) return "Artillerist";
            if (id == 511) return "FemaleGunner";
            if (id == 512) return "Gunslinger";
            if (id == 311) return "MaleMartialArtist";
            if (id == 312) return "Striker";
            return "UnknownClass";
        }
    }
}
