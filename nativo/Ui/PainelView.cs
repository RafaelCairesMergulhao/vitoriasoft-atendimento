using System.Globalization;
using VitoriaSoft.Atendimento.Servico;

namespace VitoriaSoft.Atendimento.Ui;

public sealed class PainelView : UserControl, IAtualizavel
{
    private readonly IApp _app;
    private readonly FlowLayoutPanel _cartoes = new() { Dock = DockStyle.Top, Height = 108, WrapContents = false };
    private readonly ListaChamados _hoje;
    private readonly ListaChamados _abertos;
    private readonly Label _subtitulo;
    private bool _atualizando;

    public PainelView(IApp app)
    {
        _app = app;
        Dock = DockStyle.Fill;
        BackColor = Tema.Fundo;
        _hoje = new ListaChamados(app, "Chamados de hoje", Atualizar);
        _abertos = new ListaChamados(app, "Chamados em aberto", Atualizar);
        var topo = new Panel { Dock = DockStyle.Top, Height = 72 };
        var quando = DateTime.Today.ToString("dddd, dd 'de' MMMM", new CultureInfo("pt-BR"));
        _subtitulo = Tema.Subtitulo(char.ToUpper(quando[0]) + quando[1..] + ". Altere farmácia, assunto, canal, técnico e situação direto na lista.");
        topo.Controls.Add(_subtitulo);
        topo.Controls.Add(Tema.TituloDe("Painel do dia"));
        var grade = new TableLayoutPanel { Dock = DockStyle.Fill, ColumnCount = 2, RowCount = 1, Padding = new Padding(0, 8, 0, 0) };
        grade.ColumnStyles.Add(new ColumnStyle(SizeType.Percent, 50));
        grade.ColumnStyles.Add(new ColumnStyle(SizeType.Percent, 50));
        grade.Controls.Add(Caixa(_hoje), 0, 0);
        grade.Controls.Add(Caixa(_abertos), 1, 0);
        Controls.Add(grade);
        Controls.Add(_cartoes);
        Controls.Add(topo);
    }

    public void Atualizar()
    {
        if (_atualizando) return;
        _atualizando = true;
        try
        {
            var hoje = DateTime.Today.ToString("yyyy-MM-dd");
            var chamados = _app.Banco.Chamados;
            var abertos = 0;
            var andamento = 0;
            var retorno = 0;
            var deHoje = 0;
            foreach (var c in chamados)
            {
                if (c.Status == "aberto") abertos++;
                else if (c.Status == "andamento") andamento++;
                else if (c.Status is "retorno" or "agendado") retorno++;
                if (c.Data == hoje) deHoje++;
            }
            _cartoes.Controls.Clear();
            _cartoes.Controls.Add(Cartao("Em aberto", abertos.ToString(), () => _app.AbrirFila("aberto", false)));
            _cartoes.Controls.Add(Cartao("Em atendimento", andamento.ToString(), () => _app.AbrirFila("andamento", false)));
            _cartoes.Controls.Add(Cartao("Retorno ou agendado", retorno.ToString(), () => _app.AbrirFila("retorno-agendado", false)));
            _cartoes.Controls.Add(Cartao("Registrados hoje", deHoje.ToString(), () => _app.AbrirFila("todos", true)));

            var doDia = _app.Consultas.DoDia(hoje);
            var fila = _app.Consultas.EmAberto();
            _hoje.Mostrar(doDia, 400, doDia.Count == 0
                ? "Nenhum chamado hoje. Use Novo chamado no menu."
                : "Atendido encerra sem pedir canal. Abrir mostra a ficha.");
            _abertos.Mostrar(fila, 300, "Para encerrar vários de uma vez, abra Chamados e use o botão verde.");
        }
        finally
        {
            _atualizando = false;
        }
    }

    private static Panel Caixa(Control miolo)
    {
        var caixa = new Panel { Dock = DockStyle.Fill, Padding = new Padding(0, 0, 8, 0), BackColor = Tema.Fundo };
        miolo.Dock = DockStyle.Fill;
        caixa.Controls.Add(miolo);
        return caixa;
    }

    private static Panel Cartao(string rotulo, string valor, Action aoClicar)
    {
        var painel = new Panel { Width = 230, Height = 96, BackColor = Tema.Cartao, Margin = new Padding(0, 0, 12, 0), Cursor = Cursors.Hand };
        var numero = new Label { Text = valor, Font = Tema.Numero, ForeColor = Tema.Teal, Dock = DockStyle.Fill, TextAlign = ContentAlignment.MiddleLeft, Padding = new Padding(12, 0, 0, 0), Cursor = Cursors.Hand };
        var titulo = new Label { Text = rotulo, ForeColor = Tema.Mudo, Dock = DockStyle.Top, Height = 28, Padding = new Padding(12, 8, 0, 0), Cursor = Cursors.Hand };
        void Clique(object? sender, EventArgs e) => aoClicar();
        painel.Click += Clique;
        numero.Click += Clique;
        titulo.Click += Clique;
        painel.Controls.Add(numero);
        painel.Controls.Add(titulo);
        return painel;
    }
}
