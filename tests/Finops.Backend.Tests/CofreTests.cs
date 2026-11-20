using System.Security.Cryptography;
using System.Text;
using System.Text.Json;
using Finops.Backend.Admin;
using Finops.Backend.Cofre;
using Microsoft.Extensions.Logging;

namespace Finops.Backend.Tests;

public class CofreCriptoTests
{
    private static byte[] Chave(byte b) => Enumerable.Repeat(b, 32).ToArray();
    private static byte[] U(string s) => Encoding.UTF8.GetBytes(s);

    [Fact]
    public void Ida_e_volta_e_formato_v1()
    {
        var aad = CofreCripto.AadCredencial(42, "AWS_ACCESS_KEY");
        var blob = CofreCripto.Cifrar(Chave(1), U("SEGREDO_TESTE_ida_volta"), aad);

        Assert.Equal(0x01, blob[0]);
        Assert.Equal(1 + 12 + U("SEGREDO_TESTE_ida_volta").Length + 16, blob.Length);
        Assert.Equal("SEGREDO_TESTE_ida_volta", Encoding.UTF8.GetString(CofreCripto.Decifrar(Chave(1), blob, aad)));
    }

    [Fact]
    public void Nonce_aleatorio_por_cifragem()
    {
        var a = CofreCripto.Cifrar(Chave(1), U("x"), U("aad"));
        var b = CofreCripto.Cifrar(Chave(1), U("x"), U("aad"));
        Assert.False(a.AsSpan(1, 12).SequenceEqual(b.AsSpan(1, 12)));
        Assert.False(a.SequenceEqual(b));
    }

    [Fact]
    public void Aad_diferente_falha()
    {
        var blob = CofreCripto.Cifrar(Chave(1), U("s"), CofreCripto.AadCredencial(42, "AWS_ACCESS_KEY"));
        Assert.Throws<CofreCriptoException>(() => CofreCripto.Decifrar(Chave(1), blob, CofreCripto.AadCredencial(43, "AWS_ACCESS_KEY")));
        Assert.Throws<CofreCriptoException>(() => CofreCripto.Decifrar(Chave(1), blob, CofreCripto.AadCredencial(42, "AWS_ASSUME_ROLE")));
    }

    [Fact]
    public void Um_bit_adulterado_em_qualquer_parte_falha()
    {
        var aad = U("aad");
        var blob = CofreCripto.Cifrar(Chave(1), U("SEGREDO_TESTE_bits"), aad);
        foreach (var posicao in new[] { 1, 12, 13, blob.Length - 17, blob.Length - 1 })
        {
            var adulterado = (byte[])blob.Clone();
            adulterado[posicao] ^= 0x01;
            Assert.Throws<CofreCriptoException>(() => CofreCripto.Decifrar(Chave(1), adulterado, aad));
        }
    }

    [Fact]
    public void Dek_errada_falha()
    {
        var blob = CofreCripto.Cifrar(Chave(1), U("s"), U("aad"));
        Assert.Throws<CofreCriptoException>(() => CofreCripto.Decifrar(Chave(2), blob, U("aad")));
    }

    [Fact]
    public void Versao_de_formato_diferente_de_1_e_recusada()
    {
        var blob = CofreCripto.Cifrar(Chave(1), U("s"), U("aad"));
        blob[0] = 0x02;
        var e = Assert.Throws<CofreCriptoException>(() => CofreCripto.Decifrar(Chave(1), blob, U("aad")));
        Assert.Contains("Versão", e.Message);
    }

    [Theory]
    [InlineData(16)]
    [InlineData(31)]
    [InlineData(33)]
    public void Chave_com_tamanho_errado_e_recusada(int tamanho)
    {
        Assert.Throws<CofreCriptoException>(() => CofreCripto.Cifrar(new byte[tamanho], U("s"), U("aad")));
        Assert.Throws<CofreCriptoException>(() => CofreCripto.Decifrar(new byte[tamanho], new byte[40], U("aad")));
    }

    [Fact]
    public void Blob_curto_demais_e_recusado() =>
        Assert.Throws<CofreCriptoException>(() => CofreCripto.Decifrar(Chave(1), new byte[28], U("aad")));

    [Fact]
    public void Formatos_de_aad()
    {
        Assert.Equal("cv:dek:v1:7:1", Encoding.UTF8.GetString(CofreCripto.AadDek(7, 1)));
        Assert.Equal("cv:cred:v1:42:OCI_API_KEY", Encoding.UTF8.GetString(CofreCripto.AadCredencial(42, "OCI_API_KEY")));
    }

    [Fact]
    public void Mensagens_de_erro_nao_contem_dados()
    {
        var blob = CofreCripto.Cifrar(Chave(1), U("SEGREDO_TESTE_msg"), U("aad"));
        var e = Assert.Throws<CofreCriptoException>(() => CofreCripto.Decifrar(Chave(2), blob, U("aad")));
        Assert.DoesNotContain("SEGREDO_TESTE", e.ToString());
    }

    // ── Vetores (gerados pelo pacote Python cryptography: interoperabilidade com o coletor) ──

    public static IEnumerable<object[]> Vetores()
    {
        var caminho = Path.Combine(AppContext.BaseDirectory, "vetores", "cofre_v1.json");
        using var doc = JsonDocument.Parse(File.ReadAllText(caminho));
        foreach (var c in doc.RootElement.GetProperty("casos").EnumerateArray())
        {
            var texto = c.TryGetProperty("texto_claro_hex", out var hex)
                ? Convert.FromHexString(hex.GetString()!)
                : Encoding.UTF8.GetBytes(c.GetProperty("texto_claro").GetString()!);
            yield return [
                c.GetProperty("nome").GetString()!,
                Convert.FromHexString(c.GetProperty("chave_hex").GetString()!),
                Convert.FromHexString(c.GetProperty("nonce_hex").GetString()!),
                c.GetProperty("aad").GetString()!,
                texto,
                Convert.FromHexString(c.GetProperty("blob_hex").GetString()!),
            ];
        }
    }

    [Theory]
    [MemberData(nameof(Vetores))]
    public void Vetores_cofre_v1_sao_reproduzidos(string nome, byte[] chave, byte[] nonce, string aad, byte[] texto, byte[] blobEsperado)
    {
        var aadBytes = Encoding.UTF8.GetBytes(aad);
        Assert.True(blobEsperado.SequenceEqual(CofreCripto.CifrarComNonce(chave, nonce, texto, aadBytes)), $"cifrar: {nome}");
        Assert.True(texto.SequenceEqual(CofreCripto.Decifrar(chave, blobEsperado, aadBytes)), $"decifrar: {nome}");
    }

    [Fact]
    public void Existem_3_vetores() => Assert.Equal(3, Vetores().Count());
}

public class ChavesMestrasTests
{
    private const string SegredoKek = "SEGREDO_TESTE_kek";
    private static readonly string Kek32 = Convert.ToBase64String(Enumerable.Range(0, 32).Select(i => (byte)i).ToArray());
    private static readonly string Kek32b = Convert.ToBase64String(Enumerable.Repeat((byte)7, 32).ToArray());

    private static ChavesMestras Carregar(LoggerDeCaptura log, params (string, string?)[] vars) =>
        ChavesMestras.Carregar(vars.Select(v => new KeyValuePair<string, string?>(v.Item1, v.Item2)), log);

    [Fact]
    public void Carrega_varias_versoes_e_a_atual()
    {
        var c = Carregar(new LoggerDeCaptura(), ("CV_MASTER_KEY_V1", Kek32), ("CV_MASTER_KEY_V2", Kek32b), ("CV_MASTER_KEY_ATUAL", "2"));
        Assert.True(c.Configurado);
        Assert.Equal(2, c.VersaoAtual);
        Assert.Equal(new[] { 1, 2 }, c.VersoesCarregadas.Order());
        Assert.Equal(Convert.FromBase64String(Kek32), c.Kek(1));
        Assert.Equal(2, c.KekAtual().Versao);
    }

    [Theory]
    [InlineData(16)]
    [InlineData(31)]
    [InlineData(33)]
    public void Kek_com_tamanho_errado_e_recusada(int tamanho)
    {
        var log = new LoggerDeCaptura();
        var c = Carregar(log, ("CV_MASTER_KEY_V1", Convert.ToBase64String(new byte[tamanho])), ("CV_MASTER_KEY_ATUAL", "1"));
        Assert.False(c.Configurado);
        Assert.Throws<CofreNaoConfiguradoException>(() => c.KekAtual());
    }

    [Fact]
    public void Ausente_ou_atual_invalida_desativa_o_cofre()
    {
        Assert.False(Carregar(new LoggerDeCaptura()).Configurado);
        Assert.False(Carregar(new LoggerDeCaptura(), ("CV_MASTER_KEY_V1", Kek32)).Configurado);
        Assert.False(Carregar(new LoggerDeCaptura(), ("CV_MASTER_KEY_V1", Kek32), ("CV_MASTER_KEY_ATUAL", "2")).Configurado);
        Assert.False(Carregar(new LoggerDeCaptura(), ("CV_MASTER_KEY_V1", Kek32), ("CV_MASTER_KEY_ATUAL", "um")).Configurado);
    }

    [Fact]
    public void Nunca_loga_o_valor_das_chaves()
    {
        var log = new LoggerDeCaptura();
        Carregar(log,
            ("CV_MASTER_KEY_V1", Kek32),
            ("CV_MASTER_KEY_V2", SegredoKek),            // inválida (não é base64 de 32 bytes)
            ("CV_MASTER_KEY_V3", Kek32b),
            ("CV_MASTER_KEY_ATUAL", "1"));

        Assert.NotEmpty(log.Entradas);
        Assert.DoesNotContain(log.Entradas, e => e.Contains(SegredoKek) || e.Contains(Kek32) || e.Contains(Kek32b));
        Assert.Contains(log.Entradas, e => e.Contains("CV_MASTER_KEY_V2") && e.Contains("inválida"));
    }
}

public class CredencialRegrasTests
{
    private const string Tenancy = "ocid1.tenancy.oc1..aaaaaaaatenancy123";
    private const string Fingerprint = "ab:cd:ef:01:23:45:67:89:ab:cd:ef:01:23:45:67:89";
    private const string SecretoAws = "SEGREDOTESTEaws/000000000000000000000000"; // 40 caracteres válidos
    private const string SegredoAzure = "SEGREDO_TESTE_azure_client_secret";
    private const string PemSecreto = "-----BEGIN PRIVATE KEY-----\nSEGREDO_TESTE_pem\n-----END PRIVATE KEY-----";

    private static Dictionary<string, string?> Campos(params (string, string?)[] c) => c.ToDictionary(x => x.Item1, x => x.Item2);

    private static Dictionary<string, string?> Oci() => Campos(
        ("user_ocid", "ocid1.user.oc1..aaaaaaaauser1"), ("tenancy_ocid", Tenancy), ("fingerprint", Fingerprint),
        ("region", "sa-saopaulo-1"), ("private_key_pem", PemSecreto), ("passphrase", "SEGREDO_TESTE_pass"));

    // Access key id FALSO, montado em tempo de execução para não parecer um segredo real no código-fonte
    // (o push protection do GitHub bloqueia literais no formato AKIA + 16 caracteres).
    private static readonly string AccessKeyFalsa = "AKIA" + new string('X', 12) + "MNOP";

    private static Dictionary<string, string?> Aws() => Campos(("access_key_id", AccessKeyFalsa), ("secret_access_key", SecretoAws));

    private static Dictionary<string, string?> Azure() => Campos(
        ("tenant_id", "3F2504E0-4F89-41D3-9A0C-0305E82C3301"), ("client_id", "11111111-2222-3333-4444-555555555555"), ("client_secret", SegredoAzure));

    private static string SaJson(string tipo = "service_account", string? email = "coletor@proj.iam.gserviceaccount.com") =>
        JsonSerializer.Serialize(new Dictionary<string, string?>
        {
            ["type"] = tipo, ["client_email"] = email, ["private_key"] = "-----BEGIN PRIVATE KEY-----\nSEGREDO_TESTE_gcp\n-----END PRIVATE KEY-----",
        });

    private static ErroNegocioException Erro(Action a) => Assert.Throws<ErroNegocioException>(a);

    private static string Json(CredencialValida c) => Encoding.UTF8.GetString(c.ConteudoJson);

    // ── Válidos e dica ──────────────────────────────────────────────────────

    [Fact]
    public void Oci_valida_dica_e_json_normalizado()
    {
        var c = CredencialRegras.Validar("oci_api_key", "OCI", Tenancy, Oci());
        Assert.Equal("OCI_API_KEY", c.Tipo);
        Assert.Equal("…67:89", c.Dica);
        using var doc = JsonDocument.Parse(c.ConteudoJson);
        Assert.Equal(Tenancy, doc.RootElement.GetProperty("tenancy_ocid").GetString());
        Assert.Equal("SEGREDO_TESTE_pass", doc.RootElement.GetProperty("passphrase").GetString());
    }

    [Fact]
    public void Aws_access_key_e_assume_role()
    {
        Assert.Equal("…MNOP", CredencialRegras.Validar("AWS_ACCESS_KEY", "AWS", "123456789012", Aws()).Dica);

        var role = Aws();
        role["role_arn"] = "arn:aws:iam::123456789012:role/CloudVistaLeitura";
        role["external_id"] = "cv-ultragaz-01";
        Assert.Equal("…MNOP · CloudVistaLeitura", CredencialRegras.Validar("AWS_ASSUME_ROLE", "AWS", "123456789012", role).Dica);
    }

    [Fact]
    public void Azure_normaliza_guids_e_dica()
    {
        var c = CredencialRegras.Validar("AZURE_SERVICE_PRINCIPAL", "AZURE", "x", Azure());
        Assert.Equal("…5555", c.Dica);
        Assert.Contains("\"tenant_id\":\"3f2504e0-4f89-41d3-9a0c-0305e82c3301\"", Json(c));
    }

    [Fact]
    public void Gcp_dica_e_o_client_email()
    {
        var c = CredencialRegras.Validar("GCP_SERVICE_ACCOUNT", "GCP", "x", Campos(("service_account_json", SaJson())));
        Assert.Equal("coletor@proj.iam.gserviceaccount.com", c.Dica);
    }

    [Fact]
    public void Dica_limitada_a_40_caracteres()
    {
        var email = new string('a', 60) + "@x.iam.gserviceaccount.com";
        var c = CredencialRegras.Validar("GCP_SERVICE_ACCOUNT", "GCP", "x", Campos(("service_account_json", SaJson(email: email))));
        Assert.True(c.Dica.Length <= 40);
    }

    // ── Dica nunca contém segredo ───────────────────────────────────────────

    [Fact]
    public void Dica_nunca_contem_o_segredo()
    {
        var dicas = new[]
        {
            CredencialRegras.Validar("OCI_API_KEY", "OCI", Tenancy, Oci()).Dica,
            CredencialRegras.Validar("AWS_ACCESS_KEY", "AWS", "x", Aws()).Dica,
            CredencialRegras.Validar("AZURE_SERVICE_PRINCIPAL", "AZURE", "x", Azure()).Dica,
            CredencialRegras.Validar("GCP_SERVICE_ACCOUNT", "GCP", "x", Campos(("service_account_json", SaJson()))).Dica,
        };
        Assert.DoesNotContain(dicas, d => d.Contains("SEGREDO_TESTE") || d.Contains("SEGREDOTESTE") || d.Contains(SecretoAws) || d.Contains(SegredoAzure) || d.Contains("BEGIN"));
    }

    // ── Inválidos (mensagens nunca ecoam o valor) ───────────────────────────

    public static TheoryData<string, string, string, Dictionary<string, string?>, string> Invalidos()
    {
        var d = new TheoryData<string, string, string, Dictionary<string, string?>, string>();
        void Add(string tipo, string prov, string ident, Action<Dictionary<string, string?>> mudar, Dictionary<string, string?> baseCampos, string campo)
        {
            mudar(baseCampos);
            d.Add(tipo, prov, ident, baseCampos, campo);
        }
        Add("OCI_API_KEY", "OCI", Tenancy, c => c["tenancy_ocid"] = "ocid1.tenancy.oc1..outratenancy", Oci(), "tenancy_ocid");
        Add("OCI_API_KEY", "OCI", Tenancy, c => c["user_ocid"] = "ocid1.group.oc1..SEGREDO_TESTE_x", Oci(), "user_ocid");
        Add("OCI_API_KEY", "OCI", Tenancy, c => c["fingerprint"] = "SEGREDO_TESTE_fp", Oci(), "fingerprint");
        Add("OCI_API_KEY", "OCI", Tenancy, c => c["region"] = "SEGREDO_TESTE", Oci(), "region");
        Add("OCI_API_KEY", "OCI", Tenancy, c => c["private_key_pem"] = "SEGREDO_TESTE_semPEM", Oci(), "private_key_pem");
        Add("AWS_ACCESS_KEY", "AWS", "x", c => c["access_key_id"] = "SEGREDO_TESTE_akid", Aws(), "access_key_id");
        Add("AWS_ACCESS_KEY", "AWS", "x", c => c["secret_access_key"] = "SEGREDO_TESTE_curto", Aws(), "secret_access_key");
        Add("AWS_ASSUME_ROLE", "AWS", "x", c => c["role_arn"] = "arn:aws:iam::123:role/SEGREDO_TESTE", Aws(), "role_arn");
        Add("AZURE_SERVICE_PRINCIPAL", "AZURE", "x", c => c["tenant_id"] = "SEGREDO_TESTE_tenant", Azure(), "tenant_id");
        Add("AZURE_SERVICE_PRINCIPAL", "AZURE", "x", c => c["client_id"] = "SEGREDO_TESTE_client", Azure(), "client_id");
        Add("GCP_SERVICE_ACCOUNT", "GCP", "x", c => c["service_account_json"] = "{ SEGREDO_TESTE nao e json", Campos(), "service_account_json");
        Add("GCP_SERVICE_ACCOUNT", "GCP", "x", c => c["service_account_json"] = SaJson(tipo: "authorized_user"), Campos(), "service_account_json");
        Add("GCP_SERVICE_ACCOUNT", "GCP", "x", c => c["service_account_json"] = SaJson(email: null), Campos(), "service_account_json");
        return d;
    }

    [Theory]
    [MemberData(nameof(Invalidos))]
    public void Campos_invalidos_e_mensagem_sem_o_valor(string tipo, string provedor, string identificador, Dictionary<string, string?> campos, string campo)
    {
        var e = Erro(() => CredencialRegras.Validar(tipo, provedor, identificador, campos));
        Assert.Equal(400, e.Status);
        Assert.StartsWith(campo, e.Message);
        Assert.DoesNotContain("SEGREDO_TESTE", e.Message);
        Assert.DoesNotContain("outratenancy", e.Message);
    }

    [Fact]
    public void Tipo_incompativel_com_o_provedor_da_conta()
    {
        Assert.StartsWith("Tipo:", Erro(() => CredencialRegras.Validar("AWS_ACCESS_KEY", "OCI", Tenancy, Aws())).Message);
        Assert.StartsWith("Tipo:", Erro(() => CredencialRegras.Validar("GCP_SERVICE_ACCOUNT", "AZURE", "x", Azure())).Message);
        Assert.StartsWith("Tipo:", Erro(() => CredencialRegras.Validar("SENHA", "AWS", "x", Aws())).Message);
    }

    [Fact]
    public void Campo_obrigatorio_ausente_e_campo_extra()
    {
        var semSecret = Aws();
        semSecret.Remove("secret_access_key");
        Assert.StartsWith("secret_access_key", Erro(() => CredencialRegras.Validar("AWS_ACCESS_KEY", "AWS", "x", semSecret)).Message);

        var extra = Aws();
        extra["SEGREDO_TESTE_campo"] = "x";
        var e = Erro(() => CredencialRegras.Validar("AWS_ACCESS_KEY", "AWS", "x", extra));
        Assert.StartsWith("Campos:", e.Message);
        Assert.DoesNotContain("SEGREDO_TESTE", e.Message);
    }

    [Fact]
    public void Payload_acima_de_16_kb_e_recusado()
    {
        var c = Oci();
        c["private_key_pem"] = "-----BEGIN PRIVATE KEY-----\n" + new string('A', 17 * 1024);
        Assert.Contains("16 KB", Erro(() => CredencialRegras.Validar("OCI_API_KEY", "OCI", Tenancy, c)).Message);
    }

    [Fact]
    public void Aceite_somente_leitura_obrigatorio()
    {
        var servico = new CofreServico(null!, null!);
        foreach (var aceite in new bool?[] { null, false })
        {
            var e = Assert.ThrowsAsync<ErroNegocioException>(() =>
                servico.SalvarAsync(new Identidade.UsuarioAtual(1, "a@b.c", "A", "ADMINISTRADOR", true), 1,
                    new CredencialEntrada("AWS_ACCESS_KEY", Aws(), aceite), CancellationToken.None)).GetAwaiter().GetResult();
            Assert.Equal(400, e.Status);
        }
    }
}

/// <summary>Logger que guarda mensagem formatada + exceção completa + valores estruturados.</summary>
public sealed class LoggerDeCaptura : ILogger
{
    public List<string> Entradas { get; } = [];
    public IDisposable? BeginScope<TState>(TState state) where TState : notnull => null;
    public bool IsEnabled(LogLevel logLevel) => true;

    public void Log<TState>(LogLevel logLevel, EventId eventId, TState state, Exception? exception, Func<TState, Exception?, string> formatter)
    {
        var texto = formatter(state, exception);
        if (exception is not null) texto += " | " + exception;
        if (state is IEnumerable<KeyValuePair<string, object?>> pares)
            texto += " | " + string.Join(";", pares.Select(p => $"{p.Key}={p.Value}"));
        Entradas.Add(texto);
    }
}
