using System;
using System.Runtime.CompilerServices;
using System.Runtime.Intrinsics;
using System.Runtime.Intrinsics.X86;
using Renci.SshNet.Security.Cryptography;

namespace DevSync.Cryptography.AesNi
{
    // inspired by https://github.com/jniegsch/SimpleCrypt/blob/master/src/AESni.c
    public abstract unsafe class AesNiCipherBase : BlockCipher
    {
        // AES block size
        public const int Size = 16;

        protected Vector128<byte> IvVector;
        protected readonly int Rounds;

        protected Vector128<byte>[] EncryptionKey, DecryptionKey;

        public static bool IsSupported => Aes.IsSupported;

        protected AesNiCipherBase(byte[] key, byte[] iv) : base(key, Size, null, null)
        {
            fixed (byte* ivPointer = iv)
            {
                IvVector = Sse2.LoadVector128(ivPointer);
            }

            // 16 -> 10, 24 -> 12, 32 -> 14
            Rounds = key.Length / 4 + 6;
        }

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        private static Vector128<byte> KeygenOnce128(Vector128<byte> schedulePrev, byte control)
        {
            var temp1 = schedulePrev;
            var temp2 = Aes.KeygenAssist(schedulePrev, control);
            temp2 = Sse2.Shuffle(temp2.As<byte, int>(), 0xff).As<int, byte>();
            var temp3 = Sse2.ShiftLeftLogical128BitLane(temp1, 0x4);
            temp1 = Sse2.Xor(temp1, temp3);
            temp3 = Sse2.ShiftLeftLogical128BitLane(temp3, 0x4);
            temp1 = Sse2.Xor(temp1, temp3);
            temp3 = Sse2.ShiftLeftLogical128BitLane(temp3, 0x4);
            temp1 = Sse2.Xor(temp1, temp3);
            temp1 = Sse2.Xor(temp1, temp2);
            return temp1;
        }

        private static void GenerateWorkingKey128(Vector128<byte>[] schedule, byte* keyPointer)
        {
            var keyVector = Sse2.LoadVector128(keyPointer);

            schedule[0] = keyVector;
            schedule[1] = KeygenOnce128(schedule[0], 0x01);
            schedule[2] = KeygenOnce128(schedule[1], 0x02);
            schedule[3] = KeygenOnce128(schedule[2], 0x04);
            schedule[4] = KeygenOnce128(schedule[3], 0x08);
            schedule[5] = KeygenOnce128(schedule[4], 0x10);
            schedule[6] = KeygenOnce128(schedule[5], 0x20);
            schedule[7] = KeygenOnce128(schedule[6], 0x40);
            schedule[8] = KeygenOnce128(schedule[7], 0x80);
            schedule[9] = KeygenOnce128(schedule[8], 0x1b);
            schedule[10] = KeygenOnce128(schedule[9], 0x36);
        }

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        private static void Keygen192Assist(ref Vector128<byte> temp1, ref Vector128<byte> temp2, ref Vector128<byte> temp3)
        {
            temp2 = Sse2.Shuffle(temp2.As<byte, int>(), 0x55).As<int, byte>();
            var temp4 = Sse2.ShiftLeftLogical128BitLane(temp1, 0x4);
            temp1 = Sse2.Xor(temp1, temp4);
            temp4 = Sse2.ShiftLeftLogical128BitLane(temp4, 0x4);
            temp1 = Sse2.Xor(temp1, temp4);
            temp4 = Sse2.ShiftLeftLogical128BitLane(temp4, 0x4);
            temp1 = Sse2.Xor(temp1, temp4);
            temp1 = Sse2.Xor(temp1, temp2);
            temp2 = Sse2.Shuffle(temp1.As<byte, int>(), 0xff).As<int, byte>();
            temp4 = Sse2.ShiftLeftLogical128BitLane(temp3, 0x4);
            temp3 = Sse2.Xor(temp3, temp4);
            temp3 = Sse2.Xor(temp3, temp2);
        }

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        private static void KeygenThree192(Vector128<byte>[] schedule, ref Vector128<byte> temp1, ref Vector128<byte> temp3,
            int i, byte control1, byte control2)
        {
            schedule[i] = temp1;
            schedule[i + 1] = temp3;
            var temp2 = Aes.KeygenAssist(temp3, control1);
            Keygen192Assist(ref temp1, ref temp2, ref temp3);
            schedule[i + 1] = Sse2.Shuffle(schedule[i + 1].As<byte, double>(), temp1.As<byte, double>(), 0)
                .As<double, byte>();
            schedule[i + 2] = Sse2.Shuffle(temp1.As<byte, double>(), temp3.As<byte, double>(), 1)
                .As<double, byte>();
            temp2 = Aes.KeygenAssist(temp3, control2);
            Keygen192Assist(ref temp1, ref temp2, ref temp3);
        }

        private static void GenerateWorkingKey192(Vector128<byte>[] schedule, byte* keyPointer)
        {
            var temp1 = Sse2.LoadVector128(keyPointer);
            // load 64bit
            var temp3 = Vector128.Create(*(ulong*) (keyPointer + 16), 0).As<ulong, byte>();
            KeygenThree192(schedule, ref temp1, ref temp3, 0, 0x01, 0x02);
            KeygenThree192(schedule, ref temp1, ref temp3, 3, 0x04, 0x08);
            KeygenThree192(schedule, ref temp1, ref temp3, 6, 0x10, 0x20);
            KeygenThree192(schedule, ref temp1, ref temp3, 9, 0x40, 0x80);
            schedule[12] = temp1;
        }

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        private static void Keygen256Assist1(ref Vector128<byte> temp1, ref Vector128<byte> temp2)
        {
            temp2 = Sse2.Shuffle(temp2.As<byte, int>(), 0xff).As<int, byte>();
            var temp4 = Sse2.ShiftLeftLogical128BitLane(temp1, 0x4);
            temp1 = Sse2.Xor(temp1, temp4);
            temp4 = Sse2.ShiftLeftLogical128BitLane(temp4, 0x4);
            temp1 = Sse2.Xor(temp1, temp4);
            temp4 = Sse2.ShiftLeftLogical128BitLane(temp4, 0x4);
            temp1 = Sse2.Xor(temp1, temp4);
            temp1 = Sse2.Xor(temp1, temp2);
        }

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        private static void Keygen256Assist2(ref Vector128<byte> temp1, ref Vector128<byte> temp3)
        {
            var temp4 = Aes.KeygenAssist(temp1, 0x0);
            var temp2 = Sse2.Shuffle(temp4.As<byte, int>(), 0xaa).As<int, byte>();
            temp4 = Sse2.ShiftLeftLogical128BitLane(temp3, 0x4);
            temp3 = Sse2.Xor(temp3, temp4);
            temp4 = Sse2.ShiftLeftLogical128BitLane(temp4, 0x4);
            temp3 = Sse2.Xor(temp3, temp4);
            temp4 = Sse2.ShiftLeftLogical128BitLane(temp4, 0x4);
            temp3 = Sse2.Xor(temp3, temp4);
            temp3 = Sse2.Xor(temp3, temp2);
        }

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        private static void KeygenTwice256(Vector128<byte>[] schedule, ref Vector128<byte> temp1, ref Vector128<byte> temp3,
            int i, byte control)
        {
            var temp2 = Aes.KeygenAssist(temp3, control);
            Keygen256Assist1(ref temp1, ref temp2);
            schedule[i] = temp1;
            Keygen256Assist2(ref temp1, ref temp3);
            schedule[i + 1] = temp3;
        }

        private static void GenerateWorkingKey256(Vector128<byte>[] schedule, byte* keyPointer)
        {
            var temp1 = Sse2.LoadVector128(keyPointer);
            var temp3 = Sse2.LoadVector128(keyPointer + 16);

            schedule[0] = temp1;
            schedule[1] = temp3;
            KeygenTwice256(schedule, ref temp1, ref temp3, 2, 0x01);
            KeygenTwice256(schedule, ref temp1, ref temp3, 4, 0x02);
            KeygenTwice256(schedule, ref temp1, ref temp3, 6, 0x04);
            KeygenTwice256(schedule, ref temp1, ref temp3, 8, 0x08);
            KeygenTwice256(schedule, ref temp1, ref temp3, 10, 0x10);
            KeygenTwice256(schedule, ref temp1, ref temp3, 12, 0x20);
            var temp2 = Aes.KeygenAssist(temp3, 0x40);
            Keygen256Assist1(ref temp1, ref temp2);
            schedule[14] = temp1;
        }

        protected Vector128<byte>[] GenerateWorkingKey(byte[] key, bool isEncryption)
        {
            fixed (byte* keyPointer = key)
            {
                Vector128<byte>[] keySchedule = new Vector128<byte>[Rounds + 1];
                switch (key.Length)
                {
                    case 16:
                        GenerateWorkingKey128(keySchedule, keyPointer);
                        break;

                    case 24:
                        GenerateWorkingKey192(keySchedule, keyPointer);
                        break;

                    case 32:
                        GenerateWorkingKey256(keySchedule, keyPointer);
                        break;

                    default:
                        throw new ArgumentException($"Invalid key length: {key.Length}");
                }

                // prepare decryption key
                if (!isEncryption)
                {
                    for (var i = 1; i < Rounds; i++)
                    {
                        keySchedule[i] = Aes.InverseMixColumns(keySchedule[i]);
                    }
                }

                return keySchedule;
            }
        }

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        protected Vector128<byte> EncryptAes(Vector128<byte> data)
        {
            if (EncryptionKey == null)
            {
                EncryptionKey = GenerateWorkingKey(Key, true);
            }

            data = Sse2.Xor(data, EncryptionKey[0]);
            // unrolled for performance
            data = Aes.Encrypt(data, EncryptionKey[1]);
            data = Aes.Encrypt(data, EncryptionKey[2]);
            data = Aes.Encrypt(data, EncryptionKey[3]);
            data = Aes.Encrypt(data, EncryptionKey[4]);
            data = Aes.Encrypt(data, EncryptionKey[5]);
            data = Aes.Encrypt(data, EncryptionKey[6]);
            data = Aes.Encrypt(data, EncryptionKey[7]);
            data = Aes.Encrypt(data, EncryptionKey[8]);
            data = Aes.Encrypt(data, EncryptionKey[9]);
            if (Rounds > 10)
            {
                data = Aes.Encrypt(data, EncryptionKey[10]);
                data = Aes.Encrypt(data, EncryptionKey[11]);
                if (Rounds > 12)
                {
                    data = Aes.Encrypt(data, EncryptionKey[12]);
                    data = Aes.Encrypt(data, EncryptionKey[13]);
                }
            }

            return Aes.EncryptLast(data, EncryptionKey[Rounds]);
        }

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        protected Vector128<byte> DecryptAes(Vector128<byte> data)
        {
            if (DecryptionKey == null)
            {
                DecryptionKey = GenerateWorkingKey(Key, false);
            }

            data = Sse2.Xor(data, DecryptionKey[Rounds]);
            // unrolled for performance
            if (Rounds > 12)
            {
                data = Aes.Decrypt(data, DecryptionKey[13]);
                data = Aes.Decrypt(data, DecryptionKey[12]);
            }

            if (Rounds > 10)
            {
                data = Aes.Decrypt(data, DecryptionKey[11]);
                data = Aes.Decrypt(data, DecryptionKey[10]);
            }

            data = Aes.Decrypt(data, DecryptionKey[9]);
            data = Aes.Decrypt(data, DecryptionKey[8]);
            data = Aes.Decrypt(data, DecryptionKey[7]);
            data = Aes.Decrypt(data, DecryptionKey[6]);
            data = Aes.Decrypt(data, DecryptionKey[5]);
            data = Aes.Decrypt(data, DecryptionKey[4]);
            data = Aes.Decrypt(data, DecryptionKey[3]);
            data = Aes.Decrypt(data, DecryptionKey[2]);
            data = Aes.Decrypt(data, DecryptionKey[1]);
            return Aes.DecryptLast(data, DecryptionKey[0]);
        }
    }
}
