using VitoriaSoft.Atendimento.Servico;
using VitoriaSoft.Atendimento.Ui;

namespace VitoriaSoft.Atendimento;

internal static class Program
{
    [STAThread]
    private static void Main(string[] args)
    {
        if (args.Contains("--checar"))
        {
            var caminho = Path.Combine(Path.GetTempPath(), "vs-checar.txt");
            try
            {
                var banco = Repositorio.Carregar();
                File.WriteAllText(caminho, banco.Chamados.Count + " chamados, " + banco.Ramais.Count + " ramais");
            }
            catch (Exception ex)
            {
                File.WriteAllText(caminho, ex.ToString());
            }
            return;
        }

        Application.EnableVisualStyles();
        Application.SetCompatibleTextRenderingDefault(false);
        Application.SetHighDpiMode(HighDpiMode.PerMonitorV2);
        Application.Run(new MainForm());
    }
}
