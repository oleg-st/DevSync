using System.Runtime.Intrinsics;
using System.Runtime.Intrinsics.X86;

namespace DevSync.Cryptography.AesNi;

public unsafe class AesNiCipherCtr : AesNiCipherBase
{
    // increment vectors:
    // (1, 1, 1, ..., 1, 1, 1)
    // (0, 1, 1, ..., 1, 1, 1)
    // (0, 0, 1, ..., 1, 1, 1)
    // ...
    // (0, 0, 0, ..., 1, 1, 1)
    // (0, 0, 0, ..., 0, 1, 1)
    // (0, 0, 0, ..., 0, 0, 1)
    private readonly Vector128<byte>[] _incrementVectors;

    public AesNiCipherCtr(byte[] key, byte[] iv) : base(key, iv)
    {
        // pre calculate increment vectors
        _incrementVectors = new Vector128<byte>[Size];
        var vector = Vector128.Create((byte)1);
        for (var i = 0; i < Size; i++)
        {
            _incrementVectors[i] = vector;
            vector = vector.WithElement(i, (byte)0);
        }
    }

    public override int EncryptBlock(byte[] inputBuffer, int inputOffset, int inputCount, byte[] outputBuffer, int outputOffset)
    {
        fixed (byte* outputBufferPointer = outputBuffer)
        fixed (byte* inputBufferPointer = inputBuffer)
        {
            var encryptedVector = EncryptAes(IvVector);
            var inputVector = Sse2.LoadVector128(inputBufferPointer + inputOffset);
            Sse2.Store(outputBufferPointer + outputOffset, Sse2.Xor(encryptedVector, inputVector));

            // increment IvVector
            var i = Size - 1;
            for (; i > 0 && IvVector.GetElement(i) == 0xff; i--)
            {
            }
            IvVector = Sse2.Add(IvVector, _incrementVectors[i]);
        }
        return Size;
    }

    public override int DecryptBlock(byte[] inputBuffer, int inputOffset, int inputCount, byte[] outputBuffer, int outputOffset) => 
        EncryptBlock(inputBuffer, inputOffset, inputCount, outputBuffer, outputOffset);
}