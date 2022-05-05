namespace Lost_Ark_Packet_Capture
{
    static class Xor
    {
        public static void Cipher(byte[] data, int seed, byte[] xorKey = null)
        {
            if (xorKey == null) xorKey = Environment.XorTable;
            for (int i = 0; i < data.Length; i++) data[i] = (byte)(data[i] ^ xorKey[seed++ % xorKey.Length]);
        }
    }
}
