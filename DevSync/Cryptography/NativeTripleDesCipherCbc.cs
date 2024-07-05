using System.Security.Cryptography;

namespace DevSync.Cryptography;

public class NativeTripleDesCipherCbc(byte[] key, byte[] iv) : NativeBlockCipher(key, iv, 8, null)
{
    protected override SymmetricAlgorithm Create()
    {
        var tripleDes = TripleDES.Create();
        tripleDes.Mode = CipherMode.CBC;
        tripleDes.Padding = PaddingMode.None;
        tripleDes.BlockSize = BlockSize * 8;
        tripleDes.KeySize = Key.Length * 8;
        return tripleDes;
    }
}