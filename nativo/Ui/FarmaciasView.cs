using VitoriaSoft.Atendimento.Modelo;
using VitoriaSoft.Atendimento.Servico;

namespace VitoriaSoft.Atendimento.Ui;

public sealed class FarmaciasView : UserControl, IAtualizavel
{
    private readonly IApp _app;
    private readonly TextBox _busca = new() { Dock = DockStyle.Top };
    private readonly DataGridView _grade = new() { Dock = DockStyle.Fill };
    private readonly DataGridView _historico = new() { Dock = DockStyle.Bottom, Height = 220 };
    private List<LinhaFarm> _lista = [];
    private List<Chamado> _chamados = [];

    public FarmaciasView(IApp app)
    {
        _app = app;
        Dock = DockStyle.Fill;
        BackColor = Tema.Fundo;
        Preparar(_grade, ["Farmácia", "Telefone", "Código", "Chamados", "Último"]);
        Preparar(_historico, ["Data", "Assunto", "Canal", "Status", "Técnico"]);
        _busca.PlaceholderText = "Nome da farmácia";
        _busca.TextChanged += (_, _) => MostrarLista();
        _grade.SelectionChanged += (_, _) => MostrarHistorico();
        _grade.CellDoubleClick += (_, _) => EditarSelecionada();
        _historico.CellDoubleClick += (_, e) =>
        {
            if (e.RowIndex >= 0 && e.RowIndex < _chamados.Count) _app.Editar(_chamados[e.RowIndex]);
        };

        var acoes = new FlowLayoutPanel { Dock = DockStyle.Top, Height = 48, WrapContents = false };
        var editar = Tema.Botao("Editar dados", Tema.Teal, Color.White);
        var novoChamado = Tema.Botao("Novo chamado desta farmácia", Color.White, Tema.Tinta);
        var incluir = Tema.Botao("Incluir farmácia", Color.White, Tema.Tinta);
        var remover = Tema.Botao("Remover do cadastro", Color.FromArgb(255, 244, 242), Tema.Perigo);
        editar.Width = 140;
        novoChamado.Width = 230;
        incluir.Width = 150;
        remover.Width = 170;
        editar.Click += (_, _) => EditarSelecionada();
        novoChamado.Click += (_, _) => NovoChamado();
        incluir.Click += (_, _) => Incluir();
        remover.Click += (_, _) => RemoverCadastro();
        acoes.Controls.Add(editar);
        acoes.Controls.Add(novoChamado);
        acoes.Controls.Add(incluir);
        acoes.Controls.Add(remover);

        var topo = new Panel { Dock = DockStyle.Top, Height = 78 };
        topo.Controls.Add(Tema.Subtitulo("Duplo clique no histórico abre o chamado. Editar dados corrige a farmácia nos chamados."));
        topo.Controls.Add(Tema.TituloDe("Farmácias"));
        Controls.Add(_grade);
        Controls.Add(_historico);
        Controls.Add(acoes);
        Controls.Add(_busca);
        Controls.Add(topo);
    }

    public void Atualizar() => MostrarLista();

    private void MostrarLista()
    {
        var q = _busca.Text.Trim();
        var mapa = new Dictionary<string, LinhaFarm>(StringComparer.OrdinalIgnoreCase);
        foreach (var f in _app.Consultas.Farmacias())
        {
            mapa[f.Nome.Trim()] = new LinhaFarm
            {
                Nome = f.Nome,
                Codigo = f.Codigo,
                Ddd = f.Ddd,
                Telefone = f.Telefone,
                Telefone2 = f.Telefone2,
                Quantidade = f.Quantidade,
                Ultima = f.Ultima
            };
        }
        foreach (var empresa in _app.Banco.Empresas)
        {
            if (string.IsNullOrWhiteSpace(empresa.Nome)) continue;
            if (!mapa.TryGetValue(empresa.Nome.Trim(), out var linha))
            {
                linha = new LinhaFarm { Nome = empresa.Nome.Trim() };
                mapa[linha.Nome] = linha;
            }
            linha.Empresa = empresa;
            if (string.IsNullOrWhiteSpace(linha.Codigo)) linha.Codigo = empresa.Codigo;
            if (string.IsNullOrWhiteSpace(linha.Telefone))
            {
                linha.Ddd = empresa.Ddd;
                linha.Telefone = empresa.Telefone;
            }
            if (string.IsNullOrWhiteSpace(linha.Telefone2)) linha.Telefone2 = empresa.Telefone2;
            linha.Cidade = empresa.Cidade;
            linha.Obs = empresa.Obs;
        }
        _lista = mapa.Values
            .Where(f => q.Length == 0 || f.Nome.Contains(q, StringComparison.OrdinalIgnoreCase) || f.Codigo.Contains(q, StringComparison.OrdinalIgnoreCase))
            .OrderByDescending(f => f.Quantidade)
            .Take(400)
            .ToList();
        _grade.RowCount = _lista.Count;
        _grade.Invalidate();
        MostrarHistorico();
    }

    private void MostrarHistorico()
    {
        _chamados = [];
        if (_grade.CurrentRow != null && _grade.CurrentRow.Index >= 0 && _grade.CurrentRow.Index < _lista.Count)
            _chamados = _app.Consultas.DaFarmacia(_lista[_grade.CurrentRow.Index].Nome).Take(80).ToList();
        _historico.RowCount = _chamados.Count;
        _historico.Invalidate();
    }

    private void EditarSelecionada()
    {
        var linha = Selecionada();
        if (linha == null)
        {
            MessageBox.Show("Selecione uma farmácia.", "Vitória Soft Atendimento");
            return;
        }
        if (!PerguntarDados(linha, true)) return;
        var alterados = _app.Consultas.AplicarFarmacia(linha.NomeOriginal, linha.Nome, linha.Codigo, linha.Ddd, linha.Telefone, linha.Telefone2);
        GuardarEmpresa(linha);
        _app.Salvar();
        MessageBox.Show("Farmácia atualizada em " + alterados + " chamados.", "Vitória Soft Atendimento");
        MostrarLista();
    }

    private void Incluir()
    {
        var linha = new LinhaFarm();
        if (!PerguntarDados(linha, false)) return;
        GuardarEmpresa(linha);
        _app.Salvar();
        _busca.Text = linha.Nome;
        MostrarLista();
    }

    private void NovoChamado()
    {
        var linha = Selecionada();
        if (linha == null)
        {
            MessageBox.Show("Selecione uma farmácia.", "Vitória Soft Atendimento");
            return;
        }
        _app.Editar(new Chamado
        {
            Farmacia = linha.Nome,
            Codigo = linha.Codigo,
            Ddd = linha.Ddd,
            Telefone = linha.Telefone,
            Telefone2 = linha.Telefone2,
            Status = "aberto",
            Data = DateTime.Today.ToString("yyyy-MM-dd"),
            HoraInicio = DateTime.Now.ToString("HH:mm")
        });
    }

    private void RemoverCadastro()
    {
        var linha = Selecionada();
        if (linha?.Empresa == null)
        {
            MessageBox.Show("Esta farmácia veio dos chamados. Excluir o cadastro não apaga os atendimentos.", "Vitória Soft Atendimento");
            return;
        }
        if (MessageBox.Show("Remover " + linha.Nome + " do cadastro? Os chamados continuam na fila.", "Vitória Soft Atendimento", MessageBoxButtons.YesNo) != DialogResult.Yes)
            return;
        _app.Banco.Empresas.Remove(linha.Empresa);
        _app.Salvar();
        MostrarLista();
    }

    private void GuardarEmpresa(LinhaFarm linha)
    {
        var empresa = linha.Empresa ?? _app.Banco.Empresas.FirstOrDefault(e => e.Nome.Equals(linha.Nome, StringComparison.OrdinalIgnoreCase));
        if (empresa == null)
        {
            empresa = new Empresa { Id = "emp-" + Guid.NewGuid().ToString("N"), Manual = true };
            _app.Banco.Empresas.Add(empresa);
        }
        empresa.Nome = linha.Nome;
        empresa.Codigo = linha.Codigo;
        empresa.Ddd = linha.Ddd;
        empresa.Telefone = linha.Telefone;
        empresa.Telefone2 = linha.Telefone2;
        empresa.Cidade = linha.Cidade;
        empresa.Obs = linha.Obs;
        empresa.Manual = true;
    }

    private bool PerguntarDados(LinhaFarm linha, bool existente)
    {
        using var form = new Form
        {
            Text = existente ? "Editar farmácia" : "Incluir farmácia",
            FormBorderStyle = FormBorderStyle.FixedDialog,
            StartPosition = FormStartPosition.CenterParent,
            MinimizeBox = false,
            MaximizeBox = false,
            ClientSize = new Size(460, 340),
            Font = Tema.Fonte,
            BackColor = Tema.Fundo
        };
        var nome = Caixa(form, "Nome", linha.Nome, 16);
        var codigo = Caixa(form, "Código", linha.Codigo, 58);
        var ddd = Caixa(form, "DDD", linha.Ddd, 100);
        var tel = Caixa(form, "Telefone", linha.Telefone, 142);
        var tel2 = Caixa(form, "Telefone 2", linha.Telefone2, 184);
        var cidade = Caixa(form, "Cidade", linha.Cidade, 226);
        var ok = Tema.Botao("Salvar", Tema.Teal, Color.White);
        var cancelar = Tema.Botao("Cancelar", Color.White, Tema.Tinta);
        ok.Bounds = new Rectangle(220, 286, 100, 34);
        cancelar.Bounds = new Rectangle(330, 286, 110, 34);
        ok.DialogResult = DialogResult.OK;
        cancelar.DialogResult = DialogResult.Cancel;
        form.AcceptButton = ok;
        form.CancelButton = cancelar;
        form.Controls.Add(ok);
        form.Controls.Add(cancelar);
        if (form.ShowDialog(this) != DialogResult.OK) return false;
        if (string.IsNullOrWhiteSpace(nome.Text))
        {
            MessageBox.Show("Informe o nome da farmácia.", "Vitória Soft Atendimento");
            return false;
        }
        linha.NomeOriginal = linha.Nome;
        linha.Nome = nome.Text.Trim();
        linha.Codigo = codigo.Text.Trim();
        linha.Ddd = ddd.Text.Trim();
        linha.Telefone = tel.Text.Trim();
        linha.Telefone2 = tel2.Text.Trim();
        linha.Cidade = cidade.Text.Trim();
        return true;
    }

    private static TextBox Caixa(Form form, string rotulo, string valor, int topo)
    {
        form.Controls.Add(new Label { Text = rotulo, Bounds = new Rectangle(16, topo, 110, 28), TextAlign = ContentAlignment.MiddleLeft });
        var caixa = new TextBox { Text = valor, Bounds = new Rectangle(130, topo, 310, 28) };
        form.Controls.Add(caixa);
        return caixa;
    }

    private LinhaFarm? Selecionada()
    {
        var i = _grade.CurrentRow?.Index ?? -1;
        return i >= 0 && i < _lista.Count ? _lista[i] : null;
    }

    private void Grade_CellValueNeeded(object? sender, DataGridViewCellValueEventArgs e)
    {
        if (sender == _grade)
        {
            if (e.RowIndex < 0 || e.RowIndex >= _lista.Count) return;
            var f = _lista[e.RowIndex];
            e.Value = e.ColumnIndex switch
            {
                0 => f.Nome,
                1 => Consultas.Fone(f.Ddd, f.Telefone),
                2 => f.Codigo,
                3 => f.Quantidade,
                _ => Consultas.DataBr(f.Ultima)
            };
            return;
        }
        if (e.RowIndex < 0 || e.RowIndex >= _chamados.Count) return;
        var c = _chamados[e.RowIndex];
        e.Value = e.ColumnIndex switch
        {
            0 => Consultas.DataBr(c.Data),
            1 => c.Assunto,
            2 => _app.Consultas.NomeCanal(c.Canal),
            3 => _app.Consultas.NomeStatus(c.Status),
            _ => c.Tecnico
        };
    }

    private void Preparar(DataGridView grade, string[] colunas)
    {
        grade.ReadOnly = true;
        grade.AllowUserToAddRows = false;
        grade.RowHeadersVisible = false;
        grade.SelectionMode = DataGridViewSelectionMode.FullRowSelect;
        grade.MultiSelect = false;
        grade.BackgroundColor = Tema.Cartao;
        grade.BorderStyle = BorderStyle.None;
        grade.AutoSizeColumnsMode = DataGridViewAutoSizeColumnsMode.Fill;
        grade.VirtualMode = true;
        grade.CellValueNeeded += Grade_CellValueNeeded;
        foreach (var nome in colunas) grade.Columns.Add(nome, nome);
    }

    private sealed class LinhaFarm
    {
        public string NomeOriginal = "";
        public string Nome = "";
        public string Codigo = "";
        public string Ddd = "";
        public string Telefone = "";
        public string Telefone2 = "";
        public string Cidade = "";
        public string Obs = "";
        public int Quantidade;
        public string Ultima = "";
        public Empresa? Empresa;
    }
}
