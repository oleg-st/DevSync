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
            return new AesCryptoServiceProvider
            {
                Mode = CipherMode.CBC,
                Padding = PaddingMode.None,
                BlockSize = BlockSize * 8,
                KeySize = Key.Length * 8
            };
        }
    }
}
