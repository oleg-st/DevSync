using DevSync.Cryptography.AesNi;
using Renci.SshNet.Security.Cryptography;
using System;

namespace DevSync.Cryptography;

public static class AesCipherFactory
{
    public enum Mode
    {
        Ctr,
        Cbc,
    }

    public static Cipher Create(byte[] key, byte[] iv, Mode mode)
    {
        // Aes-Ni accelerated implementations
        // https://en.wikipedia.org/wiki/AES_instruction_set
        if (AesNiCipherBase.IsSupported)
        {
            switch (mode)
            {
                case Mode.Ctr:
                    return new AesNiCipherCtr(key, iv);
                case Mode.Cbc:
                    return new AesNiCipherCbc(key, iv);
            }
        }

        switch (mode)
        {
            case Mode.Ctr:
                return new NativeAesCipherCtr(key, iv);
            case Mode.Cbc:
                return new NativeAesCipherCbc(key, iv);
            default:
                throw new NotSupportedException($"Not supported mode {mode}");
        }
    }
}