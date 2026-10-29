namespace PasadorScript.Services.Crypto
{
    public interface IEncriptacionHelper
    {
        string Desencriptar(string textoEncriptado);
        string Encriptar(string textoPlano);
    }
}