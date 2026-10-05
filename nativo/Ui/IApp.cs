using VitoriaSoft.Atendimento.Modelo;
using VitoriaSoft.Atendimento.Servico;

namespace VitoriaSoft.Atendimento.Ui;

public interface IApp
{
    Banco Banco { get; }
    Consultas Consultas { get; }
    void Salvar();
    void NovoChamado();
    void Editar(Chamado chamado);
    void Ir(string tela);
    void Voltar();
    void AbrirFila(string status, bool somenteHoje);
}
