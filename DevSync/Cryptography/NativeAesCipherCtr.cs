using System.Security.Cryptography;
using Renci.SshNet.Security.Cryptography.Ciphers.Modes;

namespace DevSync.Cryptography
{
    public class NativeAesCipherCtr : NativeBlockCipher
    {
        public NativeAesCipherCtr(byte[] key, byte[] iv) : base(key, 16, new CtrCipherMode(iv))
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
