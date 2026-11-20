using System.Globalization;

namespace Finops.Backend.Cofre;

/// <summary>Cofre sem KEK utilizável (variáveis ausentes ou inválidas).</summary>
public sealed class CofreNaoConfiguradoException() : Exception("Cofre de credenciais não configurado.");

/// <summary>
/// Chaves mestras (KEK) lidas de CV_MASTER_KEY_V{n} (base64 de 32 bytes) e CV_MASTER_KEY_ATUAL (= n).
/// Várias versões podem estar carregadas ao mesmo tempo (rotação). Ausente/inválida → cofre desativado,
/// sem derrubar a aplicação e sem nunca logar o valor das chaves.
/// </summary>
public sealed class ChavesMestras
{
    public const string PrefixoVariavel = "CV_MASTER_KEY_V";
    public const string VariavelAtual = "CV_MASTER_KEY_ATUAL";

    private readonly IReadOnlyDictionary<int, byte[]> _chaves;

    public int? VersaoAtual { get; }
    public bool Configurado => VersaoAtual is not null;
    public IReadOnlyCollection<int> VersoesCarregadas => _chaves.Keys.ToArray();

    private ChavesMestras(IReadOnlyDictionary<int, byte[]> chaves, int? versaoAtual)
    {
        _chaves = chaves;
        VersaoAtual = versaoAtual;
    }

    /// <summary>Carrega da configuração (variáveis de ambiente entram no IConfiguration).</summary>
    public static ChavesMestras Carregar(IConfiguration configuracao, ILogger logger) =>
        Carregar(configuracao.AsEnumerable(), logger);

    /// <summary>Carrega de pares chave/valor (testável). Loga só nomes de variáveis e motivos, nunca valores.</summary>
    public static ChavesMestras Carregar(IEnumerable<KeyValuePair<string, string?>> variaveis, ILogger logger)
    {
        var chaves = new Dictionary<int, byte[]>();
        string? atualTexto = null;

        foreach (var (nome, valor) in variaveis)
        {
            if (string.Equals(nome, VariavelAtual, StringComparison.OrdinalIgnoreCase))
            {
                atualTexto = valor;
                continue;
            }
            if (!nome.StartsWith(PrefixoVariavel, StringComparison.OrdinalIgnoreCase))
                continue;
            if (!int.TryParse(nome.AsSpan(PrefixoVariavel.Length), NumberStyles.None, CultureInfo.InvariantCulture, out var versao) || versao <= 0)
                continue;

            var chave = DecodificarChave(valor);
            if (chave is null)
            {
                logger.LogError("{Variavel} inválida: precisa ser base64 de exatamente 32 bytes. Essa versão foi ignorada.", nome);
                continue;
            }
            chaves[versao] = chave;
        }

        if (!int.TryParse(atualTexto?.Trim(), NumberStyles.None, CultureInfo.InvariantCulture, out var atual))
        {
            logger.LogWarning("{Variavel} ausente ou inválida. Cofre de credenciais desativado.", VariavelAtual);
            return new ChavesMestras(chaves, null);
        }
        if (!chaves.ContainsKey(atual))
        {
            logger.LogWarning("{Variavel} aponta para a versão {Versao}, que não foi carregada. Cofre de credenciais desativado.", VariavelAtual, atual);
            return new ChavesMestras(chaves, null);
        }

        logger.LogInformation("Cofre de credenciais ativo: KEK atual v{Versao}; versões carregadas: {Versoes}.",
            atual, string.Join(",", chaves.Keys.Order()));
        return new ChavesMestras(chaves, atual);
    }

    /// <summary>KEK de uma versão (para decifrar DEKs antigas). Lança se não estiver carregada.</summary>
    public byte[] Kek(int versao) =>
        _chaves.TryGetValue(versao, out var chave) ? chave : throw new CofreNaoConfiguradoException();

    /// <summary>KEK atual (para cifrar DEKs novas).</summary>
    public (int Versao, byte[] Chave) KekAtual() =>
        VersaoAtual is { } v ? (v, _chaves[v]) : throw new CofreNaoConfiguradoException();

    private static byte[]? DecodificarChave(string? base64)
    {
        if (string.IsNullOrWhiteSpace(base64)) return null;
        try
        {
            var bytes = Convert.FromBase64String(base64.Trim());
            return bytes.Length == CofreCripto.TamanhoChave ? bytes : null;
        }
        catch (FormatException)
        {
            return null;
        }
    }
}
