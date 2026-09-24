using Lone.Core.Auditoria;

namespace Lone.Core.Entidades;

/// <summary>
/// Controle de acesso do usuário, em tabela própria: muda a cada login sem alterar a versão do cadastro
/// (senão um administrador editando o usuário receberia falso aviso de edição simultânea).
/// </summary>
[NaoAuditar]
public class UsuarioAcesso
{
    public int UsuarioId { get; set; }
    public int TentativasFalhas { get; set; }
    public DateTime? BloqueadoAte { get; set; }
    public DateTime? UltimoAcessoEm { get; set; }

    public bool EstaBloqueado(DateTime agora) => BloqueadoAte is { } ate && ate > agora;
}
