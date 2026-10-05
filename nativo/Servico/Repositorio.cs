using System.Text.Json;
using System.Text.Json.Serialization;
using VitoriaSoft.Atendimento.Modelo;

namespace VitoriaSoft.Atendimento.Servico;

public static class Repositorio
{
    public static readonly JsonSerializerOptions Opcoes = new()
    {
        PropertyNamingPolicy = JsonNamingPolicy.CamelCase,
        PropertyNameCaseInsensitive = true,
        DefaultIgnoreCondition = JsonIgnoreCondition.WhenWritingNull,
        WriteIndented = false
    };

    public static string Caminho => Path.Combine(
        Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData),
        "VitoriaSoft", "Atendimento", "dados.json");

    public static Banco Carregar()
    {
        Directory.CreateDirectory(Path.GetDirectoryName(Caminho)!);
        if (!File.Exists(Caminho))
        {
            using var origem = typeof(Repositorio).Assembly.GetManifestResourceStream("dados-inicial.json")
                ?? throw new InvalidOperationException("A base inicial do atendimento não foi encontrada.");
            using var destino = File.Create(Caminho);
            origem.CopyTo(destino);
        }

        var banco = JsonSerializer.Deserialize<Banco>(File.ReadAllText(Caminho), Opcoes)
            ?? throw new InvalidOperationException("Não foi possível ler os chamados.");
        banco.Config ??= new Configuracao();
        banco.Chamados ??= [];
        banco.Ramais ??= [];
        banco.DicasTelefone ??= [];
        banco.Empresas ??= [];
        banco.Config.VsuUrl ??= "";
        banco.Config.VsuCaminho ??= "";
        banco.Config.VsuChave ??= "";
        banco.Config.VsuEsquema ??= "Bearer";
        banco.Config.Canais ??= [];
        banco.Config.Status ??= [];
        banco.Config.Tecnicos ??= [];
        banco.Config.Frases ??= [];
        banco.Config.CamposExtras ??= [];
        return banco;
    }

    public static void Salvar(Banco banco)
    {
        var pasta = Path.GetDirectoryName(Caminho)!;
        Directory.CreateDirectory(pasta);
        var temporario = Caminho + ".tmp";
        File.WriteAllText(temporario, JsonSerializer.Serialize(banco, Opcoes));
        if (File.Exists(Caminho)) File.Copy(Caminho, Caminho + ".bak", true);
        File.Move(temporario, Caminho, true);
    }
}
