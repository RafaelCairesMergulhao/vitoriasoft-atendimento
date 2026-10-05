using VitoriaSoft.Atendimento.Modelo;
using VitoriaSoft.Atendimento.Servico;

namespace VitoriaSoft.Atendimento.Ui;

public sealed class MainForm : Form, IApp
{
    private readonly Panel _conteudo = new() { Dock = DockStyle.Fill, BackColor = Tema.Fundo, Padding = new Padding(18) };
    private readonly List<Button> _menu = [];
    private PainelView? _painel;
    private FilaView? _fila;
    private FichaView? _ficha;
    private FarmaciasView? _farmacias;
    private RamaisView? _ramais;
    private AjustesView? _ajustes;
    private readonly Button _voltar;
    private readonly Label _caminho = new() { Dock = DockStyle.Fill, TextAlign = ContentAlignment.MiddleLeft, ForeColor = Tema.Tinta, Padding = new Padding(8, 0, 0, 0) };
    private readonly Stack<string> _historico = new();
    private string _tela = "";
    private bool _pronto;

    public Banco Banco { get; private set; } = new();
    public Consultas Consultas { get; private set; }

    public MainForm()
    {
        Consultas = new Consultas(Banco);
        Text = "Vitória Soft Atendimento 2.0";
        Font = Tema.Fonte;
        BackColor = Tema.Fundo;
        ForeColor = Tema.Tinta;
        StartPosition = FormStartPosition.CenterScreen;
        Size = new Size(1320, 840);
        MinimumSize = new Size(1100, 700);
        _voltar = Tema.Botao("←  Voltar", Color.White, Tema.Tinta);
        _voltar.Width = 120;
        _voltar.Dock = DockStyle.Left;
        _voltar.Visible = false;
        _voltar.Click += (_, _) => Voltar();
        KeyPreview = true;
        KeyDown += (_, e) =>
        {
            if (e.Control && e.KeyCode == Keys.N)
            {
                e.SuppressKeyPress = true;
                NovoChamado();
            }
        };

        var aviso = new Label
        {
            Text = "Abrindo a base de atendimento…",
            Dock = DockStyle.Fill,
            TextAlign = ContentAlignment.MiddleCenter,
            ForeColor = Tema.Mudo,
            Font = Tema.Titulo
        };
        Controls.Add(aviso);
    }

    protected override void OnShown(EventArgs e)
    {
        base.OnShown(e);
        if (_pronto) return;
        UseWaitCursor = true;
        try
        {
            Banco = Repositorio.Carregar();
            Consultas = new Consultas(Banco);
            Montar();
            _pronto = true;
            Ir("painel");
        }
        catch (Exception ex)
        {
            MessageBox.Show("Não foi possível abrir os chamados.\n\n" + ex.Message, Text, MessageBoxButtons.OK, MessageBoxIcon.Error);
            Close();
        }
        finally
        {
            UseWaitCursor = false;
        }
    }

    private void Montar()
    {
        Controls.Clear();
        var menu = new Panel { Dock = DockStyle.Left, Width = 230, BackColor = Tema.Sidebar, Padding = new Padding(14) };
        var marca = new Label
        {
            Text = "VS   Vitória Soft\r\n      Atendimento",
            ForeColor = Color.FromArgb(246, 241, 230),
            Font = new Font("Segoe UI", 12f, FontStyle.Bold),
            Dock = DockStyle.Top,
            Height = 64
        };
        var novo = Tema.Botao("Novo chamado", Color.FromArgb(244, 239, 228), Tema.Sidebar);
        novo.Dock = DockStyle.Top;
        novo.Height = 40;
        novo.Click += (_, _) => NovoChamado();
        menu.Controls.Add(Item("Personalizar", "ajustes"));
        menu.Controls.Add(Item("Ramais", "ramais"));
        menu.Controls.Add(Item("Farmácias", "farmacias"));
        menu.Controls.Add(Item("Chamados", "fila"));
        menu.Controls.Add(Item("Painel", "painel"));
        menu.Controls.Add(novo);
        menu.Controls.Add(marca);
        var rodape = new Label
        {
            Text = "Os dados ficam só neste computador.\r\nAtalho: Ctrl+N",
            Dock = DockStyle.Bottom,
            Height = 48,
            ForeColor = Color.FromArgb(183, 173, 157)
        };
        menu.Controls.Add(rodape);
        var trilha = new Panel { Dock = DockStyle.Top, Height = 52, BackColor = Tema.Cartao, Padding = new Padding(8, 8, 12, 8) };
        trilha.Controls.Add(_caminho);
        trilha.Controls.Add(_voltar);
        Controls.Add(_conteudo);
        Controls.Add(trilha);
        Controls.Add(menu);

        _painel = new PainelView(this);
        _fila = new FilaView(this);
        _ficha = new FichaView(this);
        _farmacias = new FarmaciasView(this);
        _ramais = new RamaisView(this);
        _ajustes = new AjustesView(this);
    }

    private Button Item(string texto, string tela)
    {
        var botao = new Button
        {
            Text = texto,
            Dock = DockStyle.Top,
            Height = 40,
            FlatStyle = FlatStyle.Flat,
            ForeColor = Color.FromArgb(239, 231, 216),
            BackColor = Tema.Sidebar,
            TextAlign = ContentAlignment.MiddleLeft,
            Padding = new Padding(8, 0, 0, 0),
            Cursor = Cursors.Hand,
            Tag = tela
        };
        botao.FlatAppearance.BorderSize = 0;
        botao.Click += (_, _) => Ir(tela);
        _menu.Add(botao);
        return botao;
    }

    public void Salvar()
    {
        Consultas.Invalidar();
        Repositorio.Salvar(Banco);
    }

    public void NovoChamado()
    {
        _ficha!.Carregar(null);
        Ir("ficha");
    }

    public void Editar(Chamado chamado)
    {
        _ficha!.Carregar(chamado);
        Ir("ficha");
    }

    public void AbrirFila(string status, bool somenteHoje)
    {
        _fila!.DefinirFiltro(status, somenteHoje);
        Ir("fila");
    }

    public void Voltar()
    {
        var destino = _historico.Count > 0 ? _historico.Pop() : "painel";
        Mostrar(destino, false);
    }

    public void Ir(string tela) => Mostrar(tela, true);

    private void Mostrar(string tela, bool empilhar)
    {
        if (empilhar && _tela.Length > 0 && _tela != tela) _historico.Push(_tela);
        _tela = tela;
        _voltar.Visible = tela != "painel";
        _caminho.Text = string.Join("    ›    ", _historico.Reverse().Append(tela).Select(NomeTela));
        foreach (var botao in _menu)
            botao.BackColor = (string?)botao.Tag == tela ? Tema.Ativo : Tema.Sidebar;
        Control view = tela switch
        {
            "fila" => _fila!,
            "ficha" => _ficha!,
            "farmacias" => _farmacias!,
            "ramais" => _ramais!,
            "ajustes" => _ajustes!,
            _ => _painel!
        };
        if (view is IAtualizavel atualizavel && tela != "ficha") atualizavel.Atualizar();
        view.Dock = DockStyle.Fill;
        _conteudo.Controls.Clear();
        _conteudo.Controls.Add(view);
    }

    private static string NomeTela(string tela) => tela switch
    {
        "fila" => "Chamados",
        "ficha" => "Ficha do chamado",
        "farmacias" => "Farmácias",
        "ramais" => "Ramais",
        "ajustes" => "Personalizar",
        _ => "Painel do dia"
    };
}

public interface IAtualizavel
{
    void Atualizar();
}
