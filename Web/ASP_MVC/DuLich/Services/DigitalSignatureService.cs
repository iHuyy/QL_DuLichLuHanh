using System;
using System.Security.Cryptography;
using System.Text;
using System.IO;

namespace DuLich.Services
{
    public class DigitalSignatureService
    {
        private readonly RSA _privateKey;
        private readonly RSA _publicKey;

        public DigitalSignatureService(string privateKeyPath, string publicKeyPath)
        {
            _privateKey = RSA.Create();
            _publicKey = RSA.Create();

            // Load private key
            var privateKeyPem = File.ReadAllText(privateKeyPath);
            _privateKey.ImportFromPem(privateKeyPem.ToCharArray());

            // Load public key
            var publicKeyPem = File.ReadAllText(publicKeyPath);
            _publicKey.ImportFromPem(publicKeyPem.ToCharArray());
        }

        public string SignData(string data)
        {
            byte[] dataBytes = Encoding.UTF8.GetBytes(data);
            byte[] hashedData = SHA256.HashData(dataBytes);

            byte[] signature = _privateKey.SignHash(hashedData, HashAlgorithmName.SHA256, RSASignaturePadding.Pkcs1);
            return Convert.ToBase64String(signature);
        }

        public bool VerifySignature(string data, string signature, string publicKeyPem)
        {
            using (RSA rsa = RSA.Create())
            {
                rsa.ImportFromPem(publicKeyPem.ToCharArray());

                byte[] dataBytes = Encoding.UTF8.GetBytes(data);
                byte[] hashedData = SHA256.HashData(dataBytes);
                byte[] signatureBytes = Convert.FromBase64String(signature);

                return rsa.VerifyHash(hashedData, signatureBytes, HashAlgorithmName.SHA256, RSASignaturePadding.Pkcs1);
            }
        }
    }
}
