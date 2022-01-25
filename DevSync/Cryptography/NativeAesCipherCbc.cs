using System.Security.Cryptography;

namespace DevSync.Cryptography
{
    public class NativeAesCipherCbc : NativeBlockCipher
    {
        public NativeAesCipherCbc(byte[] key, byte[] iv) : base(key, iv, 16, null)
        {
        }

        protected override SymmetricAlgorithm Create()
        {
            var aes = Aes.Create();
            aes.Mode = CipherMode.CBC;
            aes.Padding = PaddingMode.None;
            aes.BlockSize = BlockSize * 8;
            aes.KeySize = Key.Length * 8;
            return aes;
        }
    }
}
