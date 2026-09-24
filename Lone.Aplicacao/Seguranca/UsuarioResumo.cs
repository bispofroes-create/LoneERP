namespace Lone.Aplicacao.Seguranca;

public sealed class UsuarioResumo
{
    public int Id { get; init; }
    public string Login { get; init; } = string.Empty;
    public string Nome { get; init; } = string.Empty;
    public bool Ativo { get; init; }
    public DateTime? BloqueadoAte { get; init; }
    public DateTime? UltimoAcessoEm { get; init; }
    public List<string> Perfis { get; init; } = new();

    public string Detalhe
    {
        get
        {
            var partes = new List<string> { Login };
            if (Perfis.Count > 0) partes.Add(string.Join(", ", Perfis));
            if (!Ativo) partes.Add("Inativo");
            else if (BloqueadoAte > DateTime.Now) partes.Add("Bloqueado");
            return string.Join("  ·  ", partes);
        }
    }
}

public sealed class PerfilResumo
{
    public int Id { get; init; }
    public string Nome { get; init; } = string.Empty;
    public bool Administrador { get; init; }
    public bool Ativo { get; init; }
    public int QuantidadeUsuarios { get; init; }

    public string Detalhe =>
        $"{(Administrador ? "Administrador · " : string.Empty)}{QuantidadeUsuarios} usuário(s){(Ativo ? string.Empty : " · Inativo")}";
}
