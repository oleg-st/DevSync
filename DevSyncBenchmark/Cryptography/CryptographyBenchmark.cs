using System;
using BenchmarkDotNet.Attributes;
using DevSync.Cryptography;
using DevSync.Cryptography.AesNi;
using Renci.SshNet.Security.Cryptography;

namespace DevSyncBenchmark.Cryptography
{
    public class CryptographyBenchmark
    {
        private byte[] _data;

        private Cipher _nativeAesCipherCtr;
        private NativeAesCipherCbc _nativeAesCipherCbc;
        private AesNiCipherCtr _aesNiCipherCtr;
        private AesNiCipherCbc _aesNiCipherCbc;

        [GlobalSetup]
        public void Setup()
        {
            var random = new Random();

            var keySize = 128;

            var key = new byte[keySize / 8];
            random.NextBytes(key);
            var iv = new byte[32];
            random.NextBytes(iv);
            _data = new byte[65536];
            random.NextBytes(_data);

            _nativeAesCipherCtr = new NativeAesCipherCtr(key, iv);
            _nativeAesCipherCbc = new NativeAesCipherCbc(key, iv);
            _aesNiCipherCtr = new AesNiCipherCtr(key, iv);
            _aesNiCipherCbc = new AesNiCipherCbc(key, iv);
        }

        [Benchmark]
        public void NativeAesCtr()
        {
            RunCipher(_nativeAesCipherCtr);
        }

        [Benchmark]
        public void NativeAesCbc()
        {
            RunCipher(_nativeAesCipherCbc);
        }

        [Benchmark]
        public void AesNiCipherCtr()
        {
            RunCipher(_aesNiCipherCtr);
        }


        [Benchmark]
        public void AesNiCipherCbc()
        {
            RunCipher(_aesNiCipherCbc);
        }


        private void RunCipher(Cipher cipher)
        {
            cipher.Encrypt(_data);
}
    }
}
