namespace Lone.Contracts.Colaboradores;

/// <summary>Cargo da estrutura organizacional.</summary>
public sealed class CargoDto
{
    public Guid Id { get; set; }
    public byte[]? Versao { get; set; }
    public string Nome { get; set; } = string.Empty;
    public bool Ativo { get; set; } = true;

    /// <summary>Código CBO (opcional).</summary>
    public int? OcupacaoCboId { get; set; }

    /// <summary>Somente leitura: "0000-00 · título" da ocupação.</summary>
    public string? Ocupacao { get; set; }

    /// <summary>Somente leitura: lotações em aberto com este cargo (só para quem gerencia).</summary>
    public int QuantidadeUsos { get; set; }
}

/// <summary>Departamento da estrutura organizacional.</summary>
public sealed class DepartamentoDto
{
    public Guid Id { get; set; }
    public byte[]? Versao { get; set; }
    public string Nome { get; set; } = string.Empty;
    public bool Ativo { get; set; } = true;

    /// <summary>Somente leitura: lotações em aberto com este departamento (só para quem gerencia).</summary>
    public int QuantidadeUsos { get; set; }
}

/// <summary>Setor da estrutura organizacional.</summary>
public sealed class SetorDto
{
    public Guid Id { get; set; }
    public byte[]? Versao { get; set; }
    public string Nome { get; set; } = string.Empty;
    public bool Ativo { get; set; } = true;

    public Guid DepartamentoId { get; set; }

    /// <summary>Somente leitura: nome do departamento.</summary>
    public string? Departamento { get; set; }

    /// <summary>Somente leitura: lotações em aberto com este setor (só para quem gerencia).</summary>
    public int QuantidadeUsos { get; set; }
}

/// <summary>Centro de custo da estrutura organizacional.</summary>
public sealed class CentroCustoDto
{
    public Guid Id { get; set; }
    public byte[]? Versao { get; set; }
    public string Nome { get; set; } = string.Empty;
    public bool Ativo { get; set; } = true;

    public string Codigo { get; set; } = string.Empty;
    public Guid? PaiId { get; set; }
    public bool Analitico { get; set; } = true;

    /// <summary>Somente leitura: profundidade na árvore (0 = raiz), para recuar o nome na lista.</summary>
    public int Nivel { get; set; }

    /// <summary>Somente leitura: lotações em aberto com este centro de custo (só para quem gerencia).</summary>
    public int QuantidadeUsos { get; set; }
}

/// <summary>Vínculo de trabalho com uma empresa do grupo (aba "Colaborador" da ficha).</summary>
public sealed class VinculoDto
{
    public Guid Id { get; set; }
    public Guid EmpresaId { get; set; }

    /// <summary>Somente leitura: nome da empresa.</summary>
    public string? Empresa { get; set; }
    public string? Matricula { get; set; }
    public Lone.Domain.Enums.TipoVinculo Tipo { get; set; }
    public DateOnly AdmissaoEm { get; set; }
    public DateOnly? DesligamentoEm { get; set; }
    public string? MotivoDesligamento { get; set; }
    public decimal? JornadaSemanal { get; set; }
    public string? Observacoes { get; set; }

    /// <summary>Lotações do vínculo (a mais recente primeiro).</summary>
    public List<LotacaoDto> Lotacoes { get; set; } = new();
}

/// <summary>Período de lotação: cargo, departamento, setor, centro de custo e gestor juntos.</summary>
public sealed class LotacaoDto
{
    public Guid Id { get; set; }
    public DateOnly InicioEm { get; set; }
    public DateOnly? FimEm { get; set; }
    public Guid? CargoId { get; set; }
    public Guid? DepartamentoId { get; set; }
    public Guid? SetorId { get; set; }
    public Guid? CentroCustoId { get; set; }
    public Guid? GestorId { get; set; }

    /// <summary>Somente leitura: nome do gestor.</summary>
    public string? Gestor { get; set; }
}

/// <summary>Pessoa para escolher numa lista (ex.: gestor).</summary>
public sealed record PessoaOpcaoDto(Guid Id, string Nome);

/// <summary>Tudo que a aba "Colaborador" precisa para as escolhas, numa chamada só (lida quando a aba abre).</summary>
public sealed class ColaboradorOpcoesDto
{
    public List<Lone.Contracts.Empresas.EmpresaResumo> Empresas { get; set; } = new();

    /// <summary>Pessoas com vínculo ativo (podem ser gestoras).</summary>
    public List<PessoaOpcaoDto> Gestores { get; set; } = new();

    /// <summary>Com os desativados (a ficha oferece só os ativos e mostra os gravados).</summary>
    public List<CargoDto> Cargos { get; set; } = new();
    public List<DepartamentoDto> Departamentos { get; set; } = new();
    public List<SetorDto> Setores { get; set; } = new();
    public List<CentroCustoDto> CentrosCusto { get; set; } = new();
}
