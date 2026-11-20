using System.Security.Cryptography;
using System.Text;

namespace Finops.Backend.Cofre;

/// <summary>Falha de cifragem/decifragem. A mensagem é sempre genérica: nunca contém dados.</summary>
public sealed class CofreCriptoException(string mensagem) : Exception(mensagem);

/// <summary>
/// AES-256-GCM com o formato binário v1 (contrato com o coletor Python):
/// <code>[0] = 0x01 | [1..12] = nonce (12) | [13..n-17] = texto cifrado | [n-16..n-1] = tag (16)</code>
/// Equivale ao <c>AESGCM(chave).decrypt(nonce, ct || tag, aad)</c> do pacote cryptography do Python.
/// </summary>
public static class CofreCripto
{
    public const byte VersaoFormato = 0x01;
    public const int TamanhoChave = 32;
    public const int TamanhoNonce = 12;
    public const int TamanhoTag = 16;
    public const int TamanhoCabecalho = 1 + TamanhoNonce;

    /// <summary>AAD da DEK: amarra a DEK ao cliente e à versão da KEK.</summary>
    public static byte[] AadDek(long clienteId, int versaoKek) =>
        Encoding.UTF8.GetBytes($"cv:dek:v1:{clienteId}:{versaoKek}");

    /// <summary>AAD da credencial: amarra o segredo à conta e ao tipo.</summary>
    public static byte[] AadCredencial(long cloudContaId, string tipo) =>
        Encoding.UTF8.GetBytes($"cv:cred:v1:{cloudContaId}:{tipo}");

    /// <summary>Cifra com nonce aleatório novo (RandomNumberGenerator) a cada chamada.</summary>
    public static byte[] Cifrar(ReadOnlySpan<byte> chave, ReadOnlySpan<byte> textoClaro, ReadOnlySpan<byte> aad)
    {
        Span<byte> nonce = stackalloc byte[TamanhoNonce];
        RandomNumberGenerator.Fill(nonce);
        return CifrarComNonce(chave, nonce, textoClaro, aad);
    }

    /// <summary>Cifra com nonce informado. SÓ para vetores de teste determinísticos; produção usa <see cref="Cifrar"/>.</summary>
    internal static byte[] CifrarComNonce(ReadOnlySpan<byte> chave, ReadOnlySpan<byte> nonce, ReadOnlySpan<byte> textoClaro, ReadOnlySpan<byte> aad)
    {
        ValidarChave(chave);
        if (nonce.Length != TamanhoNonce)
            throw new CofreCriptoException("Nonce inválido.");

        var blob = new byte[TamanhoCabecalho + textoClaro.Length + TamanhoTag];
        blob[0] = VersaoFormato;
        nonce.CopyTo(blob.AsSpan(1, TamanhoNonce));

        using var aes = new AesGcm(chave, TamanhoTag);
        aes.Encrypt(
            nonce,
            textoClaro,
            blob.AsSpan(TamanhoCabecalho, textoClaro.Length),
            blob.AsSpan(TamanhoCabecalho + textoClaro.Length, TamanhoTag),
            aad);
        return blob;
    }

    /// <summary>Decifra um blob v1. Falha (sem detalhes) se versão, tamanho, chave, AAD ou tag não conferirem.</summary>
    public static byte[] Decifrar(ReadOnlySpan<byte> chave, ReadOnlySpan<byte> blob, ReadOnlySpan<byte> aad)
    {
        ValidarChave(chave);
        if (blob.Length < TamanhoCabecalho + TamanhoTag)
            throw new CofreCriptoException("Conteúdo cifrado inválido.");
        if (blob[0] != VersaoFormato)
            throw new CofreCriptoException("Versão do formato cifrado não suportada.");

        var tamanhoTexto = blob.Length - TamanhoCabecalho - TamanhoTag;
        var textoClaro = new byte[tamanhoTexto];
        try
        {
            using var aes = new AesGcm(chave, TamanhoTag);
            aes.Decrypt(
                blob.Slice(1, TamanhoNonce),
                blob.Slice(TamanhoCabecalho, tamanhoTexto),
                blob.Slice(TamanhoCabecalho + tamanhoTexto, TamanhoTag),
                textoClaro,
                aad);
            return textoClaro;
        }
        catch (CryptographicException)
        {
            CryptographicOperations.ZeroMemory(textoClaro);
            throw new CofreCriptoException("Falha na autenticação do conteúdo cifrado.");
        }
    }

    private static void ValidarChave(ReadOnlySpan<byte> chave)
    {
        if (chave.Length != TamanhoChave)
            throw new CofreCriptoException("Chave com tamanho inválido.");
    }
}
