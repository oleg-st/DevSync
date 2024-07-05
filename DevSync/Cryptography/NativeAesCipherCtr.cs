using Renci.SshNet.Security.Cryptography.Ciphers.Modes;
using System.Security.Cryptography;

namespace DevSync.Cryptography;

public class NativeAesCipherCtr(byte[] key, byte[] iv) : NativeBlockCipher(key, 16, new CtrCipherMode(iv))
{
    protected override SymmetricAlgorithm Create()
    {
        var aes = Aes.Create();
        aes.Mode = CipherMode.ECB;
        aes.Padding = PaddingMode.None;
        aes.BlockSize = BlockSize * 8;
        aes.KeySize = Key.Length * 8;
        return aes;
    }
}