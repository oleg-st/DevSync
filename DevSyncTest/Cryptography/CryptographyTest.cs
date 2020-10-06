using DevSync.Cryptography;
using DevSync.Cryptography.AesNi;
using Renci.SshNet.Security.Cryptography;
using System;
using Xunit;

namespace DevSyncTest.Cryptography
{
    public class CryptographyTest
    {
        [Fact]
        public void TestAesCtr()
        {
            TestAesCiphers((key, iv) => new NativeAesCipherCtr(key, iv), (key, iv) => new AesNiCipherCtr(key, iv));
        }

        [Fact]
        public void TestAesCbc()
        {
            TestAesCiphers((key, iv) => new NativeAesCipherCbc(key, iv), (key, iv) => new AesNiCipherCbc(key, iv));
        }

        private void TestAesCiphers(Func<byte[], byte[], Cipher> cipherFunc, Func<byte[], byte[], Cipher> aesNiCipherFunc)
        {
            var random = new Random();

            var keySizes = new[] { 128, 192, 256 };

            foreach (var keySize in keySizes)
            {
                var key = new byte[keySize / 8];
                random.NextBytes(key);
                var iv = new byte[32];
                random.NextBytes(iv);
                var source = new byte[1024];
                random.NextBytes(source);

                var encryptCipher = cipherFunc(key, iv);
                var encrypted = encryptCipher.Encrypt(source);
                var decryptCipher = cipherFunc(key, iv);
                var decrypted = decryptCipher.Decrypt(encrypted);
                Assert.Equal(source, decrypted);

                if (AesNiCipherBase.IsSupported)
                {
                    var aesNiEncryptCipher = aesNiCipherFunc(key, iv);
                    var aesNiEncrypted = aesNiEncryptCipher.Encrypt(source);
                    Assert.Equal(encrypted, aesNiEncrypted);

                    var aesNiDecryptCipher = aesNiCipherFunc(key, iv);
                    var aesNiDecrypted = aesNiDecryptCipher.Decrypt(encrypted);
                    Assert.Equal(source, aesNiDecrypted);
                }
            }
        }
    }
}
