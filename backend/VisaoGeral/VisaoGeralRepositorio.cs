using System.Data;
using Finops.Backend.Infrastructure;
using Npgsql;
using NpgsqlTypes;

namespace Finops.Backend.VisaoGeral;

/// <summary>Leitura dos dados da Visão Geral. Uma conexão e uma transação read-only por chamada.</summary>
public sealed class VisaoGeralRepositorio
{
    private readonly FinopsDb _db;

    public VisaoGeralRepositorio(FinopsDb db) => _db = db;

    public bool Configurado => _db.IsConfigured;

    public async Task<IReadOnlyList<ProvedorOpcao>> ListarProvedoresAsync(CancellationToken ct)
    {
        await using var conn = await _db.OpenConnectionAsync(ct);
        await using var tx = await conn.BeginTransactionAsync(IsolationLevel.RepeatableRead, ct);
        var provedores = await LerProvedoresAsync(conn, tx, ct);
        await tx.CommitAsync(ct);
        return provedores;
    }

    public async Task<OpcoesVisaoGeral> LerOpcoesAsync(int? provedor, CancellationToken ct)
    {
        await using var conn = await _db.OpenConnectionAsync(ct);
        await using var tx = await conn.BeginTransactionAsync(IsolationLevel.RepeatableRead, ct);

        var provedores = await LerProvedoresAsync(conn, tx, ct);
        var selecionado = provedor is { } p && provedores.Any(x => x.Id == p) ? p : provedores.FirstOrDefault()?.Id;
        if (selecionado is null)
        {
            await tx.CommitAsync(ct);
            return new OpcoesVisaoGeral(provedores, null, [], [], [], [], null);
        }

        var anos = await LerListaAsync(conn, tx, VisaoGeralSql.Anos, selecionado.Value, r => r.GetInt32(0), ct);
        var servicos = await LerListaAsync(conn, tx, VisaoGeralSql.Servicos, selecionado.Value, r => r.GetString(0), ct);
        var skus = await LerListaAsync(conn, tx, VisaoGeralSql.Skus, selecionado.Value, r => r.GetString(0), ct);
        var regioes = await LerListaAsync(conn, tx, VisaoGeralSql.Regioes, selecionado.Value, r => r.GetString(0), ct);
        var fator = await LerFatorAsync(conn, tx, selecionado.Value, ct);

        await tx.CommitAsync(ct);
        return new OpcoesVisaoGeral(provedores, selecionado, anos, servicos, skus, regioes, fator);
    }

    public async Task<DadosBrutosVisaoGeral> LerDadosAsync(FiltrosVisaoGeral f, DatasReferencia datas, CancellationToken ct)
    {
        await using var conn = await _db.OpenConnectionAsync(ct);
        await using var tx = await conn.BeginTransactionAsync(IsolationLevel.RepeatableRead, ct);

        var provedores = await LerProvedoresAsync(conn, tx, ct);
        var nomeProvedor = provedores.FirstOrDefault(p => p.Id == f.Provedor)?.Rotulo;

        // 1) Totais diários do intervalo (ano selecionado ∪ últimos 12 meses + mês atual).
        var (inicio, fim) = VisaoGeralCalculos.IntervaloMeses(f.Ano, datas);
        var diarios = new List<TotalDia>();
        await using (var cmd = NovoComando(conn, tx, VisaoGeralSql.TotaisDiarios))
        {
            AdicionarFiltros(cmd, f);
            AdicionarIntervalo(cmd, inicio, fim);
            await using var r = await cmd.ExecuteReaderAsync(ct);
            while (await r.ReadAsync(ct))
                diarios.Add(new TotalDia(new DateOnly(r.GetInt32(0), r.GetInt32(1), r.GetInt32(2)), r.GetDecimal(3)));
        }

        // 2) Totais por dimensão da segunda da semana passada até ontem.
        var dimensoes = new List<TotalDiaDimensoes>();
        await using (var cmd = NovoComando(conn, tx, VisaoGeralSql.TotaisPorDimensao))
        {
            AdicionarFiltros(cmd, f);
            AdicionarIntervalo(cmd, AnoMes.De(datas.SemanaAnteriorInicio), AnoMes.De(datas.Ontem));
            cmd.Parameters.Add(new NpgsqlParameter<DateOnly>("data_ini", datas.SemanaAnteriorInicio));
            cmd.Parameters.Add(new NpgsqlParameter<DateOnly>("data_fim", datas.Ontem));
            await using var r = await cmd.ExecuteReaderAsync(ct);
            while (await r.ReadAsync(ct))
                dimensoes.Add(new TotalDiaDimensoes(
                    r.GetFieldValue<DateOnly>(0), r.GetString(1), r.GetString(2), r.GetString(3), r.GetDecimal(4)));
        }

        // 3) Orçamento do ano selecionado e dos anos usados nos indicadores.
        var anos = new[] { f.Ano, datas.MesAtual.Ano, datas.UltimoMesFechado.Ano }.Distinct().ToArray();
        var orcamentos = new Dictionary<int, decimal>();
        await using (var cmd = NovoComando(conn, tx, VisaoGeralSql.Orcamentos))
        {
            cmd.Parameters.Add(new NpgsqlParameter<int>("provedor", f.Provedor));
            cmd.Parameters.Add(new NpgsqlParameter<string[]>("periodos", anos.Select(a => a.ToString()).ToArray()));
            await using var r = await cmd.ExecuteReaderAsync(ct);
            while (await r.ReadAsync(ct))
            {
                if (int.TryParse(r.GetString(0).Trim(), out var ano) && !r.IsDBNull(1))
                    orcamentos[ano] = r.GetDecimal(1);
            }
        }

        // 4) Fator do provedor.
        var fator = await LerFatorAsync(conn, tx, f.Provedor, ct);

        await tx.CommitAsync(ct);
        return new DadosBrutosVisaoGeral(diarios, dimensoes, orcamentos, fator, nomeProvedor);
    }

    // ── Auxiliares ───────────────────────────────────────────────────────────

    private static NpgsqlCommand NovoComando(NpgsqlConnection conn, NpgsqlTransaction tx, string sql) =>
        new(sql, conn, tx) { CommandTimeout = 60 };

    private static void AdicionarFiltros(NpgsqlCommand cmd, FiltrosVisaoGeral f)
    {
        cmd.Parameters.Add(new NpgsqlParameter<int>("provedor", f.Provedor));
        cmd.Parameters.Add(new NpgsqlParameter<string[]>("servicos", f.Servicos.ToArray()) { NpgsqlDbType = NpgsqlDbType.Array | NpgsqlDbType.Text });
        cmd.Parameters.Add(new NpgsqlParameter<string[]>("skus", f.Skus.ToArray()) { NpgsqlDbType = NpgsqlDbType.Array | NpgsqlDbType.Text });
        cmd.Parameters.Add(new NpgsqlParameter<string[]>("regioes", f.Regioes.ToArray()) { NpgsqlDbType = NpgsqlDbType.Array | NpgsqlDbType.Text });
    }

    private static void AdicionarIntervalo(NpgsqlCommand cmd, AnoMes inicio, AnoMes fim)
    {
        cmd.Parameters.Add(new NpgsqlParameter<int>("ano_ini", inicio.Ano));
        cmd.Parameters.Add(new NpgsqlParameter<int>("mes_ini", inicio.Mes));
        cmd.Parameters.Add(new NpgsqlParameter<int>("ano_fim", fim.Ano));
        cmd.Parameters.Add(new NpgsqlParameter<int>("mes_fim", fim.Mes));
    }

    private static async Task<List<ProvedorOpcao>> LerProvedoresAsync(NpgsqlConnection conn, NpgsqlTransaction tx, CancellationToken ct)
    {
        var lista = new List<ProvedorOpcao>();
        await using var cmd = NovoComando(conn, tx, VisaoGeralSql.Provedores);
        await using var r = await cmd.ExecuteReaderAsync(ct);
        while (await r.ReadAsync(ct))
        {
            var id = r.GetInt32(0);
            var tipo = r.IsDBNull(1) ? "" : r.GetString(1);
            var nome = r.IsDBNull(2) ? null : r.GetString(2);
            var cliente = r.IsDBNull(3) ? null : r.GetString(3);
            var rotulo = string.Join(" · ", new[] { tipo, cliente ?? nome }.Where(s => !string.IsNullOrWhiteSpace(s)));
            lista.Add(new ProvedorOpcao(id, tipo, nome, cliente, string.IsNullOrEmpty(rotulo) ? $"Provedor {id}" : rotulo));
        }
        return lista;
    }

    private static async Task<List<T>> LerListaAsync<T>(NpgsqlConnection conn, NpgsqlTransaction tx, string sql, int provedor,
        Func<NpgsqlDataReader, T> ler, CancellationToken ct)
    {
        var lista = new List<T>();
        await using var cmd = NovoComando(conn, tx, sql);
        cmd.Parameters.Add(new NpgsqlParameter<int>("provedor", provedor));
        await using var r = await cmd.ExecuteReaderAsync(ct);
        while (await r.ReadAsync(ct))
            lista.Add(ler(r));
        return lista;
    }

    private static async Task<decimal?> LerFatorAsync(NpgsqlConnection conn, NpgsqlTransaction tx, int provedor, CancellationToken ct)
    {
        await using var cmd = NovoComando(conn, tx, VisaoGeralSql.Fator);
        cmd.Parameters.Add(new NpgsqlParameter<int>("provedor", provedor));
        var valor = await cmd.ExecuteScalarAsync(ct);
        return valor is null or DBNull ? null : Convert.ToDecimal(valor);
    }
}
