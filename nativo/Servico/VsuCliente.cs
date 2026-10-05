using System.Net.Http;
using System.Text.Json;
using VitoriaSoft.Atendimento.Modelo;

namespace VitoriaSoft.Atendimento.Servico;

public sealed class ResultadoVsu
{
    public bool Ok { get; init; }
    public string Mensagem { get; init; } = "";
    public List<Empresa> Empresas { get; init; } = [];

    public static ResultadoVsu Falha(string mensagem) => new() { Ok = false, Mensagem = mensagem };
    public static ResultadoVsu Sucesso(string mensagem, List<Empresa> empresas) =>
        new() { Ok = true, Mensagem = mensagem, Empresas = empresas };
}

public static class VsuCliente
{
    public static async Task<ResultadoVsu> BuscarEmpresasAsync(Configuracao config)
    {
        if (string.IsNullOrWhiteSpace(config.VsuUrl))
            return ResultadoVsu.Falha("Informe o endereço do VSU Cloud.");
        if (string.IsNullOrWhiteSpace(config.VsuChave))
            return ResultadoVsu.Falha("Informe a chave de API.");

        var caminho = (config.VsuCaminho ?? "").Trim();
        if (caminho.Length == 0) caminho = "/api/empresas";
        if (!caminho.StartsWith('/')) caminho = "/" + caminho;
        var baseUrl = config.VsuUrl.Trim().TrimEnd('/');
        if (!Uri.TryCreate(baseUrl + caminho, UriKind.Absolute, out var uri) || uri.Scheme is not ("https" or "http"))
            return ResultadoVsu.Falha("O endereço do VSU Cloud não é válido.");

        using var http = new HttpClient { Timeout = TimeSpan.FromSeconds(25) };
        using var pedido = new HttpRequestMessage(HttpMethod.Get, uri);
        var chave = config.VsuChave.Trim();
        var esquema = config.VsuEsquema ?? "Bearer";
        if (esquema == "ApiKey")
            pedido.Headers.TryAddWithoutValidation("X-Api-Key", chave);
        else if (esquema == "Basic")
            pedido.Headers.TryAddWithoutValidation("Authorization", chave.StartsWith("Basic ", StringComparison.OrdinalIgnoreCase) ? chave : "Basic " + chave);
        else
            pedido.Headers.TryAddWithoutValidation("Authorization", chave.StartsWith("Bearer ", StringComparison.OrdinalIgnoreCase) ? chave : "Bearer " + chave);

        string corpo;
        try
        {
            using var resposta = await http.SendAsync(pedido);
            corpo = await resposta.Content.ReadAsStringAsync();
            if (!resposta.IsSuccessStatusCode)
                return ResultadoVsu.Falha("O VSU Cloud respondeu " + (int)resposta.StatusCode + ". Confira o endereço, o caminho e a chave.");
        }
        catch (Exception ex)
        {
            return ResultadoVsu.Falha("Não foi possível falar com o VSU Cloud. " + ex.Message);
        }

        try
        {
            var empresas = LerEmpresas(corpo);
            if (empresas.Count == 0)
                return ResultadoVsu.Falha("A conexão funcionou, mas a resposta não trouxe empresas com nome.");
            return ResultadoVsu.Sucesso(empresas.Count + " empresas recebidas.", empresas);
        }
        catch (Exception ex)
        {
            return ResultadoVsu.Falha("A resposta não veio como lista de empresas. " + ex.Message);
        }
    }

    public static (int novas, int jaExistiam) Mesclar(Banco banco, IEnumerable<Empresa> vindas)
    {
        banco.Empresas ??= [];
        var novas = 0;
        var jaExistiam = 0;
        foreach (var vinda in vindas)
        {
            if (string.IsNullOrWhiteSpace(vinda.Nome)) continue;
            var existente = banco.Empresas.FirstOrDefault(e =>
                (!string.IsNullOrWhiteSpace(vinda.Codigo) && e.Codigo.Equals(vinda.Codigo, StringComparison.OrdinalIgnoreCase))
                || e.Nome.Equals(vinda.Nome, StringComparison.OrdinalIgnoreCase));
            if (existente != null)
            {
                jaExistiam++;
                continue;
            }
            if (string.IsNullOrWhiteSpace(vinda.Id)) vinda.Id = "vsu-" + Guid.NewGuid().ToString("N");
            vinda.Manual = false;
            banco.Empresas.Add(vinda);
            novas++;
        }
        return (novas, jaExistiam);
    }

    private static List<Empresa> LerEmpresas(string json)
    {
        using var doc = JsonDocument.Parse(json);
        var lista = AcharLista(doc.RootElement) ?? throw new InvalidOperationException("Nenhuma lista encontrada.");
        var saida = new List<Empresa>();
        foreach (var item in lista.EnumerateArray())
        {
            if (item.ValueKind != JsonValueKind.Object) continue;
            var nome = Primeiro(item, 0, "nome", "nomeFantasia", "fantasia", "razaoSocial", "razao", "farmacia", "empresa", "name");
            if (string.IsNullOrWhiteSpace(nome)) continue;
            saida.Add(new Empresa
            {
                Nome = nome.Trim(),
                Codigo = Primeiro(item, 0, "codigo", "cod", "code") ?? "",
                Ddd = Primeiro(item, 0, "ddd") ?? "",
                Telefone = Primeiro(item, 0, "telefone", "fone", "phone", "celular") ?? "",
                Telefone2 = Primeiro(item, 0, "telefone2", "fone2") ?? "",
                Cidade = Primeiro(item, 0, "cidade", "city", "municipio") ?? "",
                Obs = Primeiro(item, 0, "cnpj", "obs", "observacao") ?? ""
            });
            if (saida.Count >= 5000) break;
        }
        return saida;
    }

    private static JsonElement? AcharLista(JsonElement raiz)
    {
        if (raiz.ValueKind == JsonValueKind.Array) return raiz;
        if (raiz.ValueKind != JsonValueKind.Object) return null;
        foreach (var nome in new[] { "empresas", "farmacias", "clientes", "data", "items", "result", "results", "content" })
        {
            foreach (var prop in raiz.EnumerateObject())
            {
                if (prop.Name.Equals(nome, StringComparison.OrdinalIgnoreCase) && prop.Value.ValueKind == JsonValueKind.Array)
                    return prop.Value;
            }
        }
        foreach (var prop in raiz.EnumerateObject())
        {
            if (prop.Value.ValueKind == JsonValueKind.Array) return prop.Value;
        }
        return null;
    }

    private static string? Primeiro(JsonElement item, int profundidade, params string[] nomes)
    {
        if (item.ValueKind != JsonValueKind.Object) return null;
        foreach (var prop in item.EnumerateObject())
        {
            if (!nomes.Any(n => prop.Name.Equals(n, StringComparison.OrdinalIgnoreCase))) continue;
            return prop.Value.ValueKind switch
            {
                JsonValueKind.String => prop.Value.GetString(),
                JsonValueKind.Number => prop.Value.ToString(),
                _ => null
            };
        }
        if (profundidade >= 2) return null;
        foreach (var prop in item.EnumerateObject())
        {
            if (prop.Value.ValueKind != JsonValueKind.Object) continue;
            var interno = Primeiro(prop.Value, profundidade + 1, nomes);
            if (!string.IsNullOrWhiteSpace(interno)) return interno;
        }
        return null;
    }
}
