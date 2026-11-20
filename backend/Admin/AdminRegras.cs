using System.Text.RegularExpressions;
using Finops.Backend.Identidade;

namespace Finops.Backend.Admin;

/// <summary>Erro de regra de negócio com status HTTP (400 validação, 404, 409 conflito).</summary>
public sealed class ErroNegocioException(int status, string mensagem) : Exception(mensagem)
{
    public int Status { get; } = status;

    public static ErroNegocioException Validacao(string mensagem) => new(StatusCodes.Status400BadRequest, mensagem);
    public static ErroNegocioException Conflito(string mensagem) => new(StatusCodes.Status409Conflict, mensagem);
    public static ErroNegocioException NaoEncontrado(string mensagem) => new(StatusCodes.Status404NotFound, mensagem);
}

// ── Contratos da API ─────────────────────────────────────────────────────────

public sealed record ClienteEntrada(string? Nome, string? Apelido, string? Documento);
public sealed record AtivoEntrada(bool Ativo);
public sealed record UsuarioCriacao(string? Email, string? Nome, string? Perfil, long[]? ClienteIds);
public sealed record UsuarioAlteracao(string? Nome, string? Perfil, long[]? ClienteIds);

public sealed record ClienteSaida(long Id, string Nome, string Apelido, string? Documento, bool Ativo, DateTimeOffset CriadoEm, DateTimeOffset? AtualizadoEm);
public sealed record ClienteResumo(long Id, string Nome, string Apelido);
public sealed record UsuarioAfetado(long Id, string Nome, string Email);
public sealed record UsuarioSaida(long Id, string Email, string Nome, string Perfil, bool Ativo, DateTimeOffset? UltimoLoginEm, IReadOnlyList<ClienteResumo> Clientes);

/// <summary>Dados de cliente já validados e normalizados.</summary>
public sealed record ClienteValido(string Nome, string Apelido, string? Documento);

/// <summary>Regras de cliente (funções puras).</summary>
public static class ClienteRegras
{
    public const int NomeMax = 150;
    public const int ApelidoMax = 60;
    public const int DocumentoMax = 20;

    private static readonly Regex Apelido = new("^[a-z0-9-]+$", RegexOptions.Compiled | RegexOptions.CultureInvariant);
    private static readonly Regex SoDigitos = new("^[0-9]+$", RegexOptions.Compiled | RegexOptions.CultureInvariant);

    public static ClienteValido Validar(ClienteEntrada entrada)
    {
        var nome = entrada.Nome?.Trim();
        if (string.IsNullOrEmpty(nome))
            throw ErroNegocioException.Validacao("Informe o nome do cliente.");
        if (nome.Length > NomeMax)
            throw ErroNegocioException.Validacao($"O nome do cliente deve ter no máximo {NomeMax} caracteres.");

        var apelido = entrada.Apelido?.Trim();
        if (string.IsNullOrEmpty(apelido))
            throw ErroNegocioException.Validacao("Informe o apelido do cliente.");
        if (apelido.Length > ApelidoMax)
            throw ErroNegocioException.Validacao($"O apelido deve ter no máximo {ApelidoMax} caracteres.");
        if (!Apelido.IsMatch(apelido))
            throw ErroNegocioException.Validacao("O apelido aceita apenas letras minúsculas sem acento, números e hífen (ex.: ultragaz).");

        var documento = entrada.Documento?.Trim();
        if (string.IsNullOrEmpty(documento))
            documento = null;
        else if (documento.Length > DocumentoMax || !SoDigitos.IsMatch(documento))
            throw ErroNegocioException.Validacao($"O documento deve conter apenas dígitos (até {DocumentoMax}).");

        return new ClienteValido(nome, apelido, documento);
    }
}

/// <summary>Regras de usuário (funções puras).</summary>
public static class UsuarioRegras
{
    public const int EmailMax = 254;
    public const int NomeMax = 150;

    private static readonly Regex EmailFormato = new(@"^[^@\s]+@[^@\s]+\.[^@\s]+$", RegexOptions.Compiled | RegexOptions.CultureInvariant);

    public static string ValidarEmail(string? email)
    {
        var normalizado = IdentidadeEntra.NormalizarEmail(email);
        if (normalizado is null)
            throw ErroNegocioException.Validacao("Informe o e-mail.");
        if (normalizado.Length > EmailMax || !EmailFormato.IsMatch(normalizado))
            throw ErroNegocioException.Validacao("E-mail inválido.");
        return normalizado;
    }

    public static string ValidarNome(string? nome)
    {
        var n = nome?.Trim();
        if (string.IsNullOrEmpty(n))
            throw ErroNegocioException.Validacao("Informe o nome do usuário.");
        if (n.Length > NomeMax)
            throw ErroNegocioException.Validacao($"O nome deve ter no máximo {NomeMax} caracteres.");
        return n;
    }

    public static string ValidarPerfil(string? perfil)
    {
        var p = perfil?.Trim().ToUpperInvariant();
        if (!Perfis.Valido(p))
            throw ErroNegocioException.Validacao("Perfil inválido. Use ADMINISTRADOR, ANALISTA_FINOPS ou CLIENTE.");
        return p!;
    }

    /// <summary>
    /// Vínculos de cliente conforme o perfil. ADMINISTRADOR: sempre vazio (ignora o que vier).
    /// CLIENTE: ≥ 1. Todos os ids precisam existir e estar ativos (<paramref name="clientesAtivos"/>).
    /// </summary>
    public static long[] ValidarVinculos(string perfil, IEnumerable<long>? clienteIds, IReadOnlySet<long> clientesAtivos)
    {
        if (perfil == Perfis.Administrador)
            return [];

        var ids = (clienteIds ?? []).Distinct().ToArray();
        if (perfil == Perfis.Cliente && ids.Length == 0)
            throw ErroNegocioException.Validacao("Usuário com perfil Cliente precisa estar vinculado a pelo menos um cliente ativo.");

        if (ids.Any(id => !clientesAtivos.Contains(id)))
            throw ErroNegocioException.Validacao("Há cliente inexistente ou inativo na seleção.");

        return ids;
    }

    /// <summary>
    /// Proteções de perfil/ativação: o administrador não rebaixa nem desativa a si mesmo,
    /// e sempre resta ≥ 1 ADMINISTRADOR ativo.
    /// </summary>
    /// <param name="atorId">Quem faz a alteração.</param>
    /// <param name="alvoId">Usuário alterado.</param>
    /// <param name="perfilAtual">Perfil atual do alvo.</param>
    /// <param name="ativoAtual">Situação atual do alvo.</param>
    /// <param name="novoPerfil">Perfil depois da alteração.</param>
    /// <param name="novoAtivo">Situação depois da alteração.</param>
    /// <param name="administradoresAtivos">Quantidade atual de administradores ativos (incluindo o alvo, se for um).</param>
    public static void ValidarProtecoes(long atorId, long alvoId, string perfilAtual, bool ativoAtual,
        string novoPerfil, bool novoAtivo, int administradoresAtivos)
    {
        if (atorId == alvoId && !novoAtivo)
            throw ErroNegocioException.Conflito("Você não pode desativar o seu próprio usuário.");

        if (atorId == alvoId && perfilAtual == Perfis.Administrador && novoPerfil != Perfis.Administrador)
            throw ErroNegocioException.Conflito("Você não pode rebaixar o seu próprio perfil.");

        var eraAdminAtivo = perfilAtual == Perfis.Administrador && ativoAtual;
        var continuaAdminAtivo = novoPerfil == Perfis.Administrador && novoAtivo;
        if (eraAdminAtivo && !continuaAdminAtivo && administradoresAtivos <= 1)
            throw ErroNegocioException.Conflito("Deve restar pelo menos um administrador ativo.");
    }
}
