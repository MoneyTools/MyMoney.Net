using System.IO;
using System.Security.Cryptography;
using System.Text;

namespace Walkabout.Data
{
    /// <summary>
    /// This class can encrypt and decrypt a given file.
    /// (Note: I've tried doing something in memory by chaining streams but kept running
    /// into the infamous CryptographicException : "Padding is invalid and cannot be removed").
    /// </summary>
    internal class Encryption
    {
        // These settings cannot change.
        private string saltValue = "Money Rocks";       // can be any string
        private string initVector = "*B5good!+027XYZ."; // must be 16 bytes
        private string hashAlgorithm = "SHA1";          // can be "MD5"
        private int passwordIterations = 2;             // can be any number        
        private int keySize = 256;                // can be 192 or 128

        /// <summary>
        /// Encrypts specified file using Rijndael symmetric key algorithm
        /// </summary>
        public void EncryptFile(string inputFile, string passPhrase, string fileName)
        {
            // Convert our plaintext into a byte array.
            byte[] initVectorBytes = Encoding.ASCII.GetBytes(this.initVector);

            var password = this.GetPasswordHelper(passPhrase);

            // Create uninitialized Rijndael encryption object.
            RijndaelManaged symmetricKey = new RijndaelManaged();

            // Use the password to generate pseudo-random bytes for the encryption
            // key. Specify the size of the key in bytes (instead of bits).
            byte[] keyBytes = password.GetBytes(this.keySize / 8);


            // It is reasonable to set encryption mode to Cipher Block Chaining
            // (CBC). Use default options for other symmetric key parameters.
            symmetricKey.Mode = CipherMode.CBC;

            // Generate encryptor from the existing key bytes and initialization 
            // vector. Key size will be defined based on the number of the key 
            // bytes.
            ICryptoTransform encryptor = symmetricKey.CreateEncryptor(
                                                             keyBytes,
                                                             initVectorBytes);

            // Define memory stream which will be used to hold encrypted data.
            FileStream fileStream = new FileStream(fileName, FileMode.Create, FileAccess.Write, FileShare.None);

            // Define cryptographic stream (always use Write mode for encryption).
            CryptoStream cryptoStream = new CryptoStream(fileStream,
                                                         encryptor,
                                                         CryptoStreamMode.Write);

            using (FileStream input = new FileStream(inputFile, FileMode.Open, FileAccess.Read, FileShare.None))
            {
                CopyStream(input, cryptoStream);
            }

            // Finish encrypting (this is critical!)
            cryptoStream.FlushFinalBlock();

            // Close both streams.
            fileStream.Close();
            cryptoStream.Close();
        }

        /// <summary>
        /// Decrypts specified file using Rijndael symmetric key algorithm.
        /// </summary>
        public void DecryptFile(string inputFile, string passPhrase, string fileName)
        {
            // Convert our plaintext into a byte array.
            byte[] initVectorBytes = Encoding.ASCII.GetBytes(this.initVector);

            var password = this.GetPasswordHelper(passPhrase);

            // Use the password to generate pseudo-random bytes for the encryption
            // key. Specify the size of the key in bytes (instead of bits).
            byte[] keyBytes = password.GetBytes(this.keySize / 8);

            // Create uninitialized Rijndael encryption object.
            RijndaelManaged symmetricKey = new RijndaelManaged();

            // It is reasonable to set encryption mode to Cipher Block Chaining
            // (CBC). Use default options for other symmetric key parameters.
            symmetricKey.Mode = CipherMode.CBC;

            // Generate decryptor from the existing key bytes and initialization 
            // vector. Key size will be defined based on the number of the key 
            // bytes.
            ICryptoTransform decryptor = symmetricKey.CreateDecryptor(
                                                             keyBytes,
                                                             initVectorBytes);



            // Define memory stream which will be used to hold encrypted data.
            FileStream fileStream = new FileStream(fileName, FileMode.Create, FileAccess.Write, FileShare.None);

            using (FileStream input = new FileStream(inputFile, FileMode.Open, FileAccess.Read, FileShare.None))
            {
                // Define cryptographic stream (always use Read mode for encryption).
                CryptoStream cryptoStream = new CryptoStream(input,
                                                              decryptor,
                                                              CryptoStreamMode.Read);

                CopyStream(cryptoStream, fileStream);

                cryptoStream.Close();
            }

            // Close both streams.
            fileStream.Close();
        }


        /// <summary>
        /// Creating a key of the right length is a bit tricky, which is why they
        /// provide the PasswordDeriveBytes class.
        /// </summary>
        private PasswordDeriveBytes GetPasswordHelper(string passPhrase)
        {
            // Convert strings into byte arrays.
            // Let us assume that strings only contain ASCII codes.
            // If strings include Unicode characters, use Unicode, UTF7, or UTF8 
            // encoding.
            byte[] saltValueBytes = Encoding.ASCII.GetBytes(this.saltValue);

            // First, we must create a password, from which the key will be derived.
            // This password will be generated from the specified passphrase and 
            // salt value. The password will be created using the specified hash 
            // algorithm. Password creation can be done in several iterations.
            PasswordDeriveBytes password = new PasswordDeriveBytes(
                                                            passPhrase,
                                                            saltValueBytes,
                                                            this.hashAlgorithm,
                                                            this.passwordIterations);
            return password;
        }


        /// <summary>
        /// Helper method to copy an input stream to an output stream.
        /// </summary>
        /// <param name="inputStream"></param>
        /// <param name="outputStream"></param>
        private static void CopyStream(Stream inputStream, Stream outputStream)
        {
            int size = 64000;
            byte[] buffer = new byte[size];
            int len = 0;
            while ((len = inputStream.Read(buffer, 0, size)) > 0)
            {
                // Start encrypting.
                outputStream.Write(buffer, 0, len);
            }
        }

    }
}
