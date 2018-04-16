using System;
using System.Text;
using ArpanTECH; //RSA dll
using System.Security.Cryptography;

namespace RSACypher
{
    class RsaHandler
    {
        RSAxParameters.RSAxHashAlgorithm[] ha_types = new RSAxParameters.RSAxHashAlgorithm[]
        {
            RSAxParameters.RSAxHashAlgorithm.SHA1,//index 0
            RSAxParameters.RSAxHashAlgorithm.SHA256,//index 1
            RSAxParameters.RSAxHashAlgorithm.SHA512//index 2
        };

        int[] hLens = new int[] { 20, 32, 64 };

        private int ModulusSize;

        public int hashType { get; private set; }


        /// <summary>
        /// Use this to decrypt received messages.
        /// </summary>
        public string PrivateKey { get; private set; }

        /// <summary>
        /// Send this to the other side.
        /// </summary>
        public string PublicKey { get; private set; }

        /// <summary>
        /// Set this with the receiver Public Key and use it to encrypt messages.
        /// </summary>
        public string OuterPublicKey { get; set; } = null;

        /// <summary>
        /// Initialize the RsaHandles class.
        /// </summary>
        /// <param name="ModulusSize">KeySize. The Default value is 2048.</param>
        /// <param name="hashType">0 -> SHA1   1-> SHA256   2 -> SHA512</param>
        public RsaHandler(int ModulusSize = 2048, int hashType = 0)
        {
            this.ModulusSize = ModulusSize;

            this.hashType = hashType;

            generateKeyPair(this.ModulusSize);
        }

        private void generateKeyPair(int dwLen)
        {

            RSACryptoServiceProvider csp = new RSACryptoServiceProvider(dwLen);

            PrivateKey = csp.ToXmlString(true).Replace("><", ">\r\n<");//private key

            PublicKey = csp.ToXmlString(false).Replace("><", ">\r\n<");//public key

        }

        public string Encrypt(string text)
        {
            string ecnryptedText;

            if (ProcessMaxLength() < text.Length)
                throw new Exception("Max text length was exceded.");

            try
            {
                RSAx rsax = new RSAx(OuterPublicKey, ModulusSize);
                rsax.RSAxHashAlgorithm = ha_types[hashType];
                byte[] CT = rsax.Encrypt(Encoding.UTF8.GetBytes(text), false, true);

                ecnryptedText = Convert.ToBase64String(CT);

                return ecnryptedText;
            }
            catch (Exception ex)
            {
                throw ex;
            }
        }

        public string Decrypt(string text)
        {
            string decryptedText;

            try
            {
                RSAx rsax = new RSAx(PrivateKey, ModulusSize);
                rsax.RSAxHashAlgorithm = ha_types[hashType];
                byte[] PT = rsax.Decrypt(Convert.FromBase64String(text), true, true);
                decryptedText = Encoding.UTF8.GetString(PT);

                return decryptedText;
            }
            catch (System.Exception ex)
            {
                throw ex;
            }
        }


        /// <summary>
        /// Recalculates the max text size for encryption.
        /// </summary>
        private int ProcessMaxLength()
        {

            int hLen = hLens[hashType];
            int maxLength = 0;

            maxLength = (ModulusSize / 8) - 2 * hLen - 2;


            return maxLength;

        }

    }
}