using System;
using System.Security.Cryptography;
using System.Text;
using System.IO;

namespace DuLich.Services
{
    public class RSAService
    {
        private readonly RSA _privateKey;
        private readonly RSA _publicKey;

        public RSAService(string privateKeyPath, string publicKeyPath)
        {
            _privateKey = RSA.Create();
            _publicKey = RSA.Create();

            var privateKeyPem = File.ReadAllText(privateKeyPath);
            _privateKey.ImportFromPem(privateKeyPem.ToCharArray());

            var publicKeyPem = File.ReadAllText(publicKeyPath);
            _publicKey.ImportFromPem(publicKeyPem.ToCharArray());
        }

        public string Sign(string data)
        {
            var dataBytes = Encoding.UTF8.GetBytes(data);
            var signature = _privateKey.SignData(dataBytes, HashAlgorithmName.SHA256, RSASignaturePadding.Pkcs1);
            return Convert.ToBase64String(signature);
        }

        public bool Verify(string data, string signatureBase64)
        {
            if (string.IsNullOrWhiteSpace(signatureBase64))
            {
                return false;
            }

            var dataBytes = Encoding.UTF8.GetBytes(data);
            var signatureBytes = Convert.FromBase64String(signatureBase64);
            return _publicKey.VerifyData(dataBytes, signatureBytes, HashAlgorithmName.SHA256, RSASignaturePadding.Pkcs1);
        }

        // *** BẮT ĐẦU THAY ĐỔI ***
        // Thêm các phương thức public để JwtService có thể lấy key

        /// <summary>
        /// Trả về đối tượng RSA private key để ký (sign) JWT.
        /// </summary>
        public RSA GetPrivateKey()
        {
            return _privateKey;
        }

        /// <summary>
        /// Trả về đối tượng RSA public key để xác thực (validate) JWT.
        /// </summary>
        public RSA GetPublicKey()
        {
            return _publicKey;
        }
        // *** KẾT THÚC THAY ĐỔI ***
    }
}