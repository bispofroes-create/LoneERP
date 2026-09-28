namespace Lone.Contracts.Seguranca;

/// <summary>Linha da lista de usuários.</summary>
public sealed class UsuarioResumo
{
    public Guid Id { get; set; }
    public string Login { get; set; } = string.Empty;
    public string Nome { get; set; } = string.Empty;
    public bool Ativo { get; set; }
    public DateTime? BloqueadoAte { get; set; }
    public DateTime? UltimoAcessoEm { get; set; }
    public List<string> Perfis { get; set; } = new();

    public bool Bloqueado => BloqueadoAte > DateTime.UtcNow;

    public string Detalhe
    {
        get
        {
            var partes = new List<string> { Login };
            if (Perfis.Count > 0) partes.Add(string.Join(", ", Perfis));
            if (!Ativo) partes.Add("Inativo");
            else if (Bloqueado) partes.Add("Bloqueado");
            return string.Join("  ·  ", partes);
        }
    }
}

/// <summary>Cadastro de um usuário. Datas em UTC. A senha nunca vem nem vai neste objeto.</summary>
public sealed class UsuarioDto
{
    public Guid Id { get; set; }
    public byte[]? Versao { get; set; }
    public string Nome { get; set; } = string.Empty;
    public string Login { get; set; } = string.Empty;
    public string? Email { get; set; }
    public bool Ativo { get; set; } = true;
    public bool DeveTrocarSenha { get; set; } = true;
    public List<UsuarioPerfilDto> Perfis { get; set; } = new();

    /// <summary>Quem é o usuário no cadastro de Pessoas (opcional; uma pessoa por usuário). Fase 2a.</summary>
    public Guid? PessoaId { get; set; }

    /// <summary>Somente leitura: nome da pessoa.</summary>
    public string? Pessoa { get; set; }

    // Somente leitura (preenchidos pela API).
    public DateTime? BloqueadoAte { get; set; }
    public DateTime? UltimoAcessoEm { get; set; }
}

/// <summary>Perfil do usuário numa empresa do grupo (EmpresaId nulo = todas as empresas).</summary>
public sealed record UsuarioPerfilDto(Guid PerfilId, Guid? EmpresaId);

/// <summary>Inclusão ou alteração. NovaSenha é obrigatória na inclusão; na alteração, só para redefinir.</summary>
public sealed record SalvarUsuarioRequisicao(UsuarioDto Usuario, string? NovaSenha);

public sealed class PerfilResumo
{
    public Guid Id { get; set; }
    public string Nome { get; set; } = string.Empty;
    public bool Administrador { get; set; }
    public bool Ativo { get; set; }
    public int QuantidadeUsuarios { get; set; }

    public string Detalhe =>
        $"{(Administrador ? "Administrador · " : string.Empty)}{QuantidadeUsuarios} usuário(s){(Ativo ? string.Empty : " · Inativo")}";
}

public sealed class PerfilDto
{
    public Guid Id { get; set; }
    public byte[]? Versao { get; set; }
    public string Nome { get; set; } = string.Empty;
    public string? Descricao { get; set; }
    public bool Administrador { get; set; }
    public bool Ativo { get; set; } = true;

    /// <summary>Até onde quem tem o perfil enxerga em Pessoas e no Comercial (Fase 2a). Ignorado quando Administrador.</summary>
    public Lone.Domain.Enums.AlcanceComercial AlcanceComercial { get; set; }

    /// <summary>Códigos do catálogo (Permissoes). Ignorado quando Administrador.</summary>
    public List<string> Permissoes { get; set; } = new();
}
