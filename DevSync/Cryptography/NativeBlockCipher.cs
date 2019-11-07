using System;
using System.Security.Cryptography;
using Renci.SshNet.Security.Cryptography;

namespace DevSync.Cryptography
{
    public abstract class NativeBlockCipher : BlockCipher
    {
        protected SymmetricAlgorithm SymmetricAlgorithm;
        protected ICryptoTransform _encryptor;
        protected ICryptoTransform _decryptor;
        protected byte[] Iv;

        protected NativeBlockCipher(byte[] key, byte blockSize, Renci.SshNet.Security.Cryptography.Ciphers.CipherMode mode) : base(key, blockSize, mode, null)
        {
            Iv = new byte[BlockSize];
        }

        protected NativeBlockCipher(byte[] key, byte[] iv, byte blockSize, Renci.SshNet.Security.Cryptography.Ciphers.CipherMode mode) : this(key, blockSize, mode)
        {
            Buffer.BlockCopy(iv, 0, Iv, 0, BlockSize);
        }

        protected abstract SymmetricAlgorithm Create();

        protected SymmetricAlgorithm GetSymmetricAlgorithm()
        {
            return SymmetricAlgorithm ??= Create();
        }

        public override int EncryptBlock(byte[] inputBuffer, int inputOffset, int inputCount, byte[] outputBuffer, int outputOffset)
        {
            if (_encryptor == null)
            {
                _encryptor = GetSymmetricAlgorithm().CreateEncryptor(Key, Iv);
            }

            return _encryptor.TransformBlock(inputBuffer, inputOffset, inputCount, outputBuffer, outputOffset);
        }

        public override int DecryptBlock(byte[] inputBuffer, int inputOffset, int inputCount, byte[] outputBuffer, int outputOffset)
        {
            if (_decryptor == null)
            {
                _decryptor = GetSymmetricAlgorithm().CreateDecryptor(Key, Iv);
            }
            return _decryptor.TransformBlock(inputBuffer, inputOffset, inputCount, outputBuffer, outputOffset);
        }
    }
}
