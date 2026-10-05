namespace VitoriaSoft.Atendimento.Modelo;

public sealed class Banco
{
    public int Versao { get; set; } = 1;
    public Configuracao Config { get; set; } = new();
    public List<Chamado> Chamados { get; set; } = [];
    public List<RamalInfo> Ramais { get; set; } = [];
    public List<string> DicasTelefone { get; set; } = [];
    public List<Empresa> Empresas { get; set; } = [];
}

public sealed class Configuracao
{
    public string Empresa { get; set; } = "Vitória Soft";
    public string TecnicoLocal { get; set; } = "";
    public List<Opcao> Canais { get; set; } = [];
    public List<Opcao> Status { get; set; } = [];
    public List<string> Tecnicos { get; set; } = [];
    public List<string> Frases { get; set; } = [];
    public List<CampoExtra> CamposExtras { get; set; } = [];
    public string VsuUrl { get; set; } = "";
    public string VsuCaminho { get; set; } = "";
    public string VsuChave { get; set; } = "";
    public string VsuEsquema { get; set; } = "Bearer";
}

public sealed class Opcao
{
    public string Id { get; set; } = "";
    public string Nome { get; set; } = "";
    public string Cor { get; set; } = "#0F6E62";
}

public sealed class CampoExtra
{
    public string Id { get; set; } = "";
    public string Nome { get; set; } = "";
    public string Tipo { get; set; } = "texto";
    public string Opcoes { get; set; } = "";
}

public sealed class Nota
{
    public string Em { get; set; } = "";
    public string Texto { get; set; } = "";
    public string Tecnico { get; set; } = "";
}

public sealed class Chamado
{
    public string Id { get; set; } = "";
    public string Protocolo { get; set; } = "";
    public int? OrigemLinha { get; set; }
    public string Canal { get; set; } = "";
    public bool CanalInformado { get; set; }
    public string Data { get; set; } = "";
    public string HoraInicio { get; set; } = "";
    public string HoraFim { get; set; } = "";
    public string Status { get; set; } = "aberto";
    public string Cliente { get; set; } = "";
    public string Farmacia { get; set; } = "";
    public string Codigo { get; set; } = "";
    public string Tecnico { get; set; } = "";
    public string Assunto { get; set; } = "";
    public string Ddd { get; set; } = "";
    public string Telefone { get; set; } = "";
    public string Telefone2 { get; set; } = "";
    public string Obs { get; set; } = "";
    public string Agendamento { get; set; } = "";
    public string Prioridade { get; set; } = "normal";
    public string Marcador { get; set; } = "";
    public string Ticket { get; set; } = "";
    public List<Nota> Historico { get; set; } = [];
    public Dictionary<string, string> Extras { get; set; } = [];
    public string CriadoEm { get; set; } = "";
    public string AtualizadoEm { get; set; } = "";
}

public sealed class RamalInfo
{
    [System.Text.Json.Serialization.JsonPropertyName("ramal")]
    public string Numero { get; set; } = "";
    public string Nome { get; set; } = "";
    public string Almoco { get; set; } = "";
    public string Ddd { get; set; } = "";
    public string Telefone { get; set; } = "";
    public string Discagem { get; set; } = "";
}

public sealed class Empresa
{
    public string Id { get; set; } = "";
    public string Nome { get; set; } = "";
    public string Codigo { get; set; } = "";
    public string Ddd { get; set; } = "";
    public string Telefone { get; set; } = "";
    public string Telefone2 { get; set; } = "";
    public string Cidade { get; set; } = "";
    public string Obs { get; set; } = "";
    public bool Manual { get; set; } = true;
}

public sealed class ResumoFarmacia
{
    public string Nome { get; set; } = "";
    public int Quantidade { get; set; }
    public string Ddd { get; set; } = "";
    public string Telefone { get; set; } = "";
    public string Telefone2 { get; set; } = "";
    public string Codigo { get; set; } = "";
    public string Ultima { get; set; } = "";
}
