using VitoriaSoft.Atendimento.Modelo;
using VitoriaSoft.Atendimento.Servico;

namespace VitoriaSoft.Atendimento.Ui;

public sealed class ListaChamados : UserControl
{
    private readonly IApp _app;
    private readonly Action _aoMudar;
    private readonly DataGridView _grade = new();
    private readonly Label _aviso = new() { Dock = DockStyle.Bottom, Height = 36, ForeColor = Tema.Mudo };
    private bool _carregando;

    public ListaChamados(IApp app, string titulo, Action aoMudar)
    {
        _app = app;
        _aoMudar = aoMudar;
        Dock = DockStyle.Fill;
        BackColor = Tema.Cartao;
        Padding = new Padding(10);
        var cabeca = new Label
        {
            Text = titulo,
            Dock = DockStyle.Top,
            Height = 28,
            Font = new Font(Tema.Fonte, FontStyle.Bold),
            ForeColor = Tema.Tinta
        };
        _grade.Dock = DockStyle.Fill;
        _grade.AllowUserToAddRows = false;
        _grade.AllowUserToDeleteRows = false;
        _grade.RowHeadersVisible = false;
        _grade.MultiSelect = false;
        _grade.SelectionMode = DataGridViewSelectionMode.CellSelect;
        _grade.EditMode = DataGridViewEditMode.EditOnEnter;
        _grade.BackgroundColor = Tema.Cartao;
        _grade.BorderStyle = BorderStyle.None;
        _grade.AutoSizeColumnsMode = DataGridViewAutoSizeColumnsMode.Fill;
        _grade.Font = Tema.Fonte;
        _grade.RowTemplate.Height = 32;
        _grade.ColumnHeadersHeight = 34;
        _grade.DataError += (_, e) => e.ThrowException = false;
        Coluna("Hora", "Hora", true, 60);
        Coluna("Farmacia", "Farmácia", false, 120);
        Coluna("Assunto", "Assunto", false, 160);
        Combo("Canal", "Canal", 90);
        Combo("Tecnico", "Técnico", 100);
        Combo("Situacao", "Situação", 110);
        _grade.Columns.Add(new DataGridViewButtonColumn
        {
            Name = "Atendido",
            HeaderText = "",
            Text = "Atendido",
            UseColumnTextForButtonValue = true,
            MinimumWidth = 100,
            FillWeight = 80
        });
        _grade.Columns.Add(new DataGridViewButtonColumn
        {
            Name = "Editar",
            HeaderText = "",
            Text = "Abrir",
            UseColumnTextForButtonValue = true,
            MinimumWidth = 80,
            FillWeight = 70
        });
        _grade.CellEndEdit += (_, e) => GravarLinha(e.RowIndex);
        _grade.CellContentClick += (_, e) =>
        {
            if (e.RowIndex < 0 || _grade.Rows[e.RowIndex].Tag is not Chamado chamado) return;
            var nome = _grade.Columns[e.ColumnIndex].Name;
            if (nome == "Editar")
            {
                _grade.EndEdit();
                _app.Editar(chamado);
            }
            else if (nome == "Atendido")
            {
                Consultas.Encerrar(chamado);
                _app.Salvar();
                _aoMudar();
            }
        };
        _grade.CellMouseDown += (_, e) =>
        {
            if (e.Button == MouseButtons.Right && e.RowIndex >= 0)
                _grade.CurrentCell = _grade.Rows[e.RowIndex].Cells[Math.Max(0, e.ColumnIndex)];
        };
        var menu = new ContextMenuStrip();
        menu.Items.Add("Abrir edição completa", null, (_, _) => AbrirAtual());
        menu.Items.Add("Excluir chamado", null, (_, _) => ExcluirAtual());
        _grade.ContextMenuStrip = menu;
        Controls.Add(_grade);
        Controls.Add(_aviso);
        Controls.Add(cabeca);
    }

    public void Mostrar(IReadOnlyList<Chamado> lista, int limite, string avisoExtra)
    {
        _carregando = true;
        var visiveis = lista.Take(limite).ToList();
        PreencherOpcoes(visiveis);
        _grade.Rows.Clear();
        foreach (var c in visiveis)
        {
            var i = _grade.Rows.Add(
                c.HoraInicio,
                c.Farmacia,
                c.Assunto,
                NomeCanal(c.Canal),
                c.Tecnico,
                NomeStatus(c.Status));
            _grade.Rows[i].Tag = c;
        }
        _aviso.Text = visiveis.Count + " nesta lista. "
            + (lista.Count > visiveis.Count ? "Há mais " + (lista.Count - visiveis.Count) + ". Abra a fila para ver o restante. " : "")
            + avisoExtra;
        _carregando = false;
    }

    private void PreencherOpcoes(IReadOnlyList<Chamado> lista)
    {
        var canal = (DataGridViewComboBoxColumn)_grade.Columns["Canal"]!;
        var tecnico = (DataGridViewComboBoxColumn)_grade.Columns["Tecnico"]!;
        var situacao = (DataGridViewComboBoxColumn)_grade.Columns["Situacao"]!;
        canal.Items.Clear();
        tecnico.Items.Clear();
        situacao.Items.Clear();
        canal.Items.Add("Não informado");
        foreach (var item in _app.Banco.Config.Canais) canal.Items.Add(item.Nome);
        tecnico.Items.Add("");
        foreach (var item in _app.Banco.Config.Tecnicos) tecnico.Items.Add(item);
        foreach (var item in _app.Banco.Config.Status) situacao.Items.Add(item.Nome);
        foreach (var c in lista)
        {
            var nomeCanal = NomeCanal(c.Canal);
            if (!canal.Items.Contains(nomeCanal)) canal.Items.Add(nomeCanal);
            if (!string.IsNullOrWhiteSpace(c.Tecnico) && !tecnico.Items.Contains(c.Tecnico)) tecnico.Items.Add(c.Tecnico);
            var nomeStatus = NomeStatus(c.Status);
            if (!situacao.Items.Contains(nomeStatus)) situacao.Items.Add(nomeStatus);
        }
    }

    private void GravarLinha(int linha)
    {
        if (_carregando || linha < 0 || linha >= _grade.Rows.Count) return;
        if (_grade.Rows[linha].Tag is not Chamado chamado) return;
        chamado.HoraInicio = Texto(linha, "Hora");
        chamado.Farmacia = Texto(linha, "Farmacia");
        chamado.Assunto = Texto(linha, "Assunto");
        var canalNome = Texto(linha, "Canal");
        if (canalNome == "Não informado")
        {
            chamado.Canal = "";
            chamado.CanalInformado = false;
        }
        else
        {
            var canal = _app.Banco.Config.Canais.FirstOrDefault(c => c.Nome == canalNome);
            if (canal != null)
            {
                chamado.Canal = canal.Id;
                chamado.CanalInformado = true;
            }
        }
        chamado.Tecnico = Texto(linha, "Tecnico");
        var statusNome = Texto(linha, "Situacao");
        var status = _app.Banco.Config.Status.FirstOrDefault(s => s.Nome == statusNome);
        if (status != null)
        {
            chamado.Status = status.Id;
            if (status.Id == "concluido" && string.IsNullOrWhiteSpace(chamado.HoraFim))
                chamado.HoraFim = DateTime.Now.ToString("HH:mm");
        }
        chamado.AtualizadoEm = DateTime.Now.ToString("s");
        if (!string.IsNullOrWhiteSpace(chamado.Tecnico) &&
            !_app.Banco.Config.Tecnicos.Any(t => t.Equals(chamado.Tecnico, StringComparison.OrdinalIgnoreCase)))
            _app.Banco.Config.Tecnicos.Add(chamado.Tecnico);
        _app.Salvar();
        BeginInvoke(_aoMudar);
    }

    private void AbrirAtual()
    {
        if (_grade.CurrentRow?.Tag is Chamado chamado) _app.Editar(chamado);
    }

    private void ExcluirAtual()
    {
        if (_grade.CurrentRow?.Tag is not Chamado chamado) return;
        if (MessageBox.Show("Excluir o chamado " + chamado.Protocolo + "?", "Vitória Soft Atendimento", MessageBoxButtons.YesNo) != DialogResult.Yes)
            return;
        _app.Banco.Chamados.Remove(chamado);
        _app.Salvar();
        _aoMudar();
    }

    private string NomeCanal(string? id) =>
        string.IsNullOrWhiteSpace(id) ? "Não informado" : _app.Consultas.NomeCanal(id);

    private string NomeStatus(string? id) => _app.Consultas.NomeStatus(id);

    private string Texto(int linha, string coluna) => _grade.Rows[linha].Cells[coluna].Value as string ?? "";

    private void Coluna(string nome, string titulo, bool somenteLeitura, float peso)
    {
        var coluna = new DataGridViewTextBoxColumn { Name = nome, HeaderText = titulo, ReadOnly = somenteLeitura, FillWeight = peso };
        _grade.Columns.Add(coluna);
    }

    private void Combo(string nome, string titulo, float peso)
    {
        var coluna = new DataGridViewComboBoxColumn
        {
            Name = nome,
            HeaderText = titulo,
            FlatStyle = FlatStyle.Flat,
            DisplayStyle = DataGridViewComboBoxDisplayStyle.ComboBox,
            FillWeight = peso
        };
        _grade.Columns.Add(coluna);
    }
}
