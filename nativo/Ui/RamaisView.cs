using VitoriaSoft.Atendimento.Modelo;

namespace VitoriaSoft.Atendimento.Ui;

public sealed class RamaisView : UserControl, IAtualizavel
{
    private readonly IApp _app;
    private readonly DataGridView _grade = new() { Dock = DockStyle.Fill };
    private readonly TextBox _nome = new() { Width = 160, PlaceholderText = "Nome" };
    private readonly TextBox _numero = new() { Width = 80, PlaceholderText = "Ramal" };
    private readonly TextBox _almoco = new() { Width = 110, PlaceholderText = "Almoço" };
    private readonly TextBox _ddd = new() { Width = 50, PlaceholderText = "DDD" };
    private readonly TextBox _telefone = new() { Width = 120, PlaceholderText = "Telefone" };
    private readonly TextBox _discagem = new() { Width = 130, PlaceholderText = "Discagem" };
    private readonly TextBox _dicas = new() { Dock = DockStyle.Bottom, Height = 72, Multiline = true, ScrollBars = ScrollBars.Vertical };
    private bool _carregando;

    public RamaisView(IApp app)
    {
        _app = app;
        Dock = DockStyle.Fill;
        BackColor = Tema.Fundo;
        _grade.ReadOnly = false;
        _grade.AllowUserToAddRows = false;
        _grade.AllowUserToDeleteRows = false;
        _grade.RowHeadersVisible = false;
        _grade.SelectionMode = DataGridViewSelectionMode.CellSelect;
        _grade.EditMode = DataGridViewEditMode.EditOnEnter;
        _grade.AutoSizeColumnsMode = DataGridViewAutoSizeColumnsMode.Fill;
        _grade.BackgroundColor = Tema.Cartao;
        _grade.BorderStyle = BorderStyle.None;
        foreach (var coluna in new[] { "Nome", "Ramal", "Almoço", "DDD", "Telefone", "Discagem" })
            _grade.Columns.Add(coluna, coluna);
        _grade.CellEndEdit += (_, e) =>
        {
            if (_carregando || _grade.Rows[e.RowIndex].Tag is not RamalInfo ramal) return;
            ramal.Nome = Celula(e.RowIndex, 0);
            ramal.Numero = Celula(e.RowIndex, 1);
            ramal.Almoco = Celula(e.RowIndex, 2);
            ramal.Ddd = Celula(e.RowIndex, 3);
            ramal.Telefone = Celula(e.RowIndex, 4);
            ramal.Discagem = Celula(e.RowIndex, 5);
            _app.Salvar();
        };
        var copiar = Tema.Botao("Copiar discagem", Color.White, Tema.Tinta);
        var remover = Tema.Botao("Remover", Color.FromArgb(255, 244, 242), Tema.Perigo);
        copiar.Click += (_, _) =>
        {
            if (Linha() is { } ramal && !string.IsNullOrWhiteSpace(ramal.Discagem))
            {
                Clipboard.SetText(ramal.Discagem);
                MessageBox.Show("Discagem copiada: " + ramal.Discagem, "Vitória Soft Atendimento");
            }
        };
        remover.Click += (_, _) =>
        {
            if (Linha() is not { } ramal) return;
            _app.Banco.Ramais.Remove(ramal);
            _app.Salvar();
            Atualizar();
        };
        var incluir = Tema.Botao("Adicionar ramal", Tema.Teal, Color.White);
        incluir.Click += (_, _) =>
        {
            if (string.IsNullOrWhiteSpace(_nome.Text) || string.IsNullOrWhiteSpace(_numero.Text))
            {
                MessageBox.Show("Informe o nome e o ramal.", "Vitória Soft Atendimento");
                return;
            }
            _app.Banco.Ramais.Add(new RamalInfo
            {
                Nome = _nome.Text.Trim(),
                Numero = _numero.Text.Trim(),
                Almoco = _almoco.Text.Trim(),
                Ddd = _ddd.Text.Trim(),
                Telefone = _telefone.Text.Trim(),
                Discagem = _discagem.Text.Trim()
            });
            _nome.Clear();
            _numero.Clear();
            _almoco.Clear();
            _ddd.Clear();
            _telefone.Clear();
            _discagem.Clear();
            _app.Salvar();
            Atualizar();
        };
        var faixa = new FlowLayoutPanel { Dock = DockStyle.Bottom, Height = 88, WrapContents = true, Padding = new Padding(0, 8, 0, 0) };
        faixa.Controls.Add(_nome);
        faixa.Controls.Add(_numero);
        faixa.Controls.Add(_almoco);
        faixa.Controls.Add(_ddd);
        faixa.Controls.Add(_telefone);
        faixa.Controls.Add(_discagem);
        faixa.Controls.Add(incluir);
        faixa.Controls.Add(copiar);
        faixa.Controls.Add(remover);
        var salvarDicas = Tema.Botao("Salvar dicas", Color.White, Tema.Tinta);
        salvarDicas.Dock = DockStyle.Bottom;
        salvarDicas.Height = 36;
        salvarDicas.Click += (_, _) =>
        {
            _app.Banco.DicasTelefone = _dicas.Text.Split(["\r\n", "\n"], StringSplitOptions.RemoveEmptyEntries).Select(l => l.Trim()).Where(l => l.Length > 0).ToList();
            _app.Salvar();
        };
        var topo = new Panel { Dock = DockStyle.Top, Height = 70 };
        topo.Controls.Add(Tema.Subtitulo("Clique na célula para corrigir nome, ramal, almoço, telefone ou discagem."));
        topo.Controls.Add(Tema.TituloDe("Ramais"));
        Controls.Add(_grade);
        Controls.Add(faixa);
        Controls.Add(salvarDicas);
        Controls.Add(_dicas);
        Controls.Add(topo);
    }

    public void Atualizar()
    {
        _carregando = true;
        _grade.Rows.Clear();
        foreach (var ramal in _app.Banco.Ramais)
        {
            var i = _grade.Rows.Add(ramal.Nome, ramal.Numero, ramal.Almoco, ramal.Ddd, ramal.Telefone, ramal.Discagem);
            _grade.Rows[i].Tag = ramal;
        }
        _dicas.Text = string.Join(Environment.NewLine, _app.Banco.DicasTelefone);
        _carregando = false;
    }

    private RamalInfo? Linha() => _grade.CurrentRow?.Tag as RamalInfo;

    private string Celula(int linha, int coluna) => _grade.Rows[linha].Cells[coluna].Value as string ?? "";
}
