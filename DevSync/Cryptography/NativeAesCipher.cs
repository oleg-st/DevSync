using System.Security.Cryptography;

namespace DevSync.Cryptography
{
    public class NativeAesCipher : NativeBlockCipher
    {
        public NativeAesCipher(byte[] key, Renci.SshNet.Security.Cryptography.Ciphers.CipherMode mode) : base(key, 16, mode)
        {
        }

        protected override SymmetricAlgorithm Create()
        {
            return new AesCryptoServiceProvider
            {
                Mode = CipherMode.ECB,
                Padding = PaddingMode.None,
                BlockSize = BlockSize * 8,
                KeySize = Key.Length * 8
            };
        }
    }
}
