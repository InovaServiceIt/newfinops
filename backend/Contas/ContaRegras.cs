using System.Text.RegularExpressions;
using Finops.Backend.Admin;

namespace Finops.Backend.Contas;

// ── Contratos da API ─────────────────────────────────────────────────────────

public sealed record ContaCriacao(long? ClienteId, string? Provedor, string? Nome, string? Identificador, string? Escopo, string? RegiaoPadrao);
public sealed record ContaAlteracao(string? Nome, string? Escopo, string? RegiaoPadrao);

public sealed record ContaSaida(
    long Id, long ClienteId, string ClienteNome, string Provedor, string Nome, string Identificador,
    string Escopo, string? RegiaoPadrao, bool Ativo, DateTimeOffset CriadoEm,
    /// <summary>Placeholder: preenchido no MD 0.3 (cofre de credenciais).</summary>
    string? CredencialStatus);

/// <summary>Dados de conta já validados e normalizados.</summary>
public sealed record ContaValida(long ClienteId, string Provedor, string Nome, string Identificador, string Escopo, string? RegiaoPadrao);
public sealed record ContaAlteracaoValida(string Nome, string Escopo, string? RegiaoPadrao);

/// <summary>Provedores e escopos aceitos (mesmos CHECKs da tabela cloud_conta).</summary>
public static class Provedores
{
    public const string Oci = "OCI";
    public const string Aws = "AWS";
    public const string Azure = "AZURE";
    public const string Gcp = "GCP";

    public static readonly IReadOnlyDictionary<string, string[]> EscoposPorProvedor = new Dictionary<string, string[]>
    {
        [Oci] = ["TENANCY"],
        [Aws] = ["CONTA", "ORGANIZACAO"],
        [Azure] = ["TENANT", "SUBSCRICAO"],
        [Gcp] = ["ORGANIZACAO", "PROJETO"],
    };

    public static bool Valido(string? provedor) => provedor is not null && EscoposPorProvedor.ContainsKey(provedor);
}

/// <summary>Validação de contas de cloud por provedor (funções puras; o backend é a autoridade).</summary>
public static class ContaRegras
{
    public const int NomeMax = 150;
    public const int IdentificadorMax = 200;
    public const int RegiaoMax = 50;

    private static readonly Regex OciTenancy = new(@"^ocid1\.tenancy\.oc1\.\.[a-z0-9]+$", RegexOptions.Compiled | RegexOptions.CultureInvariant);
    private static readonly Regex AwsAccountId = new(@"^[0-9]{12}$", RegexOptions.Compiled | RegexOptions.CultureInvariant);
    private static readonly Regex Guid = new(@"^[0-9a-fA-F]{8}-([0-9a-fA-F]{4}-){3}[0-9a-fA-F]{12}$", RegexOptions.Compiled | RegexOptions.CultureInvariant);
    private static readonly Regex SoDigitos = new(@"^[0-9]+$", RegexOptions.Compiled | RegexOptions.CultureInvariant);
    private static readonly Regex GcpProjeto = new(@"^[a-z][a-z0-9-]{4,28}[a-z0-9]$", RegexOptions.Compiled | RegexOptions.CultureInvariant);

    public static ContaValida ValidarCriacao(ContaCriacao e)
    {
        if (e.ClienteId is not > 0)
            throw ErroNegocioException.Validacao("Cliente: selecione o cliente da conta.");

        var provedor = e.Provedor?.Trim().ToUpperInvariant();
        if (!Provedores.Valido(provedor))
            throw ErroNegocioException.Validacao("Provedor: use OCI, AWS, AZURE ou GCP.");

        var nome = ValidarNome(e.Nome);
        var escopo = ValidarEscopo(provedor!, e.Escopo);
        var identificador = ValidarIdentificador(provedor!, escopo, e.Identificador);
        var regiao = ValidarRegiao(e.RegiaoPadrao);

        return new ContaValida(e.ClienteId.Value, provedor!, nome, identificador, escopo, regiao);
    }

    /// <summary>
    /// Alteração: provedor e identificador são imutáveis (vêm do registro atual). O novo escopo
    /// precisa ser válido para o provedor e compatível com o identificador já gravado.
    /// </summary>
    public static ContaAlteracaoValida ValidarAlteracao(string provedorAtual, string identificadorAtual, ContaAlteracao e)
    {
        var nome = ValidarNome(e.Nome);
        var escopo = ValidarEscopo(provedorAtual, e.Escopo);
        ValidarIdentificador(provedorAtual, escopo, identificadorAtual,
            "Escopo: incompatível com o identificador já cadastrado (o identificador não pode ser alterado).");
        var regiao = ValidarRegiao(e.RegiaoPadrao);
        return new ContaAlteracaoValida(nome, escopo, regiao);
    }

    public static string ValidarNome(string? nome)
    {
        var n = nome?.Trim();
        if (string.IsNullOrEmpty(n))
            throw ErroNegocioException.Validacao("Nome: informe um nome para a conta.");
        if (n.Length > NomeMax)
            throw ErroNegocioException.Validacao($"Nome: no máximo {NomeMax} caracteres.");
        return n;
    }

    public static string ValidarEscopo(string provedor, string? escopo)
    {
        var e = escopo?.Trim().ToUpperInvariant();
        var validos = Provedores.EscoposPorProvedor[provedor];
        if (e is null || !validos.Contains(e))
            throw ErroNegocioException.Validacao($"Escopo: para {provedor} use {string.Join(" ou ", validos)}.");
        return e;
    }

    /// <summary>Valida e normaliza o identificador (trim; GUID do Azure em minúsculas).</summary>
    public static string ValidarIdentificador(string provedor, string escopo, string? identificador, string? mensagemErro = null)
    {
        var id = identificador?.Trim() ?? "";
        if (id.Length == 0)
            throw ErroNegocioException.Validacao("Identificador: informe o identificador da conta.");
        if (id.Length > IdentificadorMax)
            throw ErroNegocioException.Validacao($"Identificador: no máximo {IdentificadorMax} caracteres.");

        var (valido, formato) = (provedor, escopo) switch
        {
            (Provedores.Oci, "TENANCY") => (OciTenancy.IsMatch(id), "OCID da tenancy (ocid1.tenancy.oc1..…)"),
            (Provedores.Aws, "CONTA") => (AwsAccountId.IsMatch(id), "account id com 12 dígitos"),
            (Provedores.Aws, "ORGANIZACAO") => (AwsAccountId.IsMatch(id), "account id da conta payer/management (12 dígitos)"),
            (Provedores.Azure, _) => (Guid.IsMatch(id), "GUID (ex.: 00000000-0000-0000-0000-000000000000)"),
            (Provedores.Gcp, "ORGANIZACAO") => (SoDigitos.IsMatch(id), "id numérico da organização"),
            (Provedores.Gcp, "PROJETO") => (GcpProjeto.IsMatch(id), "project id (6 a 30 caracteres: minúsculas, dígitos e hífen, começando por letra)"),
            _ => (false, "formato desconhecido"),
        };

        if (!valido)
            throw ErroNegocioException.Validacao(mensagemErro ?? $"Identificador: para {provedor}/{escopo} informe o {formato}.");

        return provedor == Provedores.Azure ? id.ToLowerInvariant() : id;
    }

    public static string? ValidarRegiao(string? regiao)
    {
        var r = regiao?.Trim();
        if (string.IsNullOrEmpty(r)) return null;
        if (r.Length > RegiaoMax)
            throw ErroNegocioException.Validacao($"Região padrão: no máximo {RegiaoMax} caracteres.");
        return r;
    }
}
