using VitoriaSoft.Atendimento.Modelo;
using VitoriaSoft.Atendimento.Servico;

namespace VitoriaSoft.Atendimento.Ui;

public sealed class FichaView : UserControl
{
    private readonly IApp _app;
    private readonly FlowLayoutPanel _canais = new() { Dock = DockStyle.Top, Height = 48, WrapContents = false };
    private readonly DateTimePicker _data = new() { Format = DateTimePickerFormat.Short, Width = 140 };
    private readonly TextBox _inicio = new() { Width = 80 };
    private readonly TextBox _fim = new() { Width = 80 };
    private readonly TextBox _farmacia = new() { Dock = DockStyle.Top };
    private readonly TextBox _cliente = new() { Width = 220 };
    private readonly TextBox _codigo = new() { Width = 120 };
    private readonly TextBox _tecnico = new() { Width = 180 };
    private readonly TextBox _assunto = new() { Dock = DockStyle.Top };
    private readonly TextBox _ddd = new() { Width = 60 };
    private readonly TextBox _tel = new() { Width = 140 };
    private readonly TextBox _tel2 = new() { Width = 140 };
    private readonly TextBox _obs = new() { Dock = DockStyle.Top, Multiline = true, Height = 120, ScrollBars = ScrollBars.Vertical };
    private readonly DateTimePicker _agenda = new() { Format = DateTimePickerFormat.Custom, CustomFormat = "dd/MM/yyyy HH:mm", Width = 180, ShowCheckBox = true };
    private readonly ComboBox _prioridade = new() { Width = 120, DropDownStyle = ComboBoxStyle.DropDownList };
    private readonly CheckBox _marca = new() { Text = "Marcador X", AutoSize = true };
    private readonly FlowLayoutPanel _status = new() { Dock = DockStyle.Top, Height = 44, WrapContents = true };
    private readonly FlowLayoutPanel _extras = new() { FlowDirection = FlowDirection.TopDown, WrapContents = false, AutoSize = true };
    private readonly Dictionary<string, Control> _editores = new();
    private readonly ListBox _historico = new() { Dock = DockStyle.Fill, Font = Tema.Fonte };
    private readonly ListBox _frases = new() { Dock = DockStyle.Bottom, Height = 160, Font = Tema.Fonte };
    private readonly TextBox _protocolo = new() { Width = 160 };
    private readonly Label _titulo = Tema.TituloDe("Novo chamado");
    private Chamado? _origem;
    private string _canal = "";
    private string _statusId = "aberto";
    private bool _carregando;

    public FichaView(IApp app)
    {
        _app = app;
        Dock = DockStyle.Fill;
        BackColor = Tema.Fundo;
        _prioridade.Items.AddRange(["Normal", "Alta"]);
        _farmacia.TextChanged += (_, _) => { if (!_carregando) AtualizarLado(); };
        _historico.DoubleClick += (_, _) =>
        {
            if (_historico.SelectedItem is LinhaHist linha) _app.Editar(linha.Chamado);
        };
        _frases.DoubleClick += (_, _) =>
        {
            if (_frases.SelectedItem is string frase)
                _obs.Text = string.IsNullOrWhiteSpace(_obs.Text) ? frase : _obs.Text.TrimEnd() + Environment.NewLine + frase;
        };

        var esquerda = new Panel { Dock = DockStyle.Fill, AutoScroll = true, Padding = new Padding(4, 0, 12, 0) };
        var blocos = new List<Control>
        {
            _titulo,
            Tema.Subtitulo("Escolha de onde veio o chamado e registre o que a farmácia precisa. Tudo nesta ficha pode ser corrigido."),
            CampoProtocolo(),
            _canais,
            Faixa(_data, "Data", _inicio, "Hora início", _fim, "Hora fim"),
            Rotulo("Farmácia"),
            _farmacia,
            Faixa(_cliente, "Cliente", _codigo, "Código", _tecnico, "Técnico"),
            Rotulo("Assunto"),
            _assunto,
            Faixa(_ddd, "DDD", _tel, "Telefone", _tel2, "Telefone 2"),
            AcoesTelefone(),
            Rotulo("Observações"),
            _obs,
            FaixaAgenda(),
            _marca,
            _extras,
            Rotulo("Situação"),
            _status,
            AcoesSalvar()
        };
        foreach (var bloco in Enumerable.Reverse(blocos))
        {
            bloco.Dock = DockStyle.Top;
            esquerda.Controls.Add(bloco);
        }

        var direita = new Panel { Dock = DockStyle.Right, Width = 340, BackColor = Tema.Cartao, Padding = new Padding(12) };
        var tituloFrases = new Label { Text = "Frases prontas", Dock = DockStyle.Bottom, Height = 24, Font = new Font(Tema.Fonte, FontStyle.Bold) };
        var tituloHist = new Label { Text = "Chamados desta farmácia", Dock = DockStyle.Top, Height = 28, Font = new Font(Tema.Fonte, FontStyle.Bold) };
        direita.Controls.Add(_historico);
        direita.Controls.Add(tituloFrases);
        direita.Controls.Add(_frases);
        direita.Controls.Add(tituloHist);
        Controls.Add(esquerda);
        Controls.Add(direita);
    }

    public void Carregar(Chamado? chamado)
    {
        _carregando = true;
        _origem = chamado;
        _titulo.Text = string.IsNullOrWhiteSpace(chamado?.Protocolo) ? "Novo chamado" : chamado!.Protocolo;
        _protocolo.Text = chamado?.Protocolo ?? "";
        MontarCanais();
        MontarStatus();
        _canal = chamado?.Canal ?? "";
        _statusId = chamado?.Status ?? "aberto";
        PintarCanais();
        PintarStatus();
        _data.Value = DateTime.TryParse(chamado?.Data, out var data) ? data : DateTime.Today;
        _inicio.Text = chamado?.HoraInicio ?? DateTime.Now.ToString("HH:mm");
        _fim.Text = chamado?.HoraFim ?? "";
        _farmacia.Text = chamado?.Farmacia ?? "";
        _cliente.Text = chamado?.Cliente ?? "";
        _codigo.Text = chamado?.Codigo ?? "";
        _tecnico.Text = chamado?.Tecnico ?? _app.Banco.Config.TecnicoLocal ?? "";
        _assunto.Text = chamado?.Assunto ?? "";
        _ddd.Text = chamado?.Ddd ?? "";
        _tel.Text = chamado?.Telefone ?? "";
        _tel2.Text = chamado?.Telefone2 ?? "";
        _obs.Text = chamado?.Obs ?? "";
        _prioridade.SelectedIndex = chamado?.Prioridade == "alta" ? 1 : 0;
        _marca.Checked = chamado?.Marcador == "X";
        if (DateTime.TryParse(chamado?.Agendamento, out var agenda))
        {
            _agenda.Value = agenda;
            _agenda.Checked = true;
        }
        else _agenda.Checked = false;
        CompletarListas();
        MontarExtras(chamado);
        _carregando = false;
        AtualizarLado();
    }

    private void MontarExtras(Chamado? chamado)
    {
        _extras.Controls.Clear();
        _editores.Clear();
        foreach (var campo in _app.Banco.Config.CamposExtras)
        {
            Control editor;
            var atual = "";
            if (chamado != null && chamado.Extras.TryGetValue(campo.Id, out var lido)) atual = lido;
            if (campo.Tipo == "lista")
            {
                var combo = new ComboBox { Width = 240, DropDownStyle = ComboBoxStyle.DropDownList };
                combo.Items.Add("");
                foreach (var opcao in (campo.Opcoes ?? "").Split(',', StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries))
                    combo.Items.Add(opcao);
                combo.SelectedItem = combo.Items.Contains(atual) ? atual : "";
                editor = combo;
            }
            else if (campo.Tipo == "data")
            {
                var data = new DateTimePicker { Width = 150, Format = DateTimePickerFormat.Short, ShowCheckBox = true, Checked = false };
                if (DateTime.TryParse(atual, out var quando))
                {
                    data.Value = quando;
                    data.Checked = true;
                }
                editor = data;
            }
            else
            {
                editor = new TextBox { Width = 280, Text = atual ?? "" };
            }
            _editores[campo.Id] = editor;
            var linha = new Panel { Width = 460, Height = 32 };
            editor.Left = 160;
            editor.Top = 2;
            linha.Controls.Add(new Label { Text = campo.Nome, AutoSize = true, Top = 6, MaximumSize = new Size(150, 0) });
            linha.Controls.Add(editor);
            _extras.Controls.Add(linha);
        }
    }

    private void MontarCanais()
    {
        _canais.Controls.Clear();
        foreach (var canal in _app.Banco.Config.Canais)
        {
            var botao = Tema.Botao(canal.Nome, Color.White, Tema.Tinta);
            botao.Width = 150;
            botao.Tag = canal.Id;
            botao.Margin = new Padding(0, 4, 8, 4);
            botao.Click += (_, _) =>
            {
                _canal = canal.Id;
                PintarCanais();
            };
            _canais.Controls.Add(botao);
        }
    }

    private void MontarStatus()
    {
        _status.Controls.Clear();
        foreach (var status in _app.Banco.Config.Status)
        {
            var botao = Tema.Botao(status.Nome, Color.White, Tema.Tinta);
            botao.AutoSize = true;
            botao.Tag = status.Id;
            botao.Margin = new Padding(0, 4, 8, 4);
            botao.Click += (_, _) =>
            {
                _statusId = status.Id;
                if (status.Id == "concluido" && string.IsNullOrWhiteSpace(_fim.Text))
                    _fim.Text = DateTime.Now.ToString("HH:mm");
                PintarStatus();
            };
            _status.Controls.Add(botao);
        }
    }

    private void PintarCanais()
    {
        foreach (Button botao in _canais.Controls)
        {
            var escolhido = (string?)botao.Tag == _canal;
            botao.BackColor = escolhido ? Tema.Teal : Color.White;
            botao.ForeColor = escolhido ? Color.White : Tema.Tinta;
        }
    }

    private void PintarStatus()
    {
        foreach (Button botao in _status.Controls)
        {
            var escolhido = (string?)botao.Tag == _statusId;
            botao.BackColor = escolhido ? Tema.Teal : Color.White;
            botao.ForeColor = escolhido ? Color.White : Tema.Tinta;
        }
    }

    private void CompletarListas()
    {
        var farms = new AutoCompleteStringCollection();
        foreach (var f in _app.Consultas.Farmacias().Take(1200)) farms.Add(f.Nome);
        foreach (var empresa in _app.Banco.Empresas)
            if (!string.IsNullOrWhiteSpace(empresa.Nome)) farms.Add(empresa.Nome);
        _farmacia.AutoCompleteMode = AutoCompleteMode.SuggestAppend;
        _farmacia.AutoCompleteSource = AutoCompleteSource.CustomSource;
        _farmacia.AutoCompleteCustomSource = farms;
        var tecnicos = new AutoCompleteStringCollection();
        foreach (var t in _app.Banco.Config.Tecnicos) tecnicos.Add(t);
        _tecnico.AutoCompleteMode = AutoCompleteMode.SuggestAppend;
        _tecnico.AutoCompleteSource = AutoCompleteSource.CustomSource;
        _tecnico.AutoCompleteCustomSource = tecnicos;
        _frases.Items.Clear();
        foreach (var frase in _app.Banco.Config.Frases) _frases.Items.Add(frase);
    }

    private void AtualizarLado()
    {
        var resumo = _app.Consultas.Exata(_farmacia.Text);
        if (resumo != null && string.IsNullOrWhiteSpace(_tel.Text))
        {
            _ddd.Text = resumo.Ddd;
            _tel.Text = resumo.Telefone;
            _tel2.Text = resumo.Telefone2;
            if (string.IsNullOrWhiteSpace(_codigo.Text)) _codigo.Text = resumo.Codigo;
        }
        _historico.Items.Clear();
        foreach (var c in _app.Consultas.DaFarmacia(_farmacia.Text).Take(12))
            _historico.Items.Add(new LinhaHist(c, Consultas.DataBr(c.Data) + "  " + c.Assunto));
    }

    private void Gravar(bool outro)
    {
        if (string.IsNullOrWhiteSpace(_farmacia.Text) && string.IsNullOrWhiteSpace(_cliente.Text))
        {
            MessageBox.Show("Informe a farmácia ou o cliente.", "Vitória Soft Atendimento");
            return;
        }
        if (string.IsNullOrWhiteSpace(_assunto.Text))
        {
            MessageBox.Show("Informe o assunto do chamado.", "Vitória Soft Atendimento");
            return;
        }
        if (string.IsNullOrWhiteSpace(_canal) && _statusId != "concluido")
        {
            MessageBox.Show("Escolha se o chamado veio por ligação, WhatsApp, pedido interno ou e-mail. Se ele já foi atendido, use o botão Já foi atendido.", "Vitória Soft Atendimento");
            return;
        }
        var novo = _origem == null || !_app.Banco.Chamados.Contains(_origem);
        var chamado = _origem ?? new Chamado();
        var protocolo = _protocolo.Text.Trim();
        if (protocolo.Length == 0) protocolo = novo ? _app.Consultas.NovoProtocolo() : chamado.Protocolo;
        if (_app.Banco.Chamados.Any(c => c != chamado && c.Protocolo.Equals(protocolo, StringComparison.OrdinalIgnoreCase)))
        {
            MessageBox.Show("Já existe um chamado com o protocolo " + protocolo + ".", "Vitória Soft Atendimento");
            return;
        }
        if (novo)
        {
            chamado.Id = string.IsNullOrWhiteSpace(chamado.Id) ? "novo-" + DateTimeOffset.Now.ToUnixTimeMilliseconds() : chamado.Id;
            chamado.CriadoEm = _data.Value.ToString("yyyy-MM-dd") + "T" + _inicio.Text;
        }
        chamado.Protocolo = protocolo;
        chamado.Canal = _canal;
        chamado.CanalInformado = true;
        chamado.Data = _data.Value.ToString("yyyy-MM-dd");
        chamado.HoraInicio = _inicio.Text.Trim();
        chamado.HoraFim = _fim.Text.Trim();
        chamado.Status = _statusId;
        chamado.Farmacia = _farmacia.Text.Trim();
        chamado.Cliente = _cliente.Text.Trim();
        chamado.Codigo = _codigo.Text.Trim();
        chamado.Tecnico = _tecnico.Text.Trim();
        chamado.Assunto = _assunto.Text.Trim();
        chamado.Ddd = _ddd.Text.Trim();
        chamado.Telefone = _tel.Text.Trim();
        chamado.Telefone2 = _tel2.Text.Trim();
        chamado.Obs = _obs.Text.Trim();
        chamado.Prioridade = _prioridade.SelectedIndex == 1 ? "alta" : "normal";
        chamado.Marcador = _marca.Checked ? "X" : "";
        chamado.Agendamento = _agenda.Checked ? _agenda.Value.ToString("yyyy-MM-ddTHH:mm") : "";
        chamado.AtualizadoEm = DateTime.Now.ToString("s");
        foreach (var campo in _app.Banco.Config.CamposExtras)
        {
            if (!_editores.TryGetValue(campo.Id, out var editor)) continue;
            var valor = editor switch
            {
                ComboBox combo => combo.SelectedItem as string ?? "",
                DateTimePicker data => data.Checked ? data.Value.ToString("yyyy-MM-dd") : "",
                TextBox caixa => caixa.Text.Trim(),
                _ => ""
            };
            if (string.IsNullOrEmpty(valor)) chamado.Extras.Remove(campo.Id);
            else chamado.Extras[campo.Id] = valor;
        }
        if (!string.IsNullOrWhiteSpace(chamado.Tecnico) &&
            !_app.Banco.Config.Tecnicos.Any(t => t.Equals(chamado.Tecnico, StringComparison.OrdinalIgnoreCase)))
            _app.Banco.Config.Tecnicos.Add(chamado.Tecnico);
        if (novo) _app.Banco.Chamados.Add(chamado);
        _app.Salvar();
        if (outro) Carregar(null);
        else _app.Ir("fila");
    }

    private Control CampoProtocolo()
    {
        var faixa = new FlowLayoutPanel { Height = 58, WrapContents = false };
        faixa.Controls.Add(Campo("Protocolo", _protocolo));
        return faixa;
    }

    private Control AcoesTelefone()
    {
        var faixa = new FlowLayoutPanel { Height = 44, WrapContents = false };
        var ligar = Tema.Botao("Ligar", Color.White, Tema.Tinta);
        var whats = Tema.Botao("Abrir WhatsApp", Color.White, Tema.Tinta);
        ligar.Click += (_, _) => Discar(false);
        whats.Click += (_, _) => Discar(true);
        faixa.Controls.Add(ligar);
        faixa.Controls.Add(whats);
        return faixa;
    }

    private void Discar(bool whatsapp)
    {
        var numero = Consultas.Digitos(_ddd.Text + _tel.Text);
        if (numero.Length < 10)
        {
            MessageBox.Show("Preencha o DDD e o telefone.", "Vitória Soft Atendimento");
            return;
        }
        Tema.Abrir(whatsapp ? "https://wa.me/55" + numero : "tel:+55" + numero);
    }

    private Control FaixaAgenda()
    {
        var faixa = new FlowLayoutPanel { Height = 40, WrapContents = false };
        faixa.Controls.Add(new Label { Text = "Agendamento", AutoSize = true, Padding = new Padding(0, 8, 8, 0) });
        faixa.Controls.Add(_agenda);
        faixa.Controls.Add(new Label { Text = "Prioridade", AutoSize = true, Padding = new Padding(12, 8, 8, 0) });
        faixa.Controls.Add(_prioridade);
        return faixa;
    }

    private Control AcoesSalvar()
    {
        var faixa = new FlowLayoutPanel { Height = 92, WrapContents = true, Padding = new Padding(0, 8, 0, 0) };
        var voltar = Tema.Botao("←  Voltar", Color.White, Tema.Tinta);
        var salvar = Tema.Botao("Salvar", Tema.Teal, Color.White);
        var atendido = Tema.Botao("Já foi atendido", Tema.Teal, Color.White);
        var outro = Tema.Botao("Salvar e abrir outro", Color.White, Tema.Tinta);
        var excluir = Tema.Botao("Excluir", Color.FromArgb(255, 244, 242), Tema.Perigo);
        voltar.Width = 120;
        salvar.Width = 120;
        atendido.Width = 170;
        outro.Width = 180;
        voltar.Click += (_, _) => _app.Voltar();
        salvar.Click += (_, _) => Gravar(false);
        atendido.Click += (_, _) =>
        {
            _statusId = "concluido";
            if (string.IsNullOrWhiteSpace(_fim.Text)) _fim.Text = DateTime.Now.ToString("HH:mm");
            PintarStatus();
            Gravar(false);
        };
        outro.Click += (_, _) => Gravar(true);
        excluir.Click += (_, _) =>
        {
            if (_origem == null)
            {
                _app.Ir("fila");
                return;
            }
            if (MessageBox.Show("Excluir o chamado " + _origem.Protocolo + "?", Text, MessageBoxButtons.YesNo) != DialogResult.Yes) return;
            _app.Banco.Chamados.Remove(_origem);
            _app.Salvar();
            _app.Ir("fila");
        };
        faixa.Controls.Add(voltar);
        faixa.Controls.Add(salvar);
        faixa.Controls.Add(atendido);
        faixa.Controls.Add(outro);
        faixa.Controls.Add(excluir);
        return faixa;
    }

    private static Label Rotulo(string texto) => new()
    {
        Text = texto,
        ForeColor = Tema.Mudo,
        Height = 22,
        Padding = new Padding(0, 6, 0, 0)
    };

    private static FlowLayoutPanel Faixa(Control a, string la, Control b, string lb, Control c, string lc)
    {
        var faixa = new FlowLayoutPanel { Height = 58, WrapContents = false };
        faixa.Controls.Add(Campo(la, a));
        faixa.Controls.Add(Campo(lb, b));
        faixa.Controls.Add(Campo(lc, c));
        return faixa;
    }

    private static Panel Campo(string rotulo, Control editor)
    {
        var painel = new Panel { Width = editor.Width + 16, Height = 54 };
        editor.Top = 22;
        editor.Left = 0;
        painel.Controls.Add(editor);
        painel.Controls.Add(new Label { Text = rotulo, ForeColor = Tema.Mudo, AutoSize = true, Top = 2 });
        return painel;
    }

    private sealed class LinhaHist
    {
        public LinhaHist(Chamado chamado, string texto)
        {
            Chamado = chamado;
            Texto = texto;
        }
        public Chamado Chamado { get; }
        public string Texto { get; }
        public override string ToString() => Texto;
    }
}
