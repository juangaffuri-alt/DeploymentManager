using System.Security.Cryptography;
using System.Text;

namespace PasadorScript.Services.Crypto
{
    public class EncriptacionHelper : IEncriptacionHelper
    {
        // Clave debe ser de 16, 24 o 32 bytes para AES
        private readonly string Clave; // 16 caracteres = 16 bytes
        // Vector de inicialización debe ser de 16 bytes
        private readonly string IV; // 16 caracteres = 16 bytes
        public EncriptacionHelper(IConfiguration configuration)
        {
            Clave = configuration["EncriptacionClaves:Clave"]!;
            IV = configuration["EncriptacionClaves:IV"]!;
        }

        public string Encriptar(string textoPlano)
        {
            if (string.IsNullOrEmpty(textoPlano))
                return textoPlano;

            using (Aes aesAlg = Aes.Create())
            {
                aesAlg.Key = Encoding.UTF8.GetBytes(Clave);
                aesAlg.IV = Encoding.UTF8.GetBytes(IV);

                ICryptoTransform encryptor = aesAlg.CreateEncryptor(aesAlg.Key, aesAlg.IV);

                using (MemoryStream msEncrypt = new MemoryStream())
                {
                    using (CryptoStream csEncrypt = new CryptoStream(msEncrypt, encryptor, CryptoStreamMode.Write))
                    {
                        using (StreamWriter swEncrypt = new StreamWriter(csEncrypt))
                        {
                            swEncrypt.Write(textoPlano);
                        }
                    }
                    return Convert.ToBase64String(msEncrypt.ToArray());
                }
            }
        }

        public string Desencriptar(string textoEncriptado)
        {
            if (string.IsNullOrEmpty(textoEncriptado))
                return textoEncriptado;

            try
            {
                byte[] cipherText = Convert.FromBase64String(textoEncriptado);

                using (Aes aesAlg = Aes.Create())
                {
                    aesAlg.Key = Encoding.UTF8.GetBytes(Clave);
                    aesAlg.IV = Encoding.UTF8.GetBytes(IV);

                    ICryptoTransform decryptor = aesAlg.CreateDecryptor(aesAlg.Key, aesAlg.IV);

                    using (MemoryStream msDecrypt = new MemoryStream(cipherText))
                    {
                        using (CryptoStream csDecrypt = new CryptoStream(msDecrypt, decryptor, CryptoStreamMode.Read))
                        {
                            using (StreamReader srDecrypt = new StreamReader(csDecrypt))
                            {
                                return srDecrypt.ReadToEnd();
                            }
                        }
                    }
                }
            }
            catch (FormatException)
            {
                throw new ArgumentException("El texto encriptado no tiene un formato válido");
            }
            catch (CryptographicException)
            {
                throw new ArgumentException("Error al desencriptar - verifica la clave y el IV");
            }
        }
    }
}
