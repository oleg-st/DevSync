using System.Runtime.Intrinsics.X86;

namespace DevSync.Cryptography.AesNi;

public unsafe class AesNiCipherCbc(byte[] key, byte[] iv) : AesNiCipherBase(key, iv)
{
    public override int EncryptBlock(byte[] inputBuffer, int inputOffset, int inputCount, byte[] outputBuffer, int outputOffset)
    {
        fixed (byte* outputBufferPointer = outputBuffer)
        fixed (byte* inputBufferPointer = inputBuffer)
        {
            var inputVector = Sse2.LoadVector128(inputBufferPointer + inputOffset);
            IvVector = EncryptAes(Sse2.Xor(inputVector, IvVector));
            Sse2.Store(outputBufferPointer + outputOffset, IvVector);
        }
        return Size;
    }

    public override int DecryptBlock(byte[] inputBuffer, int inputOffset, int inputCount, byte[] outputBuffer, int outputOffset)
    {
        fixed (byte* outputBufferPointer = outputBuffer)
        fixed (byte* inputBufferPointer = inputBuffer)
        {
            var inputVector = Sse2.LoadVector128(inputBufferPointer + inputOffset);
            Sse2.Store(outputBufferPointer + outputOffset, Sse2.Xor(DecryptAes(inputVector), IvVector));
            IvVector = inputVector;
        }
        return Size;
    }
}