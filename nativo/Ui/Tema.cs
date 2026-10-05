namespace VitoriaSoft.Atendimento.Ui;

public static class Tema
{
    public static readonly Color Fundo = Color.FromArgb(243, 239, 228);
    public static readonly Color Cartao = Color.FromArgb(255, 253, 248);
    public static readonly Color Tinta = Color.FromArgb(27, 25, 20);
    public static readonly Color Mudo = Color.FromArgb(109, 101, 88);
    public static readonly Color Teal = Color.FromArgb(15, 110, 98);
    public static readonly Color Sidebar = Color.FromArgb(16, 36, 31);
    public static readonly Color Ativo = Color.FromArgb(28, 68, 60);
    public static readonly Color Linha = Color.FromArgb(227, 217, 198);
    public static readonly Color Perigo = Color.FromArgb(159, 45, 45);
    public static readonly Font Fonte = new("Segoe UI", 10f);
    public static readonly Font Titulo = new("Segoe UI", 18f, FontStyle.Bold);
    public static readonly Font Numero = new("Segoe UI", 22f, FontStyle.Bold);

    public static Button Botao(string texto, Color fundo, Color tinta)
    {
        var botao = new Button
        {
            Text = texto,
            FlatStyle = FlatStyle.Flat,
            BackColor = fundo,
            ForeColor = tinta,
            Height = 36,
            Cursor = Cursors.Hand,
            Font = Fonte
        };
        botao.FlatAppearance.BorderColor = Linha;
        botao.FlatAppearance.BorderSize = 1;
        return botao;
    }

    public static Label TituloDe(string texto) => new()
    {
        Text = texto,
        Font = Titulo,
        ForeColor = Tinta,
        AutoSize = true,
        Dock = DockStyle.Top,
        Padding = new Padding(0, 0, 0, 4)
    };

    public static Label Subtitulo(string texto) => new()
    {
        Text = texto,
        ForeColor = Mudo,
        AutoSize = true,
        Dock = DockStyle.Top,
        Padding = new Padding(0, 0, 0, 12)
    };

    public static string? Perguntar(IWin32Window? dono, string titulo, string rotulo, string atual, bool multilinha = false)
    {
        using var form = new Form
        {
            Text = titulo,
            FormBorderStyle = FormBorderStyle.FixedDialog,
            StartPosition = FormStartPosition.CenterParent,
            MinimizeBox = false,
            MaximizeBox = false,
            ClientSize = new Size(520, multilinha ? 320 : 168),
            Font = Fonte,
            BackColor = Fundo
        };
        var label = new Label { Text = rotulo, ForeColor = Mudo, Bounds = new Rectangle(16, 14, 488, 22) };
        var caixa = new TextBox
        {
            Text = atual,
            Bounds = new Rectangle(16, 40, 488, multilinha ? 200 : 28),
            Multiline = multilinha,
            ScrollBars = multilinha ? ScrollBars.Vertical : ScrollBars.None
        };
        var ok = Botao("Salvar", Teal, Color.White);
        var cancelar = Botao("Cancelar", Color.White, Tinta);
        ok.Bounds = new Rectangle(280, multilinha ? 258 : 86, 108, 36);
        cancelar.Bounds = new Rectangle(396, multilinha ? 258 : 86, 108, 36);
        ok.DialogResult = DialogResult.OK;
        cancelar.DialogResult = DialogResult.Cancel;
        form.AcceptButton = ok;
        form.CancelButton = cancelar;
        form.Controls.Add(label);
        form.Controls.Add(caixa);
        form.Controls.Add(ok);
        form.Controls.Add(cancelar);
        return form.ShowDialog(dono) == DialogResult.OK ? caixa.Text.Trim() : null;
    }

    public static void Abrir(string url)
    {
        try
        {
            System.Diagnostics.Process.Start(new System.Diagnostics.ProcessStartInfo(url) { UseShellExecute = true });
        }
        catch (Exception ex)
        {
            MessageBox.Show("Não foi possível abrir " + url + ".\n" + ex.Message, "Vitória Soft Atendimento");
        }
    }
}
