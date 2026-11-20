using System.Text;
using System.Text.Json;
using System.Text.RegularExpressions;
using Finops.Backend.Admin;
using Finops.Backend.Contas;

namespace Finops.Backend.Cofre;

/// <summary>Tipos de credencial (mesmos CHECKs da tabela credencial).</summary>
public static class TiposCredencial
{
    public const string OciApiKey = "OCI_API_KEY";
    public const string AwsAccessKey = "AWS_ACCESS_KEY";
    public const string AwsAssumeRole = "AWS_ASSUME_ROLE";
    public const string AzureServicePrincipal = "AZURE_SERVICE_PRINCIPAL";
    public const string GcpServiceAccount = "GCP_SERVICE_ACCOUNT";

    public static readonly IReadOnlyDictionary<string, string> ProvedorDoTipo = new Dictionary<string, string>
    {
        [OciApiKey] = Provedores.Oci,
        [AwsAccessKey] = Provedores.Aws,
        [AwsAssumeRole] = Provedores.Aws,
        [AzureServicePrincipal] = Provedores.Azure,
        [GcpServiceAccount] = Provedores.Gcp,
    };
}

/// <summary>Credencial validada: JSON UTF-8 pronto para cifrar + dica pública (nunca o segredo).</summary>
public sealed class CredencialValida(string tipo, byte[] conteudoJson, string dica)
{
    public string Tipo { get; } = tipo;
    /// <summary>Texto claro (JSON UTF-8). Quem usa deve zerar depois (CryptographicOperations.ZeroMemory).</summary>
    public byte[] ConteudoJson { get; } = conteudoJson;
    public string Dica { get; } = dica;
}

/// <summary>
/// Validação dos campos por tipo de credencial (funções puras). As mensagens de erro citam só o
/// NOME do campo e o formato esperado; nunca o valor recebido.
/// </summary>
public static class CredencialRegras
{
    public const int TamanhoMaximoBytes = 16 * 1024;
    public const int DicaMax = 40;

    private const RegexOptions Opcoes = RegexOptions.Compiled | RegexOptions.CultureInvariant;
    private static readonly Regex OcidUsuario = new(@"^ocid1\.user\.oc1\.\.[a-z0-9]+$", Opcoes);
    private static readonly Regex OcidTenancy = new(@"^ocid1\.tenancy\.oc1\.\.[a-z0-9]+$", Opcoes);
    private static readonly Regex Fingerprint = new(@"^([0-9a-f]{2}:){15}[0-9a-f]{2}$", Opcoes);
    private static readonly Regex RegiaoOci = new(@"^[a-z]{2,}-[a-z]+-[0-9]+$", Opcoes);
    private static readonly Regex AwsAccessKeyId = new(@"^(AKIA|ASIA)[A-Z0-9]{16}$", Opcoes);
    private static readonly Regex AwsSecret = new(@"^[A-Za-z0-9/+=]{40}$", Opcoes);
    private static readonly Regex AwsRoleArn = new(@"^arn:aws:iam::[0-9]{12}:role/[A-Za-z0-9+=,.@_/-]{1,512}$", Opcoes);
    private static readonly Regex AwsExternalId = new(@"^[A-Za-z0-9+=,.@:/-]{2,1224}$", Opcoes);
    private static readonly Regex Guid = new(@"^[0-9a-fA-F]{8}-([0-9a-fA-F]{4}-){3}[0-9a-fA-F]{12}$", Opcoes);

    /// <summary>Campos aceitos por tipo (obrigatórios e opcionais).</summary>
    public static readonly IReadOnlyDictionary<string, (string[] Obrigatorios, string[] Opcionais)> Campos =
        new Dictionary<string, (string[], string[])>
        {
            [TiposCredencial.OciApiKey] = (["user_ocid", "tenancy_ocid", "fingerprint", "region", "private_key_pem"], ["passphrase"]),
            [TiposCredencial.AwsAccessKey] = (["access_key_id", "secret_access_key"], []),
            [TiposCredencial.AwsAssumeRole] = (["access_key_id", "secret_access_key", "role_arn"], ["external_id"]),
            [TiposCredencial.AzureServicePrincipal] = (["tenant_id", "client_id", "client_secret"], []),
            [TiposCredencial.GcpServiceAccount] = (["service_account_json"], []),
        };

    /// <param name="tipo">Tipo informado.</param>
    /// <param name="provedorConta">Provedor da conta (cloud_conta.provedor).</param>
    /// <param name="identificadorConta">Identificador da conta (para conferir o tenancy da OCI).</param>
    /// <param name="campos">Campos informados (valores secretos: nunca entram em mensagens).</param>
    public static CredencialValida Validar(string? tipo, string provedorConta, string identificadorConta, IReadOnlyDictionary<string, string?>? campos)
    {
        var t = tipo?.Trim().ToUpperInvariant();
        if (t is null || !TiposCredencial.ProvedorDoTipo.TryGetValue(t, out var provedorTipo))
            throw ErroNegocioException.Validacao("Tipo: tipo de credencial inválido.");
        if (provedorTipo != provedorConta)
            throw ErroNegocioException.Validacao($"Tipo: {t} não é compatível com uma conta {provedorConta}.");

        campos ??= new Dictionary<string, string?>();
        var (obrigatorios, opcionais) = Campos[t];
        var permitidos = obrigatorios.Concat(opcionais).ToHashSet(StringComparer.Ordinal);

        var desconhecido = campos.Keys.FirstOrDefault(k => !permitidos.Contains(k));
        if (desconhecido is not null)
            throw ErroNegocioException.Validacao($"Campos: campo não permitido para {t}.");

        // Normaliza: trim (exceto passphrase) e descarta opcionais vazios.
        var valores = new Dictionary<string, string>(StringComparer.Ordinal);
        foreach (var nome in permitidos)
        {
            campos.TryGetValue(nome, out var bruto);
            var valor = nome == "passphrase" ? bruto : bruto?.Trim();
            if (string.IsNullOrEmpty(valor))
            {
                if (obrigatorios.Contains(nome))
                    throw ErroNegocioException.Validacao($"{nome}: campo obrigatório.");
                continue;
            }
            valores[nome] = valor;
        }

        var dica = t switch
        {
            TiposCredencial.OciApiKey => ValidarOci(valores, identificadorConta),
            TiposCredencial.AwsAccessKey => ValidarAwsChave(valores),
            TiposCredencial.AwsAssumeRole => ValidarAwsRole(valores),
            TiposCredencial.AzureServicePrincipal => ValidarAzure(valores),
            TiposCredencial.GcpServiceAccount => ValidarGcp(valores),
            _ => throw ErroNegocioException.Validacao("Tipo: tipo de credencial inválido."),
        };

        var json = SerializarOrdenado(obrigatorios.Concat(opcionais), valores);
        if (json.Length > TamanhoMaximoBytes)
        {
            Array.Clear(json);
            throw ErroNegocioException.Validacao("Campos: o conteúdo da credencial excede 16 KB.");
        }

        return new CredencialValida(t, json, Limitar(dica));
    }

    // ── Por tipo (retornam a dica) ───────────────────────────────────────────

    private static string ValidarOci(Dictionary<string, string> v, string identificadorConta)
    {
        Exigir(OcidUsuario.IsMatch(v["user_ocid"]), "user_ocid: informe o OCID do usuário (ocid1.user.oc1..…).");
        Exigir(OcidTenancy.IsMatch(v["tenancy_ocid"]), "tenancy_ocid: informe o OCID da tenancy (ocid1.tenancy.oc1..…).");
        Exigir(v["tenancy_ocid"] == identificadorConta, "tenancy_ocid: precisa ser igual ao identificador da conta OCI.");
        v["fingerprint"] = v["fingerprint"].ToLowerInvariant();
        Exigir(Fingerprint.IsMatch(v["fingerprint"]), "fingerprint: formato esperado xx:xx:…:xx (16 pares hexadecimais).");
        Exigir(RegiaoOci.IsMatch(v["region"]), "region: formato esperado como sa-saopaulo-1.");
        Exigir(v["private_key_pem"].StartsWith("-----BEGIN", StringComparison.Ordinal), "private_key_pem: a chave privada deve estar em PEM (começa com -----BEGIN).");
        return "…" + Ultimos(v["fingerprint"], 5);
    }

    private static string ValidarAwsChave(Dictionary<string, string> v)
    {
        ValidarAwsBase(v);
        return "…" + Ultimos(v["access_key_id"], 4);
    }

    private static string ValidarAwsRole(Dictionary<string, string> v)
    {
        ValidarAwsBase(v);
        Exigir(AwsRoleArn.IsMatch(v["role_arn"]), "role_arn: formato esperado arn:aws:iam::<12 dígitos>:role/<nome>.");
        if (v.TryGetValue("external_id", out var externalId))
            Exigir(AwsExternalId.IsMatch(externalId), "external_id: 2 a 1224 caracteres (letras, dígitos e +=,.@:/-).");
        var nomeRole = v["role_arn"][(v["role_arn"].LastIndexOf('/') + 1)..];
        return $"…{Ultimos(v["access_key_id"], 4)} · {nomeRole}";
    }

    private static void ValidarAwsBase(Dictionary<string, string> v)
    {
        Exigir(AwsAccessKeyId.IsMatch(v["access_key_id"]), "access_key_id: formato esperado AKIA/ASIA + 16 caracteres.");
        Exigir(AwsSecret.IsMatch(v["secret_access_key"]), "secret_access_key: deve ter 40 caracteres (letras, dígitos, / + =).");
    }

    private static string ValidarAzure(Dictionary<string, string> v)
    {
        Exigir(Guid.IsMatch(v["tenant_id"]), "tenant_id: informe um GUID.");
        Exigir(Guid.IsMatch(v["client_id"]), "client_id: informe um GUID.");
        Exigir(v["client_secret"].Length <= 1024, "client_secret: tamanho inválido.");
        v["tenant_id"] = v["tenant_id"].ToLowerInvariant();
        v["client_id"] = v["client_id"].ToLowerInvariant();
        return "…" + Ultimos(v["client_id"], 4);
    }

    private static string ValidarGcp(Dictionary<string, string> v)
    {
        string? tipo = null, clientEmail = null, privateKey = null;
        try
        {
            using var doc = JsonDocument.Parse(v["service_account_json"]);
            if (doc.RootElement.ValueKind != JsonValueKind.Object)
                throw ErroNegocioException.Validacao("service_account_json: o conteúdo deve ser o objeto JSON da service account.");
            tipo = Texto(doc.RootElement, "type");
            clientEmail = Texto(doc.RootElement, "client_email");
            privateKey = Texto(doc.RootElement, "private_key");
        }
        catch (JsonException)
        {
            // Não repassa a mensagem do parser (pode conter trechos do conteúdo).
            throw ErroNegocioException.Validacao("service_account_json: JSON inválido.");
        }

        Exigir(tipo == "service_account", "service_account_json: o campo \"type\" deve ser \"service_account\".");
        Exigir(!string.IsNullOrWhiteSpace(clientEmail) && clientEmail.Contains('@'), "service_account_json: \"client_email\" ausente ou inválido.");
        Exigir(!string.IsNullOrWhiteSpace(privateKey), "service_account_json: \"private_key\" ausente.");
        return clientEmail!;
    }

    // ── Auxiliares ───────────────────────────────────────────────────────────

    private static void Exigir(bool condicao, string mensagem)
    {
        if (!condicao) throw ErroNegocioException.Validacao(mensagem);
    }

    private static string? Texto(JsonElement e, string propriedade) =>
        e.TryGetProperty(propriedade, out var p) && p.ValueKind == JsonValueKind.String ? p.GetString() : null;

    private static string Ultimos(string s, int n) => s.Length <= n ? s : s[^n..];

    private static string Limitar(string dica) => dica.Length <= DicaMax ? dica : dica[..(DicaMax - 1)] + "…";

    private static byte[] SerializarOrdenado(IEnumerable<string> ordem, Dictionary<string, string> valores)
    {
        using var buffer = new MemoryStream();
        using (var w = new Utf8JsonWriter(buffer))
        {
            w.WriteStartObject();
            foreach (var nome in ordem)
                if (valores.TryGetValue(nome, out var valor))
                    w.WriteString(nome, valor);
            w.WriteEndObject();
        }
        var json = buffer.ToArray();
        Array.Clear(buffer.GetBuffer());   // não deixa cópia do texto claro no buffer interno
        return json;
    }
}
