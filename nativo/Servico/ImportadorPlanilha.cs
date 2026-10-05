using System.Globalization;
using System.IO.Compression;
using System.Text;
using System.Text.RegularExpressions;
using VitoriaSoft.Atendimento.Modelo;

namespace VitoriaSoft.Atendimento.Servico;

public static class ImportadorPlanilha
{
    public static ResultadoImportacao Mesclar(Banco banco, string caminhoXlsx)
    {
        var lido = Ler(caminhoXlsx);
        var porId = new Dictionary<string, Chamado>(StringComparer.Ordinal);
        foreach (var existente in banco.Chamados)
            if (!string.IsNullOrEmpty(existente.Id)) porId[existente.Id] = existente;
        var resultado = new ResultadoImportacao();
        foreach (var chamado in lido.Chamados)
        {
            if (!porId.TryGetValue(chamado.Id, out var existente))
            {
                chamado.Protocolo = ProximoProtocolo(banco);
                banco.Chamados.Add(chamado);
                porId[chamado.Id] = chamado;
                resultado.Novos++;
                continue;
            }
            if (MexidoNaRecepcao(existente)) continue;
            if (chamado.Status == "concluido" && existente.Status != "concluido")
            {
                existente.Status = "concluido";
                if (string.IsNullOrWhiteSpace(existente.HoraFim))
                    existente.HoraFim = DateTime.Now.ToString("HH:mm");
                existente.AtualizadoEm = existente.Data;
                resultado.AtendidosPelaCor++;
            }
            else if (chamado.Status == "aberto" && existente.Status == "concluido")
            {
                existente.Status = "aberto";
                existente.AtualizadoEm = existente.Data;
                resultado.Reabertos++;
            }
        }

        foreach (var tecnico in lido.Tecnicos)
        {
            if (!banco.Config.Tecnicos.Any(t => t.Equals(tecnico, StringComparison.OrdinalIgnoreCase)))
                banco.Config.Tecnicos.Add(tecnico);
        }
        foreach (var frase in lido.Frases)
        {
            if (!banco.Config.Frases.Contains(frase)) banco.Config.Frases.Add(frase);
        }

        var numeros = new HashSet<string>(banco.Ramais.Select(r => r.Numero), StringComparer.Ordinal);
        foreach (var ramal in lido.Ramais)
        {
            if (numeros.Add(ramal.Numero)) banco.Ramais.Add(ramal);
        }
        foreach (var dica in lido.Dicas)
        {
            if (!banco.DicasTelefone.Contains(dica)) banco.DicasTelefone.Add(dica);
        }
        return resultado;
    }

    private static bool MexidoNaRecepcao(Chamado chamado) =>
        (chamado.AtualizadoEm ?? "").Contains('T');

    private static string ProximoProtocolo(Banco banco)
    {
        var usados = new HashSet<string>(banco.Chamados.Select(c => c.Protocolo), StringComparer.OrdinalIgnoreCase);
        var n = banco.Chamados.Count + 1;
        string protocolo;
        do
        {
            protocolo = "AT-" + n.ToString("00000", CultureInfo.InvariantCulture);
            n++;
        } while (usados.Contains(protocolo));
        return protocolo;
    }

    private static Dictionary<string, string> Cabecalhos(List<Linha> linhas, List<string> strings)
    {
        var mapa = new Dictionary<string, string>(StringComparer.Ordinal)
        {
            ["ok"] = "A", ["inicio"] = "B", ["cliente"] = "C", ["farmacia"] = "D",
            ["marca"] = "E", ["tecnico"] = "F", ["assunto"] = "G", ["ddd"] = "H",
            ["telefone"] = "I", ["telefone2"] = "J", ["fim"] = "K", ["obs"] = "L",
            ["ticket"] = "M", ["codigo"] = "N", ["dia"] = "O", ["mes"] = "P", ["agenda"] = "Q"
        };
        foreach (var linha in linhas)
        {
            if (linha.N != 1) continue;
            foreach (var par in linha.Celulas)
            {
                var nome = TextoDe(linha.Celulas, par.Key, strings).Trim().ToUpperInvariant();
                var chave = nome switch
                {
                    "OK" => "ok",
                    "H.I" or "HI" or "HORA INICIO" or "HORA INÍCIO" => "inicio",
                    "CLIENTE" => "cliente",
                    "FARMACIA" or "FARMÁCIA" => "farmacia",
                    "X" => "marca",
                    "TECNICO" or "TÉCNICO" => "tecnico",
                    "ASSUNTO" => "assunto",
                    "DDD" => "ddd",
                    "TELEFONE" => "telefone",
                    "TELEFONE 2" or "TELEFONE2" => "telefone2",
                    "H.F" or "HF" or "HORA FIM" => "fim",
                    "OBS" or "OBS - ANOTACOES" or "OBS - ANOTAÇÕES" or "OBSERVAÇÕES" => "obs",
                    "TICKET" => "ticket",
                    "CÓDIGO" or "CODIGO" => "codigo",
                    "DIA" => "dia",
                    "MES" or "MÊS" => "mes",
                    "AGENDAMENTO" => "agenda",
                    _ => ""
                };
                if (chave.Length > 0) mapa[chave] = par.Key;
            }
            break;
        }
        return mapa;
    }

    private static Dictionary<int, string> Cores(string stylesXml)
    {
        var preenchimentos = new List<string>();
        foreach (Match preenchimento in Regex.Matches(stylesXml, "<fill>.*?</fill>", RegexOptions.Singleline))
            preenchimentos.Add(CorDoPreenchimento(preenchimento.Value));
        var mapa = new Dictionary<int, string>();
        var bloco = Regex.Match(stylesXml, "<cellXfs[^>]*>(.*)</cellXfs>", RegexOptions.Singleline);
        if (!bloco.Success) return mapa;
        var i = 0;
        foreach (Match estilo in Regex.Matches(bloco.Groups[1].Value, "<xf\\b[^>]*fillId=\"(\\d+)\""))
        {
            var id = int.Parse(estilo.Groups[1].Value, CultureInfo.InvariantCulture);
            mapa[i++] = id >= 0 && id < preenchimentos.Count ? preenchimentos[id] : "branco";
        }
        return mapa;
    }

    private static string CorDoPreenchimento(string valor)
    {
        if (!valor.Contains("solid", StringComparison.Ordinal)) return "branco";
        var tema = Regex.Match(valor, "theme=\"(\\d+)\"");
        if (tema.Success && int.TryParse(tema.Groups[1].Value, out var indice))
        {
            if (indice is 3 or 4 or 8) return "azul";
            if (indice == 0) return "branco";
            return "outro";
        }
        var rgb = Regex.Match(valor, "rgb=\"([0-9A-Fa-f]{6,8})\"");
        if (!rgb.Success) return "outro";
        var hex = rgb.Groups[1].Value;
        if (hex.Length == 8) hex = hex[2..];
        var r = Convert.ToInt32(hex[..2], 16);
        var g = Convert.ToInt32(hex.Substring(2, 2), 16);
        var b = Convert.ToInt32(hex.Substring(4, 2), 16);
        if (r > 235 && g > 235 && b > 235) return "branco";
        if (b > r + 20 && b > g + 5 && b > 100) return "azul";
        return "outro";
    }

    private static string CorDaLinha(Dictionary<string, Celula> celulas, Dictionary<int, string> cores, params string[] colunas)
    {
        var outra = false;
        foreach (var coluna in colunas)
        {
            if (!celulas.TryGetValue(coluna, out var celula)) continue;
            if (!cores.TryGetValue(celula.Estilo, out var cor)) continue;
            if (cor == "azul") return "azul";
            if (cor == "outro") outra = true;
        }
        return outra ? "outro" : "branco";
    }

    private static PlanilhaLida Ler(string caminhoXlsx)
    {
        using var zip = ZipFile.OpenRead(caminhoXlsx);
        var strings = Texto(zip, "xl/sharedStrings.xml");
        var compartilhadas = string.IsNullOrEmpty(strings) ? [] : SharedStrings(strings);
        var abas = Abas(zip);
        var principal = abas.FirstOrDefault(a => a.Nome == "Plan2").Arquivo
            ?? abas.FirstOrDefault().Arquivo
            ?? throw new InvalidOperationException("A planilha não tem uma aba de chamados.");
        var montado = MontarChamados(Linhas(Texto(zip, principal)), compartilhadas, Cores(Texto(zip, "xl/styles.xml")));

        var ramaisArquivo = abas.FirstOrDefault(a => a.Nome.Contains("ramal", StringComparison.OrdinalIgnoreCase)).Arquivo;
        var chatArquivo = abas.FirstOrDefault(a => a.Nome.Contains("vschat", StringComparison.OrdinalIgnoreCase) || a.Nome.Contains("chat", StringComparison.OrdinalIgnoreCase)).Arquivo;
        var ramais = string.IsNullOrEmpty(ramaisArquivo)
            ? new RamaisLidos()
            : RamaisDe(Linhas(Texto(zip, ramaisArquivo)), compartilhadas);
        if (ramais.Itens.Count == 0) ramais = RamaisPadrao();
        var frases = string.IsNullOrEmpty(chatArquivo) ? [] : FrasesDaAba(Linhas(Texto(zip, chatArquivo)), compartilhadas);
        return new PlanilhaLida(montado.Chamados, montado.Tecnicos, frases, ramais.Itens, ramais.Dicas);
    }

    private static List<(string Nome, string Arquivo)> Abas(ZipArchive zip)
    {
        var rels = Texto(zip, "xl/_rels/workbook.xml.rels");
        var wb = Texto(zip, "xl/workbook.xml");
        var alvos = new Dictionary<string, string>(StringComparer.Ordinal);
        foreach (Match m in Regex.Matches(rels, @"Id=""([^""]+)""[^>]*Target=""([^""]+)"""))
            alvos[m.Groups[1].Value] = m.Groups[2].Value.Replace('\\', '/');
        var abas = new List<(string Nome, string Arquivo)>();
        foreach (Match m in Regex.Matches(wb, @"name=""([^""]+)""[^>]*r:id=""([^""]+)"""))
        {
            if (!alvos.TryGetValue(m.Groups[2].Value, out var alvo)) continue;
            if (!alvo.StartsWith("xl/", StringComparison.OrdinalIgnoreCase)) alvo = "xl/" + alvo.TrimStart('/');
            abas.Add((Decode(m.Groups[1].Value), alvo));
        }
        return abas;
    }

    private static string Texto(ZipArchive zip, string nome)
    {
        var entrada = zip.GetEntry(nome) ?? zip.Entries.FirstOrDefault(e => e.FullName.Replace('\\', '/').Equals(nome, StringComparison.OrdinalIgnoreCase));
        if (entrada == null) return "";
        using var reader = new StreamReader(entrada.Open(), Encoding.UTF8);
        return reader.ReadToEnd();
    }

    private static List<string> SharedStrings(string xml)
    {
        var lista = new List<string>();
        var i = 0;
        while (i < xml.Length)
        {
            var s = xml.IndexOf("<si>", i, StringComparison.Ordinal);
            if (s < 0) s = xml.IndexOf("<si ", i, StringComparison.Ordinal);
            if (s < 0) break;
            var e = xml.IndexOf("</si>", s, StringComparison.Ordinal);
            if (e < 0) break;
            var bloco = xml.Substring(s, e - s);
            var texto = new StringBuilder();
            var t = 0;
            while (t < bloco.Length)
            {
                var a = bloco.IndexOf("<t", t, StringComparison.Ordinal);
                if (a < 0) break;
                var gt = bloco.IndexOf('>', a);
                if (gt < 0) break;
                var close = bloco.IndexOf("</t>", gt, StringComparison.Ordinal);
                if (close < 0) break;
                texto.Append(Decode(bloco.Substring(gt + 1, close - gt - 1)));
                t = close + 4;
            }
            lista.Add(texto.ToString());
            i = e + 5;
        }
        return lista;
    }

    private static List<Linha> Linhas(string xml)
    {
        var linhas = new List<Linha>();
        if (string.IsNullOrEmpty(xml)) return linhas;
        var i = 0;
        while (i < xml.Length)
        {
            var s = xml.IndexOf("<row ", i, StringComparison.Ordinal);
            if (s < 0) break;
            var gt = xml.IndexOf('>', s);
            if (gt < 0) break;
            var fim = xml.IndexOf("</row>", gt, StringComparison.Ordinal);
            if (fim < 0) break;
            var cabeca = xml.Substring(s, gt - s);
            var numero = Regex.Match(cabeca, @"r=""(\d+)""");
            var n = numero.Success ? int.Parse(numero.Groups[1].Value, CultureInfo.InvariantCulture) : linhas.Count + 1;
            linhas.Add(new Linha(n, Celulas(xml.Substring(gt + 1, fim - gt - 1))));
            i = fim + 6;
        }
        return linhas;
    }

    private static Dictionary<string, Celula> Celulas(string chunk)
    {
        var mapa = new Dictionary<string, Celula>(StringComparer.Ordinal);
        var i = 0;
        while (i < chunk.Length)
        {
            var s = chunk.IndexOf("<c r=\"", i, StringComparison.Ordinal);
            if (s < 0) break;
            var q2 = chunk.IndexOf('"', s + 6);
            if (q2 < 0) break;
            var referencia = chunk.Substring(s + 6, q2 - s - 6);
            var coluna = Regex.Replace(referencia, "[0-9]", "");
            var gt = chunk.IndexOf('>', q2);
            if (gt < 0) break;
            if (chunk[gt - 1] == '/')
            {
                i = gt + 1;
                continue;
            }
            var fim = chunk.IndexOf("</c>", gt, StringComparison.Ordinal);
            if (fim < 0) break;
            if (coluna.Length == 1 && coluna[0] is >= 'A' and <= 'Z')
            {
                var tag = chunk.Substring(q2, gt - q2);
                var interno = chunk.Substring(gt + 1, fim - gt - 1);
                var estilo = 0;
                var indiceEstilo = Regex.Match(tag, @"s=""(\d+)""");
                if (indiceEstilo.Success) estilo = int.Parse(indiceEstilo.Groups[1].Value, CultureInfo.InvariantCulture);
                var v0 = interno.IndexOf("<v>", StringComparison.Ordinal);
                if (v0 >= 0)
                {
                    var v1 = interno.IndexOf("</v>", v0, StringComparison.Ordinal);
                    if (v1 > v0)
                    {
                        mapa[coluna] = new Celula(tag.Contains("t=\"s\"", StringComparison.Ordinal), Decode(interno.Substring(v0 + 3, v1 - v0 - 3)), estilo);
                    }
                }
            }
            i = fim + 4;
        }
        return mapa;
    }

    private static Montagem MontarChamados(List<Linha> linhas, List<string> strings, Dictionary<int, string> cores)
    {
        var colunas = Cabecalhos(linhas, strings);
        string Col(string nome) => colunas[nome];
        var rascunhos = new List<Rascunho>();
        var tecnicos = new Dictionary<string, TecnicoContagem>(StringComparer.Ordinal);
        foreach (var linha in linhas)
        {
            if (linha.N == 1) continue;
            var c = linha.Celulas;
            var cliente = TextoUtil(TextoDe(c, Col("cliente"), strings));
            var farmacia = TextoUtil(TextoDe(c, Col("farmacia"), strings));
            var assunto = TextoUtil(TextoDe(c, Col("assunto"), strings));
            var telefone = Digitos(TextoDe(c, Col("telefone"), strings));
            if (cliente.Length == 0 && farmacia.Length == 0 && assunto.Length == 0 && telefone.Length == 0) continue;
            if (Regex.IsMatch(cliente, @"^(cliente|farmacia|assunto)$", RegexOptions.IgnoreCase)) continue;

            var ok = TextoDe(c, Col("ok"), strings);
            var tecnico = TextoDe(c, Col("tecnico"), strings);
            var obs = TextoDe(c, Col("obs"), strings);
            var marcador = TextoDe(c, Col("marca"), strings);
            var dia = Inteiro(TextoDe(c, Col("dia"), strings));
            var mes = Inteiro(TextoDe(c, Col("mes"), strings));
            var ticket = TextoDe(c, Col("ticket"), strings);
            var codigo = TextoDe(c, Col("codigo"), strings);
            if (!(dia is >= 1 and <= 31 && mes is >= 1 and <= 12))
            {
                var m = Inteiro(ticket);
                var n = Inteiro(codigo);
                if (m is >= 1 and <= 31 && n is >= 1 and <= 12)
                {
                    dia = m;
                    mes = n;
                    ticket = "";
                    codigo = "";
                }
                else
                {
                    dia = null;
                    mes = null;
                }
            }
            var nota = DataNaNota(obs);
            var agCol = TextoDe(c, Col("agenda"), strings);
            var agNum = ExcelData(Numero(c, Col("agenda")));
            var agendamento = agNum.Length > 0 ? agNum : Regex.IsMatch(agCol, @"^\d{4}-\d{2}-\d{2}") ? agCol : "";
            var horaInicio = HoraDe(c, Col("inicio"), strings);
            var blob = assunto + " " + obs + " " + horaInicio + " " + marcador;
            RegistrarTecnico(tecnicos, tecnico);
            rascunhos.Add(new Rascunho
            {
                DiaCol = dia,
                MesCol = mes,
                DiaNota = nota?.Dia,
                MesNota = nota?.Mes,
                Ok = ok,
                Cor = CorDaLinha(c, cores, Col("farmacia"), Col("cliente"), Col("assunto"), Col("ok")),
                Chamado = new Chamado
                {
                    Id = "imp-" + linha.N,
                    Protocolo = "AT-" + (rascunhos.Count + 1).ToString("00000", CultureInfo.InvariantCulture),
                    OrigemLinha = linha.N,
                    Canal = CanalDe(blob),
                    CanalInformado = false,
                    HoraInicio = horaInicio,
                    HoraFim = HoraDe(c, Col("fim"), strings),
                    Status = "aberto",
                    Cliente = cliente,
                    Farmacia = farmacia,
                    Codigo = codigo,
                    Tecnico = tecnico,
                    Assunto = assunto,
                    Ddd = Digitos(TextoDe(c, Col("ddd"), strings)),
                    Telefone = telefone,
                    Telefone2 = Digitos(TextoDe(c, Col("telefone2"), strings)),
                    Obs = obs,
                    Agendamento = agendamento,
                    Prioridade = Regex.IsMatch(assunto + " " + obs, "urgente", RegexOptions.IgnoreCase) ? "alta" : "normal",
                    Marcador = Regex.IsMatch(marcador, "^x$", RegexOptions.IgnoreCase) ? "X" : marcador,
                    Ticket = ticket,
                    Historico = obs.Length == 0 ? [] : [new Nota { Texto = obs, Tecnico = tecnico }],
                    CriadoEm = "",
                    AtualizadoEm = ""
                }
            });
        }

        AplicarDatas(rascunhos);
        foreach (var item in rascunhos)
        {
            var c = item.Chamado;
            var hora = Regex.IsMatch(c.HoraInicio, @"^\d{2}:\d{2}$") ? c.HoraInicio : "08:00";
            c.CriadoEm = c.Data.Length > 0 ? c.Data + "T" + hora : "";
            c.AtualizadoEm = c.Data;
            if (c.Historico.Count > 0) c.Historico[0].Em = c.Data;
            c.Status = item.Cor switch
            {
                "azul" => "concluido",
                "branco" => "aberto",
                _ => StatusDe(item.Ok, c.Obs, c.Agendamento, c.Data)
            };
        }

        var nomes = tecnicos.Values
            .OrderByDescending(t => t.Quantidade)
            .Select(t => Titulo(t.Nome))
            .ToList();
        return new Montagem(rascunhos.Select(r => r.Chamado).ToList(), nomes);
    }

    private static void AplicarDatas(List<Rascunho> chamados)
    {
        var fonte = chamados.Select(c => c.MesCol ?? c.MesNota).ToArray();
        var suave = new int?[chamados.Count];
        for (var i = 0; i < chamados.Count; i++)
        {
            var fatia = new List<int>();
            var inicio = Math.Max(0, i - 25);
            var fim = Math.Min(fonte.Length, i + 26);
            for (var j = inicio; j < fim; j++)
                if (fonte[j] is int mes) fatia.Add(mes);
            suave[i] = Modo(fatia);
        }

        var ano = 2024;
        int? comprometido = null;
        var memoria = new List<int>();
        var ultima = "";
        var ultimoIncremento = -1000;
        for (var i = 0; i < chamados.Count; i++)
        {
            var c = chamados[i];
            var consenso = suave[i];
            if (consenso is int consensoMes)
            {
                memoria.Add(consensoMes);
                if (memoria.Count > 180) memoria.RemoveAt(0);
            }
            var tendencia = Modo(memoria);
            if (tendencia is int mesTendencia && comprometido >= 11 && mesTendencia <= 2 && i - ultimoIncremento > 500)
            {
                ano++;
                ultimoIncremento = i;
            }
            if (tendencia is int novoComprometido) comprometido = novoComprometido;

            int? dia = c.DiaCol;
            int? mes = c.MesCol;
            if (mes == null && c.MesNota is int mesNota)
            {
                dia = c.DiaNota;
                mes = mesNota;
            }
            if (mes is int mesLinha && consenso is int consensoAtual && mesLinha != consensoAtual)
            {
                var vizinho = Math.Abs(mesLinha - consensoAtual) <= 1 || (mesLinha <= 2 && consensoAtual >= 11) || (consensoAtual <= 2 && mesLinha >= 11);
                if (!vizinho) mes = consensoAtual;
            }
            mes ??= consenso;
            var anoLinha = ano;
            if (mes >= 10 && comprometido <= 4) anoLinha = ano - 1;
            if (dia is int diaValor && mes is int mesValor)
            {
                var maximo = DateTime.DaysInMonth(anoLinha, mesValor);
                if (diaValor >= 1 && diaValor <= maximo)
                {
                    c.Chamado.Data = anoLinha + "-" + mesValor.ToString("00") + "-" + diaValor.ToString("00");
                    ultima = c.Chamado.Data;
                }
            }
            if (c.Chamado.Data.Length == 0 && ultima.Length > 0) c.Chamado.Data = ultima;
            if (c.Chamado.Obs.Length > 0)
            {
                var nota = DataNaNota(c.Chamado.Obs);
                if (nota is DiaMes dataNota && c.Chamado.Agendamento.Length == 0)
                    c.Chamado.Agendamento = Agendamento(c.Chamado.Obs, anoLinha, mes ?? dataNota.Mes, dataNota.Dia);
            }
        }
    }

    private static string StatusDe(string ok, string obs, string agendamento, string dataIso)
    {
        var concluidoNota = Regex.IsMatch(obs, @"j[aá] foi resolvid|j[aá] resolveu|n[aã]o precisa mais|cancelou", RegexOptions.IgnoreCase);
        if (Regex.IsMatch(ok.Trim(), "^ok$", RegexOptions.IgnoreCase) || concluidoNota) return "concluido";
        var hoje = DateTime.Today;
        if (agendamento.Length > 0 && DateTime.TryParse(agendamento, out var quando) && quando.Date >= hoje) return "agendado";
        if (dataIso.Length > 0 && DateTime.TryParse(dataIso, out var data))
        {
            if ((hoje - data.Date).TotalDays > 21) return "concluido";
        }
        else return "concluido";
        if (Regex.IsMatch(obs, @"ligar|retorn|ngm atende|ningu[eé]m atende|n[aã]o atendeu|remarcar", RegexOptions.IgnoreCase)) return "retorno";
        return "aberto";
    }

    private static string Agendamento(string obs, int ano, int mes, int dia)
    {
        var s = obs ?? "";
        var m = Regex.Match(s, @"ligar\s+dia\s+(\d{1,2})\s*/\s*(\d{1,2})(?:\s*(?:as|às|a)?\s*(\d{1,2})\s*(?:[:h]\s*(\d{2}))?)?", RegexOptions.IgnoreCase);
        if (!m.Success) m = Regex.Match(s, @"ligar\s+(?:as|às|após as|a partir das|a partir de)\s*(\d{1,2})\s*(?:[:h]\s*(\d{2}))?", RegexOptions.IgnoreCase);
        if (!m.Success) return "";
        var dd = dia;
        var mm = mes;
        var anoAg = ano;
        var hh = "09";
        var mi = "00";
        if (m.Groups[2].Success && int.TryParse(m.Groups[2].Value, out var mesLido) && mesLido <= 12 &&
            int.TryParse(m.Groups[1].Value, out var diaLido) && diaLido <= 31 && m.Value.Contains("dia", StringComparison.OrdinalIgnoreCase))
        {
            dd = diaLido;
            mm = mesLido;
            if (m.Groups[3].Success) hh = m.Groups[3].Value.PadLeft(2, '0');
            if (m.Groups[4].Success) mi = m.Groups[4].Value.PadLeft(2, '0');
        }
        else if (int.TryParse(m.Groups[1].Value, out var hora) && hora <= 23)
        {
            hh = hora.ToString("00");
            mi = m.Groups[2].Success ? m.Groups[2].Value.PadLeft(2, '0') : "00";
        }
        if (mm <= 3 && mes >= 10) anoAg = ano + 1;
        if (dd < 1 || mm < 1 || mm > 12) return "";
        if (dd > DateTime.DaysInMonth(anoAg, mm)) return "";
        return anoAg + "-" + mm.ToString("00") + "-" + dd.ToString("00") + "T" + hh + ":" + mi;
    }

    private static string CanalDe(string texto)
    {
        var s = texto.ToLowerInvariant();
        if (Regex.IsMatch(s, @"whats|wpp|\bzap\b|vschat|vs-chat|vs chat")) return "whatsapp";
        if (Regex.IsMatch(s, @"e-?mail")) return "email";
        if (Regex.IsMatch(s, @"pedido interno|internamente")) return "interno";
        if (Regex.IsMatch(s, @"liguei|liga[cç][aã]o|ngm atende|ningu[eé]m atende|n[aã]o atendeu")) return "ligacao";
        return "";
    }

    private static RamaisLidos RamaisDe(List<Linha> linhas, List<string> strings)
    {
        var lidos = new RamaisLidos();
        foreach (var linha in linhas)
        {
            var codigo = TextoDe(linha.Celulas, "A", strings);
            var nome = TextoDe(linha.Celulas, "B", strings).Trim();
            var aviso = TextoDe(linha.Celulas, "G", strings).Trim();
            if (Regex.IsMatch(codigo, @"^\d{3,5}$") && nome.Length > 0 && !Regex.IsMatch(codigo + nome, "ramal|hora atual", RegexOptions.IgnoreCase))
            {
                lidos.Itens.Add(new RamalInfo
                {
                    Numero = codigo,
                    Nome = nome,
                    Almoco = TextoDe(linha.Celulas, "C", strings).Trim(),
                    Ddd = Digitos(TextoDe(linha.Celulas, "H", strings)),
                    Telefone = Digitos(TextoDe(linha.Celulas, "I", strings)),
                    Discagem = Digitos(TextoDe(linha.Celulas, "J", strings))
                });
            }
            else if (aviso.Length > 12 && !Regex.IsMatch(aviso, @"^(operadora|numero)", RegexOptions.IgnoreCase))
            {
                lidos.Dicas.Add(aviso);
            }
        }
        return lidos;
    }

    private static RamaisLidos RamaisPadrao()
    {
        var lidos = new RamaisLidos();
        lidos.Itens.AddRange(
        [
            new RamalInfo { Numero = "5000", Nome = "Recepção", Almoco = "11h às 13h", Ddd = "67", Telefone = "33869704", Discagem = "0216733869704" },
            new RamalInfo { Numero = "3122", Nome = "André", Almoco = "12h às 14h", Ddd = "19", Telefone = "30371616", Discagem = "0211930371616" },
            new RamalInfo { Numero = "3123", Nome = "Wigner", Almoco = "13h às 15h", Ddd = "24", Telefone = "999460879", Discagem = "02124999460879" },
            new RamalInfo { Numero = "3124", Nome = "Jefferson", Almoco = "12h às 14h", Ddd = "24", Telefone = "999460879", Discagem = "02124999460879" },
            new RamalInfo { Numero = "3125", Nome = "Rafael/Kevin", Almoco = "", Ddd = "48", Telefone = "988011100", Discagem = "02148988011100" },
            new RamalInfo { Numero = "3126", Nome = "Batista", Almoco = "11h às 13h", Ddd = "67", Telefone = "33255851", Discagem = "0216733255851" },
            new RamalInfo { Numero = "3127", Nome = "Macnair", Almoco = "12h às 14h", Ddd = "51", Telefone = "996467879", Discagem = "02151996467879" }
        ]);
        lidos.Dicas.Add("Metropolitano: não precisa do DDD nem do 021.");
        lidos.Dicas.Add("Interurbano: disque 021 + DDD + número. Exemplo: 0215134881010.");
        return lidos;
    }

    private static List<string> FrasesDaAba(List<Linha> linhas, List<string> strings)
    {
        var frases = new List<string>();
        var vistos = new HashSet<string>(StringComparer.Ordinal);
        foreach (var linha in linhas)
        {
            foreach (var celula in linha.Celulas.Values)
            {
                var t = TextoCelula(celula, strings).Replace("\r", "").Trim();
                if (t.Length < 30 || !vistos.Add(t)) continue;
                frases.Add(t);
            }
        }
        return frases;
    }

    private static void RegistrarTecnico(Dictionary<string, TecnicoContagem> mapa, string nome)
    {
        var limpo = Regex.Replace(nome ?? "", @"\s+", " ").Trim();
        if (limpo.Length < 2 || Regex.IsMatch(limpo, @"^\d+$")) return;
        var partes = limpo.Contains('/') ? limpo.Split('/', StringSplitOptions.TrimEntries | StringSplitOptions.RemoveEmptyEntries) : [limpo];
        foreach (var parte in partes)
        {
            if (parte.Length < 2 || Regex.IsMatch(parte, @"^\d+$")) continue;
            var chave = parte.ToUpper(new CultureInfo("pt-BR")).Normalize(NormalizationForm.FormD);
            chave = new string(chave.Where(ch => CharUnicodeInfo.GetUnicodeCategory(ch) != UnicodeCategory.NonSpacingMark).ToArray());
            if (!mapa.TryGetValue(chave, out var atual))
            {
                atual = new TecnicoContagem(parte, 0);
                mapa[chave] = atual;
            }
            atual.Quantidade++;
            var maiusculas = parte.ToUpper(new CultureInfo("pt-BR"));
            if (parte != maiusculas && atual.Nome == atual.Nome.ToUpper(new CultureInfo("pt-BR"))) atual.Nome = parte;
        }
    }

    private static string Titulo(string nome)
    {
        var cultura = new CultureInfo("pt-BR");
        var chars = nome.ToLower(cultura).ToCharArray();
        var capitalizar = true;
        for (var i = 0; i < chars.Length; i++)
        {
            if (chars[i] is ' ' or '/')
            {
                capitalizar = true;
                continue;
            }
            if (capitalizar && char.IsLetter(chars[i]))
            {
                chars[i] = char.ToUpper(chars[i], cultura);
                capitalizar = false;
            }
        }
        return new string(chars);
    }

    private static DiaMes? DataNaNota(string obs)
    {
        var m = Regex.Match(obs ?? "", @"(\d{1,2})\s*/\s*(\d{1,2})");
        if (!m.Success) return null;
        var dia = int.Parse(m.Groups[1].Value, CultureInfo.InvariantCulture);
        var mes = int.Parse(m.Groups[2].Value, CultureInfo.InvariantCulture);
        if (dia < 1 || dia > 31 || mes < 1 || mes > 12) return null;
        return new DiaMes(dia, mes);
    }

    private static int? Modo(List<int> valores)
    {
        if (valores.Count == 0) return null;
        int? melhor = null;
        var quantidade = -1;
        foreach (var grupo in valores.GroupBy(v => v))
        {
            var qtd = grupo.Count();
            if (qtd > quantidade)
            {
                melhor = grupo.Key;
                quantidade = qtd;
            }
        }
        return melhor;
    }

    private static string TextoDe(Dictionary<string, Celula> celulas, string coluna, List<string> strings) =>
        celulas.TryGetValue(coluna, out var celula) ? TextoCelula(celula, strings) : "";

    private static string TextoCelula(Celula? celula, List<string> strings)
    {
        if (celula is not Celula valor) return "";
        if (valor.Compartilhada && int.TryParse(valor.Bruto, NumberStyles.Integer, CultureInfo.InvariantCulture, out var indice) && indice >= 0 && indice < strings.Count)
            return strings[indice].Trim();
        return valor.Bruto.Trim();
    }

    private static double? Numero(Dictionary<string, Celula> celulas, string coluna)
    {
        if (!celulas.TryGetValue(coluna, out var celula) || celula.Compartilhada) return null;
        return double.TryParse(celula.Bruto, NumberStyles.Float, CultureInfo.InvariantCulture, out var n) ? n : null;
    }

    private static int? Inteiro(string valor)
    {
        if (string.IsNullOrWhiteSpace(valor)) return null;
        if (!double.TryParse(valor.Replace(',', '.'), NumberStyles.Float, CultureInfo.InvariantCulture, out var n)) return null;
        if (Math.Abs(n - Math.Round(n)) > 0.0001) return null;
        return (int)Math.Round(n);
    }

    private static string TextoUtil(string valor)
    {
        var t = (valor ?? "").Trim();
        if (t.Length == 0) return "";
        if (Regex.IsMatch(t, @"^\d+(\.\d+)?$") && double.TryParse(t, NumberStyles.Float, CultureInfo.InvariantCulture, out var n) && n > 20000 && n < 80000)
            return "";
        return t;
    }

    private static string HoraDe(Dictionary<string, Celula> celulas, string coluna, List<string> strings)
    {
        var convertida = ExcelHora(Numero(celulas, coluna));
        if (convertida.Length > 0) return convertida;
        var t = TextoDe(celulas, coluna, strings);
        if (Regex.IsMatch(t, @"^\d+\.\d+$") && double.TryParse(t, NumberStyles.Float, CultureInfo.InvariantCulture, out var n))
            return ExcelHora(n);
        return t;
    }

    private static string ExcelData(double? serial)
    {
        if (serial is not double n || n < 20000 || n > 80000) return "";
        var data = new DateTime(1899, 12, 30, 0, 0, 0, DateTimeKind.Utc).AddDays(Math.Floor(n));
        return data.ToString("yyyy-MM-dd", CultureInfo.InvariantCulture);
    }

    private static string ExcelHora(double? serial)
    {
        if (serial is not double n || n <= 0 || n >= 1) return "";
        var total = (int)Math.Round(n * 24 * 60);
        var h = total / 60 % 24;
        var m = total % 60;
        return h.ToString("00") + ":" + m.ToString("00");
    }

    private static string Digitos(string valor)
    {
        if (string.IsNullOrWhiteSpace(valor)) return "";
        if (double.TryParse(valor, NumberStyles.Float, CultureInfo.InvariantCulture, out var n) && !valor.Contains('.') && n > 1000)
            return Math.Round(n).ToString("0", CultureInfo.InvariantCulture);
        var chars = new char[valor.Length];
        var i = 0;
        foreach (var c in valor)
            if (char.IsDigit(c)) chars[i++] = c;
        return new string(chars, 0, i);
    }

    private static string Decode(string texto) =>
        Regex.Replace(texto, @"&#(\d+);|&#x([0-9a-fA-F]+);|&amp;|&lt;|&gt;|&quot;|&apos;", m =>
        {
            if (m.Groups[1].Success) return char.ConvertFromUtf32(int.Parse(m.Groups[1].Value, CultureInfo.InvariantCulture));
            if (m.Groups[2].Success) return char.ConvertFromUtf32(int.Parse(m.Groups[2].Value, NumberStyles.HexNumber, CultureInfo.InvariantCulture));
            return m.Value switch
            {
                "&amp;" => "&",
                "&lt;" => "<",
                "&gt;" => ">",
                "&quot;" => "\"",
                _ => "'"
            };
        });

    private readonly record struct Celula(bool Compartilhada, string Bruto, int Estilo);
    private readonly record struct Linha(int N, Dictionary<string, Celula> Celulas);
    private readonly record struct DiaMes(int Dia, int Mes);
    private sealed class TecnicoContagem(string nome, int quantidade)
    {
        public string Nome { get; set; } = nome;
        public int Quantidade { get; set; } = quantidade;
    }
    private sealed class Rascunho
    {
        public Chamado Chamado { get; set; } = new();
        public int? DiaCol { get; set; }
        public int? MesCol { get; set; }
        public int? DiaNota { get; set; }
        public int? MesNota { get; set; }
        public string Ok { get; set; } = "";
        public string Cor { get; set; } = "";
    }

    public sealed class ResultadoImportacao
    {
        public int Novos { get; set; }
        public int AtendidosPelaCor { get; set; }
        public int Reabertos { get; set; }
    }
    private sealed class RamaisLidos
    {
        public List<RamalInfo> Itens { get; } = [];
        public List<string> Dicas { get; } = [];
    }
    private sealed record Montagem(List<Chamado> Chamados, List<string> Tecnicos);
    private sealed record PlanilhaLida(List<Chamado> Chamados, List<string> Tecnicos, List<string> Frases, List<RamalInfo> Ramais, List<string> Dicas);
}
