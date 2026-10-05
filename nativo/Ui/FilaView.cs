using VitoriaSoft.Atendimento.Modelo;
using VitoriaSoft.Atendimento.Servico;

namespace VitoriaSoft.Atendimento.Ui;

public sealed class FilaView : UserControl, IAtualizavel
{
    private readonly IApp _app;
    private readonly TextBox _busca = new() { Width = 260 };
    private readonly ComboBox _periodo = new() { Width = 150, DropDownStyle = ComboBoxStyle.DropDownList };
    private readonly ComboBox _status = new() { Width = 210, DropDownStyle = ComboBoxStyle.DropDownList };
    private readonly ComboBox _canal = new() { Width = 160, DropDownStyle = ComboBoxStyle.DropDownList };
    private readonly ComboBox _tecnico = new() { Width = 160, DropDownStyle = ComboBoxStyle.DropDownList };
    private readonly ComboBox _situacao = new() { Width = 170, DropDownStyle = ComboBoxStyle.DropDownList };
    private readonly DataGridView _grade = new();
    private readonly Label _contagem = new() { AutoSize = true, ForeColor = Tema.Mudo, Dock = DockStyle.Bottom, Height = 28, Padding = new Padding(0, 6, 0, 0) };
    private readonly System.Windows.Forms.Timer _timer = new() { Interval = 250 };
    private List<Chamado> _lista = [];
    private bool _silencio;
    private string? _forcarStatus;
    private bool? _forcarHoje;

    public FilaView(IApp app)
    {
        _app = app;
        Dock = DockStyle.Fill;
        BackColor = Tema.Fundo;
        var topo = new FlowLayoutPanel { Dock = DockStyle.Top, Height = 132, WrapContents = true };
        _busca.PlaceholderText = "Buscar farmácia, cliente, telefone, assunto";
        _busca.TextChanged += (_, _) => { _timer.Stop(); _timer.Start(); };
        _periodo.SelectedIndexChanged += (_, _) => { if (!_silencio) Atualizar(); };
        _status.SelectedIndexChanged += (_, _) => { if (!_silencio) Atualizar(); };
        _canal.SelectedIndexChanged += (_, _) => { if (!_silencio) Atualizar(); };
        _tecnico.SelectedIndexChanged += (_, _) => { if (!_silencio) Atualizar(); };
        var abrir = Tema.Botao("Abrir chamado", Color.White, Tema.Tinta);
        abrir.Width = 150;
        abrir.Click += (_, _) => Abrir(_grade.CurrentRow?.Index ?? -1);
        var este = Tema.Botao("Este já foi atendido", Tema.Teal, Color.White);
        este.Width = 200;
        este.Click += (_, _) => MarcarUm(_grade.CurrentRow?.Index ?? -1);
        var planilha = Tema.Botao("Trazer planilha do dia", Color.White, Tema.Tinta);
        planilha.Width = 210;
        planilha.Click += (_, _) => TrazerPlanilha();
        var todos = Tema.Botao("Todos desta lista já foram atendidos", Tema.Teal, Color.White);
        todos.Width = 320;
        todos.Click += (_, _) => MarcarTodos();
        var aplicar = Tema.Botao("Mudar situação deste", Color.White, Tema.Tinta);
        aplicar.Width = 190;
        aplicar.Click += (_, _) => AplicarSituacao();
        var excluir = Tema.Botao("Excluir", Color.FromArgb(255, 244, 242), Tema.Perigo);
        excluir.Width = 100;
        excluir.Click += (_, _) => ExcluirSelecionado();
        topo.Controls.Add(_busca);
        topo.Controls.Add(_periodo);
        topo.Controls.Add(_status);
        topo.Controls.Add(_canal);
        topo.Controls.Add(_tecnico);
        topo.Controls.Add(abrir);
        topo.Controls.Add(este);
        topo.Controls.Add(planilha);
        topo.Controls.Add(todos);
        topo.Controls.Add(_situacao);
        topo.Controls.Add(aplicar);
        topo.Controls.Add(excluir);

        _grade.Dock = DockStyle.Fill;
        _grade.ReadOnly = false;
        _grade.AllowUserToAddRows = false;
        _grade.AllowUserToDeleteRows = false;
        _grade.RowHeadersVisible = false;
        _grade.SelectionMode = DataGridViewSelectionMode.FullRowSelect;
        _grade.MultiSelect = false;
        _grade.BackgroundColor = Tema.Cartao;
        _grade.BorderStyle = BorderStyle.None;
        _grade.AutoSizeColumnsMode = DataGridViewAutoSizeColumnsMode.Fill;
        _grade.VirtualMode = true;
        _grade.EditMode = DataGridViewEditMode.EditProgrammatically;
        _grade.Font = Tema.Fonte;
        _grade.RowTemplate.Height = 30;
        _grade.CellValueNeeded += Grade_CellValueNeeded;
        _grade.CellDoubleClick += (_, e) => Abrir(e.RowIndex);
        _grade.KeyDown += (_, e) =>
        {
            if (e.KeyCode == Keys.Enter && _grade.CurrentRow != null)
            {
                e.Handled = true;
                Abrir(_grade.CurrentRow.Index);
            }
        };
        _grade.CellMouseDown += (_, e) =>
        {
            if (e.Button == MouseButtons.Right && e.RowIndex >= 0)
                _grade.CurrentCell = _grade.Rows[e.RowIndex].Cells[0];
        };
        var menu = new ContextMenuStrip();
        menu.Items.Add("Abrir chamado", null, (_, _) => Abrir(_grade.CurrentRow?.Index ?? -1));
        menu.Items.Add("Este já foi atendido", null, (_, _) => MarcarUm(_grade.CurrentRow?.Index ?? -1));
        menu.Items.Add("Excluir", null, (_, _) => ExcluirSelecionado());
        _grade.ContextMenuStrip = menu;
        foreach (var nome in new[] { "Protocolo", "Data", "Farmácia", "Cliente", "Assunto", "Canal", "Técnico", "Status" })
        {
            var coluna = new DataGridViewTextBoxColumn { Name = nome, HeaderText = nome, ReadOnly = true };
            _grade.Columns.Add(coluna);
        }
        _grade.Columns["Canal"]!.MinimumWidth = 130;
        _grade.Columns["Assunto"]!.MinimumWidth = 160;
        _grade.Columns["Status"]!.MinimumWidth = 100;
        _grade.Columns.Add(BotaoColuna("Atendido", "Atendido", 110));
        _grade.Columns.Add(BotaoColuna("Abrir", "Abrir", 80));
        _grade.CellContentClick += (_, e) =>
        {
            if (e.RowIndex < 0) return;
            var nome = _grade.Columns[e.ColumnIndex].Name;
            if (nome == "Abrir") Abrir(e.RowIndex);
            if (nome == "Atendido") MarcarUm(e.RowIndex);
        };
        _grade.CellToolTipTextNeeded += (_, e) =>
        {
            if (e.RowIndex < 0 || e.RowIndex >= _lista.Count) return;
            if (_grade.Columns[e.ColumnIndex] is DataGridViewButtonColumn) return;
            e.ToolTipText = TextoCelula(e.RowIndex, e.ColumnIndex);
        };

        _timer.Tick += (_, _) => { _timer.Stop(); Atualizar(); };
        var titulo = new Panel { Dock = DockStyle.Top, Height = 58 };
        titulo.Controls.Add(Tema.Subtitulo("Azul na planilha já foi atendido. Branco ainda não. O canal pode ficar em branco."));
        titulo.Controls.Add(Tema.TituloDe("Chamados"));
        Controls.Add(_grade);
        Controls.Add(_contagem);
        Controls.Add(topo);
        Controls.Add(titulo);
    }

    public void DefinirFiltro(string status, bool somenteHoje)
    {
        _forcarStatus = status;
        _forcarHoje = somenteHoje;
    }

    public void Atualizar()
    {
        if (_silencio) return;
        PreencherFiltros();
        var status = (_status.SelectedItem as OpcaoItem)?.Id ?? "abertos";
        var canal = (_canal.SelectedItem as OpcaoItem)?.Id ?? "";
        var tecnico = _tecnico.SelectedItem as string ?? "";
        if (tecnico == "Todos os técnicos") tecnico = "";
        _lista = _app.Consultas.Filtrar(_busca.Text, canal, status, tecnico, _periodo.SelectedIndex == 0);
        _grade.RowCount = 0;
        _grade.RowCount = _lista.Count;
        _grade.Invalidate();
        _contagem.Text = _lista.Count.ToString("N0") + " chamados nesta lista. O botão verde encerra todos eles, sem olhar ligação, WhatsApp ou e-mail.";
    }

    private void PreencherFiltros()
    {
        _silencio = true;
        var statusId = _forcarStatus ?? (_status.SelectedItem as OpcaoItem)?.Id ?? "abertos";
        var canalId = (_canal.SelectedItem as OpcaoItem)?.Id ?? "";
        var tecnicoId = _tecnico.SelectedItem as string ?? "Todos os técnicos";
        var periodo = _forcarHoje == true ? 0 : _forcarHoje == false ? 1 : _periodo.SelectedIndex;
        _forcarStatus = null;
        _forcarHoje = null;

        if (_periodo.Items.Count == 0)
        {
            _periodo.Items.Add("Hoje");
            _periodo.Items.Add("Qualquer data");
        }
        _periodo.SelectedIndex = periodo < 0 ? 1 : periodo;

        _status.Items.Clear();
        _status.Items.Add(new OpcaoItem("abertos", "Ainda não concluídos"));
        _status.Items.Add(new OpcaoItem("todos", "Histórico completo"));
        _status.Items.Add(new OpcaoItem("retorno-agendado", "Retorno ou agendado"));
        foreach (var s in _app.Banco.Config.Status) _status.Items.Add(new OpcaoItem(s.Id, s.Nome));
        Selecionar(_status, statusId);

        _canal.Items.Clear();
        _canal.Items.Add(new OpcaoItem("", "Todos os canais"));
        foreach (var c in _app.Banco.Config.Canais) _canal.Items.Add(new OpcaoItem(c.Id, c.Nome));
        _canal.Items.Add(new OpcaoItem("__vazio", "Não informado"));
        Selecionar(_canal, canalId);

        _tecnico.Items.Clear();
        _tecnico.Items.Add("Todos os técnicos");
        foreach (var t in _app.Banco.Config.Tecnicos) _tecnico.Items.Add(t);
        _tecnico.SelectedItem = _tecnico.Items.Contains(tecnicoId) ? tecnicoId : "Todos os técnicos";

        if (_situacao.Items.Count != _app.Banco.Config.Status.Count)
        {
            var escolhida = _situacao.SelectedItem as string;
            _situacao.Items.Clear();
            foreach (var s in _app.Banco.Config.Status) _situacao.Items.Add(s.Nome);
            if (escolhida != null && _situacao.Items.Contains(escolhida)) _situacao.SelectedItem = escolhida;
            else if (_situacao.Items.Count > 0) _situacao.SelectedIndex = 0;
        }
        _silencio = false;
    }

    private static void Selecionar(ComboBox combo, string id)
    {
        foreach (OpcaoItem item in combo.Items)
        {
            if (item.Id != id) continue;
            combo.SelectedItem = item;
            return;
        }
        if (combo.Items.Count > 0) combo.SelectedIndex = 0;
    }

    private void Grade_CellValueNeeded(object? sender, DataGridViewCellValueEventArgs e)
    {
        if (e.RowIndex < 0 || e.RowIndex >= _lista.Count) return;
        if (_grade.Columns[e.ColumnIndex] is DataGridViewButtonColumn coluna)
        {
            e.Value = coluna.Name == "Atendido" ? "Atendido" : "Abrir";
            return;
        }
        var c = _lista[e.RowIndex];
        e.Value = e.ColumnIndex switch
        {
            0 => c.Protocolo,
            1 => Consultas.DataBr(c.Data) + " " + c.HoraInicio,
            2 => c.Farmacia,
            3 => c.Cliente,
            4 => c.Assunto,
            5 => _app.Consultas.NomeCanal(c.Canal),
            6 => c.Tecnico,
            _ => _app.Consultas.NomeStatus(c.Status)
        };
    }

    private void MarcarUm(int linha)
    {
        if (linha < 0 || linha >= _lista.Count)
        {
            MessageBox.Show("Clique primeiro na linha do chamado.", "Vitória Soft Atendimento");
            return;
        }
        Consultas.Encerrar(_lista[linha]);
        _app.Salvar();
        Atualizar();
    }

    private void TrazerPlanilha()
    {
        var seguir = MessageBox.Show(
            "A planilha do dia entra assim:\n\nLinha azul: já foi atendido.\nLinha branca: ainda não foi atendido.\n\nChamado que você já alterou aqui não volta atrás.",
            "Vitória Soft Atendimento",
            MessageBoxButtons.OKCancel,
            MessageBoxIcon.Information);
        if (seguir != DialogResult.OK) return;
        using var dialogo = new OpenFileDialog { Filter = "Planilha Excel (*.xlsx)|*.xlsx" };
        if (dialogo.ShowDialog(this) != DialogResult.OK) return;
        try
        {
            UseWaitCursor = true;
            var resultado = ImportadorPlanilha.Mesclar(_app.Banco, dialogo.FileName);
            _app.Salvar();
            MessageBox.Show(AjustesView.TextoImportacao(resultado), "Vitória Soft Atendimento");
            Atualizar();
        }
        catch (Exception ex)
        {
            MessageBox.Show("Não foi possível ler a planilha.\n\n" + ex.Message, "Vitória Soft Atendimento");
        }
        finally
        {
            UseWaitCursor = false;
        }
    }

    private void MarcarTodos()
    {
        var pendentes = _lista.Where(c => c.Status != "concluido").ToList();
        if (pendentes.Count == 0)
        {
            MessageBox.Show("Não há chamado pendente nesta lista.", "Vitória Soft Atendimento");
            return;
        }
        var resposta = MessageBox.Show(
            pendentes.Count.ToString("N0") + " chamados desta lista serão marcados como atendidos.\n\nNão importa se vieram por ligação, WhatsApp, e-mail, pedido interno ou se o canal está em branco.",
            "Vitória Soft Atendimento",
            MessageBoxButtons.YesNo,
            MessageBoxIcon.Question);
        if (resposta != DialogResult.Yes) return;
        UseWaitCursor = true;
        try
        {
            foreach (var chamado in pendentes) Consultas.Encerrar(chamado);
            _app.Salvar();
        }
        finally
        {
            UseWaitCursor = false;
        }
        Atualizar();
        MessageBox.Show(pendentes.Count.ToString("N0") + " chamados foram marcados como atendidos.", "Vitória Soft Atendimento");
    }

    private string TextoCelula(int linha, int coluna)
    {
        var c = _lista[linha];
        return coluna switch
        {
            0 => c.Protocolo,
            1 => Consultas.DataBr(c.Data) + " " + c.HoraInicio,
            2 => c.Farmacia,
            3 => c.Cliente,
            4 => c.Assunto,
            5 => _app.Consultas.NomeCanal(c.Canal),
            6 => c.Tecnico,
            _ => _app.Consultas.NomeStatus(c.Status)
        };
    }

    private static DataGridViewButtonColumn BotaoColuna(string nome, string texto, int largura) => new()
    {
        Name = nome,
        HeaderText = "",
        Text = texto,
        UseColumnTextForButtonValue = true,
        MinimumWidth = largura,
        FillWeight = largura
    };

    private void AplicarSituacao()
    {
        var linha = _grade.CurrentRow?.Index ?? -1;
        if (linha < 0 || linha >= _lista.Count)
        {
            MessageBox.Show("Selecione um chamado na lista.", "Vitória Soft Atendimento");
            return;
        }
        var nome = _situacao.SelectedItem as string;
        var status = _app.Banco.Config.Status.FirstOrDefault(s => s.Nome == nome);
        if (status == null) return;
        var chamado = _lista[linha];
        chamado.Status = status.Id;
        if (status.Id == "concluido" && string.IsNullOrWhiteSpace(chamado.HoraFim))
            chamado.HoraFim = DateTime.Now.ToString("HH:mm");
        chamado.AtualizadoEm = DateTime.Now.ToString("s");
        _app.Salvar();
        Atualizar();
    }

    private void ExcluirSelecionado()
    {
        var linha = _grade.CurrentRow?.Index ?? -1;
        if (linha < 0 || linha >= _lista.Count) return;
        var chamado = _lista[linha];
        if (MessageBox.Show("Excluir o chamado " + chamado.Protocolo + "?", "Vitória Soft Atendimento", MessageBoxButtons.YesNo) != DialogResult.Yes)
            return;
        _app.Banco.Chamados.Remove(chamado);
        _app.Salvar();
        Atualizar();
    }

    private void Abrir(int linha)
    {
        if (linha >= 0 && linha < _lista.Count) _app.Editar(_lista[linha]);
    }

    private sealed record OpcaoItem(string Id, string Nome)
    {
        public override string ToString() => Nome;
    }
}
