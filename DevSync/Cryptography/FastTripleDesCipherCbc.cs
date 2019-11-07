using System.Security.Cryptography;

namespace DevSync.Cryptography
{
    public class NativeTripleDesCipherCbc : NativeBlockCipher
    {
        public NativeTripleDesCipherCbc(byte[] key, byte[] iv) : base(key, iv, 8, null)
        {
        }

        protected override SymmetricAlgorithm Create()
        {
            return new TripleDESCryptoServiceProvider
            {
                Mode = CipherMode.CBC,
                Padding = PaddingMode.None,
                BlockSize = BlockSize * 8,
                KeySize = Key.Length * 8
            };
        }
    }
}
