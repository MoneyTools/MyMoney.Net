﻿using System;
using System.IO;
using System.Security.Cryptography;

namespace Walkabout.Utilities
{
    internal class HashedFile : IEquatable<HashedFile>
    {
        private byte[] hash;
        private readonly long fileLength;
        private int hashCode;
        private readonly string path;
        private static readonly HashAlgorithm hasher = SHA256.Create();

        public HashedFile(string path)
        {
            // start with a weak hash code for speed.
            this.fileLength = new FileInfo(path).Length;
            this.hashCode = (int)this.fileLength;
            this.path = path;
        }

        public long FileLength { get { return this.fileLength; } }

        private static byte[] hashBuffer = null;

        public void SetSha1PrefixHash(int prefixLength)
        {
            if (hashBuffer == null || hashBuffer.Length != prefixLength)
            {
                hashBuffer = new byte[prefixLength];
            }
            this.hashCode = 0;
            this.hash = this.ComputeSha256Hash(this.path);
            foreach (byte b in this.hash)
            {
                this.hashCode ^= ~b;
                this.hashCode <<= 1;
            }
        }

        private byte[] ComputeSha256Hash(string file)
        {
            using (Stream fs = new FileStream(this.path, FileMode.Open, FileAccess.Read, FileShare.None))
            {
                int len = fs.Read(hashBuffer, 0, hashBuffer.Length);
                return hasher.ComputeHash(hashBuffer, 0, len);
            }
        }

        public HashedFile(string path, int hashCode)
        {
            this.path = path;
            this.hashCode = hashCode;
        }

        public string Path { get { return this.path; } }

        public override int GetHashCode()
        {
            return this.hashCode;
        }

        public override bool Equals(object obj)
        {
            HashedFile other = obj as HashedFile;
            if (other == null)
            {
                return false;
            }
            return this.HashEquals(other);
        }

        public bool Equals(HashedFile other)
        {
            return this.HashEquals(other);
        }

        /// <summary>
        /// Find out if the two files have the same hashes.  If true this does not
        /// mean the files are identical, to check that call DeepEquals.
        /// </summary>
        /// <param name="other">The other hashed file.</param>
        /// <returns>True if the hashes are the same</returns>
        internal bool HashEquals(HashedFile other)
        {
            if (this.hashCode != other.hashCode)
            {
                return false;
            }

            if (this.hash == null)
            {
                return true;
            }

            for (int i = 0; i < this.hash.Length; i++)
            {
                if (this.hash[i] != other.hash[i])
                {
                    return false;
                }
            }
            return true;
        }

        /// <summary>
        /// Call this method if the HashEquals returns true to find out if the file is identical.
        /// </summary>
        /// <param name="other">The other hashed file</param>
        /// <returns>True if every byte in the two files is identical</returns>
        public bool DeepEquals(HashedFile other)
        {
            using (Stream fs = new FileStream(this.path, FileMode.Open, FileAccess.Read, FileShare.None))
            {
                using (Stream fs2 = new FileStream(other.path, FileMode.Open, FileAccess.Read, FileShare.None))
                {
                    return StreamEquals(fs, fs2);
                }
            }
        }

        private static readonly byte[] buffer1 = new byte[65536];
        private static readonly byte[] buffer2 = new byte[65536];

        private static bool StreamEquals(Stream s1, Stream s2)
        {
            while (true)
            {
                int read = s1.Read(buffer1, 0, buffer1.Length);
                int read2 = s2.Read(buffer2, 0, buffer2.Length);
                if (read != read2)
                {
                    return false;
                }
                if (read == 0)
                {
                    break;
                }
                for (int i = 0; i < read; i++)
                {
                    if (buffer1[i] != buffer2[i])
                    {
                        return false;
                    }
                }
            }
            return true;
        }

    }
}
