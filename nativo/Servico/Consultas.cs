using VitoriaSoft.Atendimento.Modelo;

namespace VitoriaSoft.Atendimento.Servico;

public sealed class Consultas
{
    private readonly Banco _banco;
    private List<ResumoFarmacia>? _farmacias;
    private Dictionary<string, List<Chamado>>? _porFarmacia;

    public Consultas(Banco banco) => _banco = banco;

    public void Invalidar()
    {
        _farmacias = null;
        _porFarmacia = null;
    }

    public string NomeCanal(string? id)
    {
        if (string.IsNullOrWhiteSpace(id)) return "Não informado";
        return _banco.Config.Canais.FirstOrDefault(c => c.Id == id)?.Nome ?? id;
    }

    public string NomeStatus(string? id) =>
        _banco.Config.Status.FirstOrDefault(s => s.Id == id)?.Nome ?? (string.IsNullOrWhiteSpace(id) ? "—" : id);

    public IReadOnlyList<ResumoFarmacia> Farmacias()
    {
        Garantir();
        return _farmacias!;
    }

    public ResumoFarmacia? Exata(string? nome)
    {
        var chave = Chave(nome);
        if (chave.Length == 0) return null;
        var achada = Farmacias().FirstOrDefault(f => Chave(f.Nome) == chave);
        var empresa = _banco.Empresas.FirstOrDefault(e => Chave(e.Nome) == chave);
        if (achada == null && empresa == null) return null;
        return new ResumoFarmacia
        {
            Nome = achada?.Nome ?? empresa!.Nome,
            Quantidade = achada?.Quantidade ?? 0,
            Ddd = Texto(achada?.Ddd, empresa?.Ddd),
            Telefone = Texto(achada?.Telefone, empresa?.Telefone),
            Telefone2 = Texto(achada?.Telefone2, empresa?.Telefone2),
            Codigo = Texto(achada?.Codigo, empresa?.Codigo),
            Ultima = achada?.Ultima ?? ""
        };
    }

    public List<Chamado> DoDia(string iso) =>
        _banco.Chamados.Where(c => c.Data == iso)
            .OrderBy(c => c.HoraInicio)
            .ToList();

    public List<Chamado> EmAberto() =>
        _banco.Chamados.Where(c => c.Status is not "concluido")
            .OrderByDescending(c => c.Data)
            .ThenByDescending(c => c.HoraInicio)
            .ToList();

    public int AplicarFarmacia(string nomeAntigo, string nome, string codigo, string ddd, string telefone, string telefone2)
    {
        var chave = Chave(nomeAntigo);
        if (chave.Length == 0) return 0;
        var n = 0;
        foreach (var c in _banco.Chamados)
        {
            if (Chave(c.Farmacia) != chave) continue;
            c.Farmacia = nome;
            c.Codigo = codigo;
            c.Ddd = ddd;
            c.Telefone = telefone;
            c.Telefone2 = telefone2;
            c.AtualizadoEm = DateTime.Now.ToString("s");
            n++;
        }
        return n;
    }

    public int RenomearTecnico(string de, string para)
    {
        var n = 0;
        foreach (var c in _banco.Chamados)
        {
            if (!string.Equals(c.Tecnico, de, StringComparison.OrdinalIgnoreCase)) continue;
            c.Tecnico = para;
            n++;
        }
        var i = _banco.Config.Tecnicos.FindIndex(t => t.Equals(de, StringComparison.OrdinalIgnoreCase));
        if (i >= 0) _banco.Config.Tecnicos[i] = para;
        else if (!_banco.Config.Tecnicos.Any(t => t.Equals(para, StringComparison.OrdinalIgnoreCase)))
            _banco.Config.Tecnicos.Add(para);
        return n;
    }

    public IReadOnlyList<Chamado> DaFarmacia(string? nome)
    {
        Garantir();
        return _porFarmacia!.TryGetValue(Chave(nome), out var lista) ? lista : [];
    }

    public List<Chamado> Filtrar(string busca, string canal, string status, string tecnico, bool somenteHoje = false)
    {
        var q = busca.Trim();
        var hoje = DateTime.Today.ToString("yyyy-MM-dd");
        var lista = new List<Chamado>();
        for (var i = _banco.Chamados.Count - 1; i >= 0; i--)
        {
            var c = _banco.Chamados[i];
            if (somenteHoje && c.Data != hoje) continue;
            if (!Aceita(c, q, canal, status, tecnico)) continue;
            lista.Add(c);
        }
        lista.Sort((a, b) => string.Compare(b.Data, a.Data, StringComparison.Ordinal));
        return lista;
    }

    public string NovoProtocolo()
    {
        var usados = new HashSet<string>(_banco.Chamados.Select(c => c.Protocolo));
        var n = _banco.Chamados.Count + 1;
        string protocolo;
        do
        {
            protocolo = "AT-" + n.ToString("00000");
            n++;
        } while (usados.Contains(protocolo));
        return protocolo;
    }

    public static void Encerrar(Chamado chamado)
    {
        chamado.Status = "concluido";
        if (string.IsNullOrWhiteSpace(chamado.HoraFim))
            chamado.HoraFim = DateTime.Now.ToString("HH:mm");
        chamado.AtualizadoEm = DateTime.Now.ToString("s");
    }

    public static string DataBr(string? iso)
    {
        if (string.IsNullOrWhiteSpace(iso) || iso.Length < 10) return iso ?? "";
        return iso.Substring(8, 2) + "/" + iso.Substring(5, 2) + "/" + iso.Substring(0, 4);
    }

    public static string Fone(string? ddd, string? numero)
    {
        var n = Digitos(numero);
        if (n.Length == 0) return "";
        var bonito = n.Length == 9 ? n[..5] + "-" + n[5..] : n.Length == 8 ? n[..4] + "-" + n[4..] : n;
        var d = Digitos(ddd);
        return d.Length == 0 ? bonito : "(" + d + ") " + bonito;
    }

    public static string Digitos(string? valor)
    {
        if (string.IsNullOrEmpty(valor)) return "";
        var chars = new char[valor.Length];
        var n = 0;
        foreach (var c in valor)
        {
            if (char.IsDigit(c)) chars[n++] = c;
        }
        return new string(chars, 0, n);
    }

    private void Garantir()
    {
        if (_farmacias != null) return;
        var mapa = new Dictionary<string, ResumoFarmacia>(StringComparer.Ordinal);
        var grupos = new Dictionary<string, List<Chamado>>(StringComparer.Ordinal);
        foreach (var c in _banco.Chamados)
        {
            var chave = Chave(c.Farmacia);
            if (chave.Length == 0) continue;
            if (!mapa.TryGetValue(chave, out var resumo))
            {
                resumo = new ResumoFarmacia { Nome = (c.Farmacia ?? "").Trim() };
                mapa[chave] = resumo;
                grupos[chave] = [];
            }
            resumo.Quantidade++;
            if (!string.IsNullOrWhiteSpace(c.Telefone))
            {
                resumo.Ddd = c.Ddd ?? "";
                resumo.Telefone = c.Telefone;
            }
            if (!string.IsNullOrWhiteSpace(c.Telefone2)) resumo.Telefone2 = c.Telefone2;
            if (!string.IsNullOrWhiteSpace(c.Codigo)) resumo.Codigo = c.Codigo;
            if (string.Compare(c.Data, resumo.Ultima, StringComparison.Ordinal) > 0) resumo.Ultima = c.Data ?? "";
            grupos[chave].Add(c);
        }
        foreach (var lista in grupos.Values) lista.Reverse();
        _farmacias = mapa.Values.OrderByDescending(f => f.Quantidade).ToList();
        _porFarmacia = grupos;
    }

    private static bool Aceita(Chamado c, string q, string canal, string status, string tecnico)
    {
        if (canal == "__vazio")
        {
            if (!string.IsNullOrWhiteSpace(c.Canal)) return false;
        }
        else if (canal.Length > 0 && c.Canal != canal) return false;
        if (tecnico.Length > 0 && !string.Equals(c.Tecnico, tecnico, StringComparison.OrdinalIgnoreCase)) return false;
        if (status == "abertos" && c.Status == "concluido") return false;
        if (status == "retorno-agendado" && c.Status is not ("retorno" or "agendado")) return false;
        if (status is not ("abertos" or "todos" or "" or "retorno-agendado") && c.Status != status) return false;
        if (q.Length == 0) return true;
        var texto = string.Join(' ', c.Protocolo, c.Farmacia, c.Cliente, c.Assunto, c.Telefone, c.Telefone2, c.Obs, c.Tecnico, c.Codigo);
        return texto.Contains(q, StringComparison.OrdinalIgnoreCase);
    }

    private static string Chave(string? nome) => (nome ?? "").Trim().ToUpperInvariant();

    private static string Texto(string? principal, string? reserva) =>
        string.IsNullOrWhiteSpace(principal) ? reserva ?? "" : principal;
}
