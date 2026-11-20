using System.Diagnostics;
using Microsoft.Extensions.Caching.Memory;
using Npgsql;

namespace Finops.Backend.VisaoGeral;

/// <summary>Banco não configurado (ConnectionStrings:FinopsDb ausente).</summary>
public sealed class BancoNaoConfiguradoException() : Exception("Banco de dados não configurado.");

/// <summary>A tabela de resumo está sendo recriada pelo legado (42P01 persistente).</summary>
public sealed class DadosEmAtualizacaoException(Exception inner)
    : Exception("Dados em atualização, tente novamente em instantes.", inner);

/// <summary>
/// Orquestra a Visão Geral: datas de referência (America/Sao_Paulo), cache de 5 min,
/// 1 retry quando a tabela de resumo está sendo recriada (42P01) e log de tempo.
/// </summary>
public sealed class VisaoGeralServico
{
    public static readonly TimeSpan DuracaoCache = TimeSpan.FromMinutes(5);
    public static readonly TimeSpan EsperaRetry = TimeSpan.FromSeconds(3);

    private readonly VisaoGeralRepositorio _repo;
    private readonly IMemoryCache _cache;
    private readonly TimeProvider _relogio;
    private readonly ILogger<VisaoGeralServico> _logger;

    public VisaoGeralServico(VisaoGeralRepositorio repo, IMemoryCache cache, TimeProvider relogio, ILogger<VisaoGeralServico> logger)
    {
        _repo = repo;
        _cache = cache;
        _relogio = relogio;
        _logger = logger;
    }

    public DatasReferencia DatasDeHoje() => new(DatasReferencia.HojeEmSaoPaulo(_relogio));

    public Task<OpcoesVisaoGeral> OpcoesAsync(int? provedor, CancellationToken ct) =>
        ComCacheAsync($"vg-opcoes|{DatasDeHoje().Hoje:yyyy-MM-dd}|{provedor}", "opcoes",
            () => _repo.LerOpcoesAsync(provedor, ct), ct);

    public Task<VisaoGeralResposta> VisaoGeralAsync(FiltrosVisaoGeral filtros, CancellationToken ct)
    {
        var datas = DatasDeHoje();
        return ComCacheAsync(filtros.ChaveCache(datas.Hoje), "visao-geral", async () =>
        {
            var brutos = await _repo.LerDadosAsync(filtros, datas, ct);
            return VisaoGeralCalculos.Montar(filtros, datas, brutos);
        }, ct);
    }

    private async Task<T> ComCacheAsync<T>(string chave, string operacao, Func<Task<T>> carregar, CancellationToken ct)
    {
        if (_cache.TryGetValue(chave, out T? emCache) && emCache is not null)
            return emCache;

        if (!_repo.Configurado)
            throw new BancoNaoConfiguradoException();

        var sw = Stopwatch.StartNew();
        try
        {
            var resultado = await ComRetryAsync(carregar, ct);
            _cache.Set(chave, resultado, DuracaoCache);
            return resultado;
        }
        finally
        {
            _logger.LogInformation("Visão Geral [{Operacao}] carregada do banco em {ElapsedMs} ms.", operacao, sw.ElapsedMilliseconds);
        }
    }

    /// <summary>A tabela de resumo é recriada (DROP/CREATE) a cada 2 h: em 42P01, espera ~3 s e tenta 1 vez.</summary>
    private async Task<T> ComRetryAsync<T>(Func<Task<T>> carregar, CancellationToken ct)
    {
        try
        {
            return await carregar();
        }
        catch (PostgresException ex) when (ex.SqlState == PostgresErrorCodes.UndefinedTable)
        {
            _logger.LogWarning("Tabela não encontrada (42P01), provavelmente em recriação. Nova tentativa em {Segundos} s.", EsperaRetry.TotalSeconds);
            await Task.Delay(EsperaRetry, ct);
            try
            {
                return await carregar();
            }
            catch (PostgresException ex2) when (ex2.SqlState == PostgresErrorCodes.UndefinedTable)
            {
                throw new DadosEmAtualizacaoException(ex2);
            }
        }
    }
}
