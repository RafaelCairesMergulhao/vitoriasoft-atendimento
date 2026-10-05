using System.Text;
using VitoriaSoft.Atendimento.Modelo;
using VitoriaSoft.Atendimento.Servico;

namespace VitoriaSoft.Atendimento.Ui;

public sealed class AjustesView : UserControl, IAtualizavel
{
    private readonly IApp _app;
    private readonly TextBox _empresa = new() { Dock = DockStyle.Top };
    private readonly ComboBox _tecnico = new() { Dock = DockStyle.Top, DropDownStyle = ComboBoxStyle.DropDownList };
    private readonly ListBox _tecnicos = new() { Height = 140, Dock = DockStyle.Top };
    private readonly TextBox _vsuUrl = new() { Dock = DockStyle.Top, PlaceholderText = "Endereço do VSU Cloud, por exemplo https://cloud.empresa" };
    private readonly TextBox _vsuCaminho = new() { Dock = DockStyle.Top, PlaceholderText = "Caminho da consulta, por exemplo /api/empresas" };
    private readonly TextBox _vsuChave = new() { Dock = DockStyle.Top, UseSystemPasswordChar = true, PlaceholderText = "Chave de API" };
    private readonly ComboBox _vsuEsquema = new() { Dock = DockStyle.Top, DropDownStyle = ComboBoxStyle.DropDownList };
    private readonly Label _vsuAviso = new() { Dock = DockStyle.Top, Height = 48, ForeColor = Tema.Mudo };
    private readonly ListBox _status = new() { Height = 120, Dock = DockStyle.Top };
    private readonly ListBox _canais = new() { Height = 120, Dock = DockStyle.Top };
    private readonly ListBox _frases = new() { Height = 140, Dock = DockStyle.Top };
    private readonly ListBox _campos = new() { Height = 100, Dock = DockStyle.Top };
    private bool _carregando;

    public AjustesView(IApp app)
    {
        _app = app;
        Dock = DockStyle.Fill;
        BackColor = Tema.Fundo;
        AutoScroll = true;
        var blocos = new List<Control>
        {
            Tema.TituloDe("Personalizar"),
            Tema.Subtitulo("O recepcionista altera canais, situações, técnicos, frases e a ligação com o VSU Cloud."),
            Rotulo("VSU Cloud"),
            _vsuUrl,
            _vsuCaminho,
            _vsuEsquema,
            _vsuChave,
            BotoesVsu(),
            _vsuAviso,
            Rotulo("Nome exibido"),
            _empresa,
            Rotulo("Técnico deste computador"),
            _tecnico,
            BotoesDados(),
            Rotulo("Canais de entrada"),
            _canais,
            LinhaTexto("novo-canal", "Adicionar canal", AdicionarCanal),
            Rotulo("Situações do chamado"),
            _status,
            LinhaTexto("nova-situacao", "Adicionar situação", AdicionarStatus),
            Rotulo("Técnicos"),
            _tecnicos,
            LinhaTexto("novo-tecnico", "Adicionar técnico", AdicionarTecnico),
            Rotulo("Campos extras do chamado"),
            _campos,
            LinhaCampo(),
            Rotulo("Frases prontas"),
            _frases,
            LinhaTexto("nova-frase", "Adicionar frase", AdicionarFrase)
        };
        foreach (var bloco in Enumerable.Reverse(blocos))
        {
            if (bloco.Dock == DockStyle.None) bloco.Dock = DockStyle.Top;
            Controls.Add(bloco);
        }
        _empresa.Leave += (_, _) =>
        {
            if (_carregando) return;
            _app.Banco.Config.Empresa = _empresa.Text.Trim();
            _app.Salvar();
        };
        _tecnico.SelectedIndexChanged += (_, _) =>
        {
            if (_carregando) return;
            _app.Banco.Config.TecnicoLocal = _tecnico.SelectedItem as string ?? "";
            _app.Salvar();
        };
        _vsuEsquema.Items.AddRange(["Bearer", "ApiKey", "Basic"]);
        _canais.DoubleClick += (_, _) => RenomearCanal();
        _status.DoubleClick += (_, _) => RenomearStatus();
        _tecnicos.DoubleClick += (_, _) => RenomearTecnico();
        _frases.DoubleClick += (_, _) => EditarFrase();
    }

    public void Atualizar()
    {
        _carregando = true;
        _empresa.Text = _app.Banco.Config.Empresa;
        _tecnico.Items.Clear();
        _tecnico.Items.Add("");
        foreach (var t in _app.Banco.Config.Tecnicos) _tecnico.Items.Add(t);
        var local = _app.Banco.Config.TecnicoLocal ?? "";
        _tecnico.SelectedItem = _tecnico.Items.Contains(local) ? local : "";
        _vsuUrl.Text = _app.Banco.Config.VsuUrl;
        _vsuCaminho.Text = _app.Banco.Config.VsuCaminho;
        _vsuChave.Text = _app.Banco.Config.VsuChave;
        var esquema = string.IsNullOrWhiteSpace(_app.Banco.Config.VsuEsquema) ? "Bearer" : _app.Banco.Config.VsuEsquema;
        _vsuEsquema.SelectedItem = _vsuEsquema.Items.Contains(esquema) ? esquema : "Bearer";
        _vsuAviso.Text = "A chave fica só neste computador. A busca no VSU Cloud só acontece quando você clicar. O que já foi corrigido na recepção não é substituído.";
        Preencher(_canais, _app.Banco.Config.Canais.Select(c => c.Nome));
        Preencher(_status, _app.Banco.Config.Status.Select(s => s.Nome));
        Preencher(_tecnicos, _app.Banco.Config.Tecnicos);
        Preencher(_frases, _app.Banco.Config.Frases.Select(f => f.Length > 90 ? f[..90] + "…" : f));
        Preencher(_campos, _app.Banco.Config.CamposExtras.Select(c => c.Nome + " (" + c.Tipo + ")"));
        _carregando = false;
    }

    private Control BotoesDados()
    {
        var faixa = new FlowLayoutPanel { Height = 48, WrapContents = false };
        var exportar = Tema.Botao("Exportar planilha", Color.White, Tema.Tinta);
        var importar = Tema.Botao("Trazer planilha do dia", Color.White, Tema.Tinta);
        var removerTecnico = Tema.Botao("Remover técnico selecionado", Color.White, Tema.Perigo);
        exportar.Width = 160;
        importar.Width = 200;
        removerTecnico.Width = 210;
        exportar.Click += (_, _) => Exportar();
        importar.Click += (_, _) => Importar();
        removerTecnico.Click += (_, _) => RemoverSelecionado(_tecnicos, _app.Banco.Config.Tecnicos);
        faixa.Controls.Add(exportar);
        faixa.Controls.Add(importar);
        faixa.Controls.Add(removerTecnico);
        return faixa;
    }

    private void Exportar()
    {
        using var dialogo = new SaveFileDialog
        {
            Filter = "Planilha CSV (*.csv)|*.csv",
            FileName = "atendimento-vitoria-soft.csv"
        };
        if (dialogo.ShowDialog(this) != DialogResult.OK) return;
        var sb = new StringBuilder();
        sb.Append('\uFEFF');
        sb.AppendLine("Protocolo;Data;Hora início;Hora fim;Status;Canal;Cliente;Farmácia;Código;Técnico;Assunto;DDD;Telefone;Telefone 2;Observações;Agendamento;Prioridade");
        foreach (var c in _app.Banco.Chamados)
        {
            sb.AppendLine(string.Join(';', new[]
            {
                c.Protocolo, Servico.Consultas.DataBr(c.Data), c.HoraInicio, c.HoraFim,
                _app.Consultas.NomeStatus(c.Status), _app.Consultas.NomeCanal(c.Canal),
                c.Cliente, c.Farmacia, c.Codigo, c.Tecnico, c.Assunto, c.Ddd, c.Telefone, c.Telefone2,
                c.Obs, c.Agendamento, c.Prioridade
            }.Select(Csv)));
        }
        File.WriteAllText(dialogo.FileName, sb.ToString(), new UTF8Encoding(true));
        MessageBox.Show("Planilha exportada.", "Vitória Soft Atendimento");
    }

    public static string TextoImportacao(Servico.ImportadorPlanilha.ResultadoImportacao resultado) =>
        resultado.Novos + " chamados novos.\n"
        + resultado.AtendidosPelaCor + " linhas azuis marcadas como atendidas.\n"
        + resultado.Reabertos + " linhas brancas voltaram para a fila, porque ainda não foram atendidas.";

    private void Importar()
    {
        using var dialogo = new OpenFileDialog { Filter = "Planilha Excel (*.xlsx)|*.xlsx" };
        if (dialogo.ShowDialog(this) != DialogResult.OK) return;
        try
        {
            UseWaitCursor = true;
            var resultado = Servico.ImportadorPlanilha.Mesclar(_app.Banco, dialogo.FileName);
            _app.Salvar();
            MessageBox.Show(TextoImportacao(resultado), "Vitória Soft Atendimento");
            Atualizar();
        }
        catch (Exception ex)
        {
            MessageBox.Show("Não foi possível importar a planilha.\n\n" + ex.Message, "Vitória Soft Atendimento");
        }
        finally
        {
            UseWaitCursor = false;
        }
    }

    private Control BotoesVsu()
    {
        var faixa = new FlowLayoutPanel { Height = 48, WrapContents = false };
        var salvar = Tema.Botao("Salvar conexão", Tema.Teal, Color.White);
        var testar = Tema.Botao("Testar chave", Color.White, Tema.Tinta);
        var buscar = Tema.Botao("Buscar empresas", Color.White, Tema.Tinta);
        salvar.Width = 150;
        testar.Width = 130;
        buscar.Width = 160;
        salvar.Click += (_, _) =>
        {
            GuardarVsu();
            MessageBox.Show("Conexão do VSU Cloud salva neste computador.", "Vitória Soft Atendimento");
        };
        testar.Click += async (_, _) => await ConsultarVsu(false);
        buscar.Click += async (_, _) => await ConsultarVsu(true);
        faixa.Controls.Add(salvar);
        faixa.Controls.Add(testar);
        faixa.Controls.Add(buscar);
        return faixa;
    }

    private void GuardarVsu()
    {
        _app.Banco.Config.VsuUrl = _vsuUrl.Text.Trim();
        _app.Banco.Config.VsuCaminho = _vsuCaminho.Text.Trim();
        _app.Banco.Config.VsuChave = _vsuChave.Text.Trim();
        _app.Banco.Config.VsuEsquema = _vsuEsquema.SelectedItem as string ?? "Bearer";
        _app.Salvar();
    }

    private async Task ConsultarVsu(bool importar)
    {
        GuardarVsu();
        UseWaitCursor = true;
        try
        {
            var resultado = await VsuCliente.BuscarEmpresasAsync(_app.Banco.Config);
            if (!resultado.Ok)
            {
                MessageBox.Show(resultado.Mensagem, "Vitória Soft Atendimento");
                return;
            }
            if (!importar)
            {
                MessageBox.Show("A chave respondeu. " + resultado.Mensagem, "Vitória Soft Atendimento");
                return;
            }
            var (novas, jaExistiam) = VsuCliente.Mesclar(_app.Banco, resultado.Empresas);
            _app.Salvar();
            MessageBox.Show(novas + " empresas novas entraram no cadastro. " + jaExistiam + " já existiam e foram mantidas como estão.", "Vitória Soft Atendimento");
        }
        finally
        {
            UseWaitCursor = false;
        }
    }

    private void AdicionarStatus(string nome)
    {
        var id = new string(nome.ToLowerInvariant().Select(c => char.IsLetterOrDigit(c) ? c : '-').ToArray()).Trim('-');
        if (id.Length == 0) id = "status";
        if (_app.Banco.Config.Status.Any(s => s.Id == id)) id += "-" + _app.Banco.Config.Status.Count;
        _app.Banco.Config.Status.Add(new Opcao { Id = id, Nome = nome, Cor = "#0F6E62" });
        _app.Salvar();
        Atualizar();
    }

    private void RenomearCanal()
    {
        var i = _canais.SelectedIndex;
        if (i < 0 || i >= _app.Banco.Config.Canais.Count) return;
        var canal = _app.Banco.Config.Canais[i];
        var nome = Tema.Perguntar(this, "Canal", "Nome do canal", canal.Nome);
        if (string.IsNullOrWhiteSpace(nome)) return;
        canal.Nome = nome;
        _app.Salvar();
        Atualizar();
    }

    private void RenomearStatus()
    {
        var i = _status.SelectedIndex;
        if (i < 0 || i >= _app.Banco.Config.Status.Count) return;
        var status = _app.Banco.Config.Status[i];
        var nome = Tema.Perguntar(this, "Situação", "Nome da situação", status.Nome);
        if (string.IsNullOrWhiteSpace(nome)) return;
        status.Nome = nome;
        _app.Salvar();
        Atualizar();
    }

    private void RenomearTecnico()
    {
        var i = _tecnicos.SelectedIndex;
        if (i < 0 || i >= _app.Banco.Config.Tecnicos.Count) return;
        var atual = _app.Banco.Config.Tecnicos[i];
        var nome = Tema.Perguntar(this, "Técnico", "O novo nome entra nos chamados deste técnico.", atual);
        if (string.IsNullOrWhiteSpace(nome) || nome.Equals(atual, StringComparison.OrdinalIgnoreCase)) return;
        var n = _app.Consultas.RenomearTecnico(atual, nome);
        _app.Salvar();
        MessageBox.Show(n + " chamados atualizados.", "Vitória Soft Atendimento");
        Atualizar();
    }

    private void EditarFrase()
    {
        var i = _frases.SelectedIndex;
        if (i < 0 || i >= _app.Banco.Config.Frases.Count) return;
        var texto = Tema.Perguntar(this, "Frase pronta", "Texto da frase", _app.Banco.Config.Frases[i], true);
        if (string.IsNullOrWhiteSpace(texto)) return;
        _app.Banco.Config.Frases[i] = texto;
        _app.Salvar();
        Atualizar();
    }

    private void AdicionarCanal(string nome)
    {
        var id = nome.ToLowerInvariant().Replace(' ', '-');
        _app.Banco.Config.Canais.Add(new Opcao { Id = id, Nome = nome, Cor = "#0F6E62" });
        _app.Salvar();
        Atualizar();
    }

    private void AdicionarTecnico(string nome)
    {
        _app.Banco.Config.Tecnicos.Add(nome);
        _app.Salvar();
        Atualizar();
    }

    private void AdicionarFrase(string frase)
    {
        _app.Banco.Config.Frases.Add(frase);
        _app.Salvar();
        Atualizar();
    }

    private Control LinhaCampo()
    {
        var nome = new TextBox { Width = 180, PlaceholderText = "Nome do campo" };
        var tipo = new ComboBox { Width = 100, DropDownStyle = ComboBoxStyle.DropDownList };
        tipo.Items.AddRange(["texto", "lista", "data"]);
        tipo.SelectedIndex = 0;
        var opcoes = new TextBox { Width = 220, PlaceholderText = "Opções da lista, separadas por vírgula" };
        var botao = Tema.Botao("Adicionar campo", Tema.Teal, Color.White);
        botao.Click += (_, _) =>
        {
            if (string.IsNullOrWhiteSpace(nome.Text)) return;
            _app.Banco.Config.CamposExtras.Add(new CampoExtra
            {
                Id = "c" + DateTimeOffset.Now.ToUnixTimeMilliseconds(),
                Nome = nome.Text.Trim(),
                Tipo = tipo.SelectedItem?.ToString() ?? "texto",
                Opcoes = opcoes.Text.Trim()
            });
            nome.Clear();
            _app.Salvar();
            Atualizar();
        };
        var remover = Tema.Botao("Remover campo", Color.White, Tema.Perigo);
        remover.Click += (_, _) =>
        {
            var i = _campos.SelectedIndex;
            if (i < 0 || i >= _app.Banco.Config.CamposExtras.Count) return;
            _app.Banco.Config.CamposExtras.RemoveAt(i);
            _app.Salvar();
            Atualizar();
        };
        var faixa = new FlowLayoutPanel { Height = 44, WrapContents = false };
        faixa.Controls.Add(nome);
        faixa.Controls.Add(tipo);
        faixa.Controls.Add(opcoes);
        faixa.Controls.Add(botao);
        faixa.Controls.Add(remover);
        return faixa;
    }

    private Control LinhaTexto(string _, string rotulo, Action<string> aoAdicionar)
    {
        var caixa = new TextBox { Width = 280 };
        var botao = Tema.Botao(rotulo, Tema.Teal, Color.White);
        botao.AutoSize = true;
        botao.Click += (_, _) =>
        {
            var texto = caixa.Text.Trim();
            if (texto.Length == 0) return;
            caixa.Clear();
            aoAdicionar(texto);
        };
        var faixa = new FlowLayoutPanel { Height = 44, WrapContents = false };
        faixa.Controls.Add(caixa);
        faixa.Controls.Add(botao);
        if (rotulo.Contains("canal"))
        {
            var remover = Tema.Botao("Remover canal", Color.White, Tema.Perigo);
            remover.Click += (_, _) => RemoverSelecionado(_canais, _app.Banco.Config.Canais);
            faixa.Controls.Add(remover);
        }
        if (rotulo.Contains("frase"))
        {
            var remover = Tema.Botao("Remover frase", Color.White, Tema.Perigo);
            remover.Click += (_, _) => RemoverSelecionado(_frases, _app.Banco.Config.Frases);
            faixa.Controls.Add(remover);
        }
        if (rotulo.Contains("situa"))
        {
            var remover = Tema.Botao("Remover situação", Color.White, Tema.Perigo);
            remover.Click += (_, _) => RemoverSelecionado(_status, _app.Banco.Config.Status);
            faixa.Controls.Add(remover);
        }
        return faixa;
    }

    private void RemoverSelecionado<T>(ListBox lista, List<T> dados)
    {
        var i = lista.SelectedIndex;
        if (i < 0 || i >= dados.Count) return;
        dados.RemoveAt(i);
        _app.Salvar();
        Atualizar();
    }

    private static void Preencher(ListBox lista, IEnumerable<string> itens)
    {
        lista.Items.Clear();
        foreach (var item in itens) lista.Items.Add(item);
    }

    private static string Csv(string? valor)
    {
        var texto = (valor ?? "").Replace("\"", "\"\"");
        return "\"" + texto.Replace("\r", " ").Replace("\n", " ") + "\"";
    }

    private static Label Rotulo(string texto) => new()
    {
        Text = texto,
        ForeColor = Tema.Mudo,
        Height = 24,
        Padding = new Padding(0, 8, 0, 0)
    };
}
