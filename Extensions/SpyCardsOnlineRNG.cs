using System;
using System.Collections.Generic;
using System.Security.Cryptography;
using System.Text;
using UnityEngine;

namespace SpeedrunPractice.Extensions
{
    public class SpyCardsOnlineRNG<THash> : MonoBehaviour where THash : HashAlgorithm, new()
    {
        public string Seed { get; protected set; }
        public int Count { get; protected set; }
        protected byte[] seedBuf;
        protected byte[] updateBuf;
        protected byte[] buf;
        protected int index;
        protected readonly THash hash;

        public SpyCardsOnlineRNG()
        {
            this.hash = new THash();
        }

        public void SetSeed(string seed)
        {
            this.Seed = seed;
            this.seedBuf = new UTF8Encoding(false, true).GetBytes(seed);
            this.buf = this.hash.ComputeHash(this.seedBuf);
            this.updateBuf = new byte[this.buf.Length + this.seedBuf.Length];
            Array.Copy(this.seedBuf, 0, this.updateBuf, this.buf.Length, this.seedBuf.Length);
        }

        private byte next()
        {
            if (this.index >= this.buf.Length)
            {
                Array.Copy(this.buf, this.updateBuf, this.buf.Length);
                this.buf = this.hash.ComputeHash(this.updateBuf);
                this.index = 0;
            }

            byte n = this.buf[this.index];
            this.index++;
            this.Count++;
            return n;
        }
        public double CheapFloat()
        {
            return this.next() / 256.0;
        }
        public double Float()
        {
            double a = this.next();
            double b = this.next();
            double c = this.next();
            double d = this.next();

            return (((a * 256 + b) * 256 + c) * 256 + d) / 4294967296.0;
        }
        public double Int(double min, double max)
        {
            double diff = max - min;
            if (diff <= 0)
            {
                throw new ArgumentOutOfRangeException(nameof(max), "invalid random range");
            }
            if (diff >= 256)
            {
                return Math.Floor(diff * this.Float()) + min;
            }

            double maxValue = diff;
            while (maxValue + diff <= 256)
            {
                maxValue += diff;
            }

            double value = this.next();
            while (value >= maxValue)
            {
                value = this.next();
            }

            return value % diff + min;
        }

        // crockford base 32 alphabet
        private const string alphabet = "0123456789ABCDEFGHJKMNPQRSTVWXYZ";
        public static string RandomSeed()
        {
            RandomNumberGenerator rng = RandomNumberGenerator.Create();
            byte[] buf = new byte[10];
            rng.GetBytes(buf);

            char[] encoded = new char[16];
            // Base32 follows this pattern every 5 bytes:
            // 00000111
            // 11222223
            // 33334444
            // 45555566
            // 66677777
            for (int i = 0, j = 0; i < buf.Length; i += 5, j += 8)
            {
                encoded[j+0] = alphabet[buf[i + 0] >> 3];
                encoded[j+1] = alphabet[((buf[i + 0] & 7) << 2) | (buf[i + 1] >> 6)];
                encoded[j+2] = alphabet[(buf[i + 1] >> 1) & 31];
                encoded[j+3] = alphabet[((buf[i + 1] & 1) << 4) | (buf[i + 2] >> 4)];
                encoded[j+4] = alphabet[((buf[i + 2] & 15) << 1) | (buf[i + 3] >> 7)];
                encoded[j+5] = alphabet[(buf[i + 3] >> 2) & 31];
                encoded[j+6] = alphabet[((buf[i+3] & 3) << 3) | (buf[i + 4] >> 5)];
                encoded[j+7] = alphabet[buf[i+4] & 31];
            }

            return new string(encoded);
        }
    }
    public class RNG512 : SpyCardsOnlineRNG<SHA512Managed>
    {
        private struct SavedState
        {
            public int index;
            public int count;
            public byte[] buf;
        }

        private readonly SortedList<int, SavedState> states = new SortedList<int, SavedState>();

        public void SaveState(int index)
        {
            this.states[index] = new SavedState
            {
                index = this.index,
                count = this.Count,
                buf = this.buf,
            };
        }
        public void LoadState(int index)
        {
            var state = this.states[index];
            this.index = state.index;
            this.Count = state.count;
            this.buf = state.buf;
        }
        public bool HasState(int index)
        {
            return this.states.ContainsKey(index);
        }
        public void LoadStateIfExists(int index)
        {
            if (HasState(index))
            {
                LoadState(index);
            }
        }
        public static float RangeFloat(float min, float max, RNG512 rng)
        {
            return min + (float)rng.Float() * (max - min);
        }
        public static int RangeInt(int min, int max, RNG512 rng)
        {
            return (int)rng.Int(min, max);
        }
    }
}
