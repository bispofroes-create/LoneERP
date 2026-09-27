using Lone.Domain.Enums;
using Lone.Domain.Validacao;

namespace Lone.Contracts.Pessoas;

/// <summary>Filtro da lista de pessoas (vai na query string: ?texto=...&amp;papel=Cliente).</summary>
public sealed class FiltroPessoas
{
    public const int LimiteMaximo = 500;

    /// <summary>Nome, código, CPF/CNPJ, telefone ou e-mail.</summary>
    public string? Texto { get; set; }

    /// <summary>Só pessoas com este papel ativo (Id do cadastro de papéis). Nulo = todas.</summary>
    public Guid? PapelId { get; set; }

    public bool IncluirInativos { get; set; }

    /// <summary>Só pessoas com esta etiqueta. Nulo = todas.</summary>
    public Guid? EtiquetaId { get; set; }

    /// <summary>Só cadastros com município antigo (texto) ainda por escolher na tabela do IBGE.</summary>
    public bool MunicipioACorrigir { get; set; }
    public int Limite { get; set; } = LimiteMaximo;

    /// <summary>Só pessoas desta natureza (física, jurídica, estrangeiro). Nulo = todas.</summary>
    public NaturezaPessoa? Natureza { get; set; }

    /// <summary>Só cadastros com situação "Ativo" (sem os "Em análise").</summary>
    public bool SomenteAtivos { get; set; }

    /// <summary>Só cadastros inativos ou arquivados (os que somem da busca padrão).</summary>
    public bool SomenteInativos { get; set; }
}

/// <summary>Uma página da lista de Pessoas, com o total que atende aos filtros (para "Mostrando 1–50 de 1.248").</summary>
public sealed class PaginaListaPessoas
{
    public const int TamanhoPadrao = 50;
    public const int TamanhoMaximo = 200;

    public List<PessoaResumo> Itens { get; set; } = new();
    public int Total { get; set; }
    /// <summary>Começa em 1.</summary>
    public int Pagina { get; set; } = 1;
    public int TamanhoPagina { get; set; } = TamanhoPadrao;
}

/// <summary>Resultado de uma gravação: a pessoa como ficou e avisos que não impediram a gravação.</summary>
public sealed class ResultadoSalvarPessoa
{
    public PessoaDto Pessoa { get; set; } = new();
    public List<string> Avisos { get; set; } = new();
}

/// <summary>Linha da lista de pessoas (só o necessário para exibir e buscar).</summary>
public sealed class PessoaResumo
{
    public Guid Id { get; set; }
    public int Codigo { get; set; }
    public string Nome { get; set; } = string.Empty;
    public NaturezaPessoa Natureza { get; set; }
    public string? DocumentoPrincipal { get; set; }
    public string? CnpjPrincipal { get; set; }
    public int QuantidadeEstabelecimentos { get; set; }
    public SituacaoPessoa Situacao { get; set; }
    /// <summary>Nomes dos papéis ativos, na ordem do cadastro de papéis.</summary>
    public List<string> Papeis { get; set; } = new();
    public string? Cidade { get; set; }
    public string? Uf { get; set; }

    /// <summary>Tem texto de município antigo esperando a escolha do município certo.</summary>
    public bool MunicipioACorrigir { get; set; }

    public string CodigoFormatado => Codigo.ToString("000000");

    public string DocumentoFormatado => Natureza switch
    {
        NaturezaPessoa.Juridica => Documento.Formatar(CnpjPrincipal) +
                                   (QuantidadeEstabelecimentos > 1 ? $" (+{QuantidadeEstabelecimentos - 1})" : string.Empty),
        NaturezaPessoa.Fisica => Documento.Formatar(DocumentoPrincipal),
        _ => DocumentoPrincipal ?? string.Empty
    };

    public string PapeisTexto => string.Join(" · ", Papeis);

    public string Local => Cidade is null ? string.Empty : Uf is null ? Cidade : $"{Cidade}/{Uf}";

    /// <summary>Coluna "Tipo" da lista: PF, PJ ou EX.</summary>
    public string NaturezaSigla => Natureza switch
    {
        NaturezaPessoa.Fisica => "PF",
        NaturezaPessoa.Juridica => "PJ",
        _ => "EX"
    };

    public string NaturezaTexto => Natureza switch
    {
        NaturezaPessoa.Fisica => "Pessoa física",
        NaturezaPessoa.Juridica => "Pessoa jurídica",
        _ => "Estrangeiro"
    };

    /// <summary>Texto do selo de situação da tabela (sempre preenchido; a cor nunca é a única indicação).</summary>
    public string SituacaoSelo => Situacao switch
    {
        SituacaoPessoa.Ativo => "Ativo",
        SituacaoPessoa.EmAnalise => "Em análise",
        SituacaoPessoa.Inativo => "Inativo",
        _ => "Arquivado"
    };

    public bool EmUso => Situacao is SituacaoPessoa.Ativo or SituacaoPessoa.EmAnalise;

    public string SituacaoTexto => Situacao == SituacaoPessoa.Ativo ? string.Empty : NomesPessoa.Situacao(Situacao);

    public string Detalhe => string.Join("  ·  ",
        new[] { CodigoFormatado, DocumentoFormatado, Local, PapeisTexto, SituacaoTexto,
                MunicipioACorrigir ? "Município a corrigir" : string.Empty }.Where(s => s.Length > 0));
}

/// <summary>Nomes em português dos enums do cadastro, para telas e mensagens.</summary>
public static class NomesPessoa
{
    public static string Papel(TipoPapel papel) => papel switch
    {
        TipoPapel.Cliente => "Cliente",
        TipoPapel.Fornecedor => "Fornecedor",
        TipoPapel.EmpresaDoGrupo => "Empresa do grupo",
        TipoPapel.Vendedor => "Vendedor",
        TipoPapel.Funcionario => "Funcionário",
        TipoPapel.Transportadora => "Transportadora",
        TipoPapel.Representante => "Representante",
        TipoPapel.PrestadorServico => "Prestador de serviço",
        _ => papel.ToString()
    };

    public static string Situacao(SituacaoPessoa situacao) => situacao switch
    {
        SituacaoPessoa.Ativo => "Ativo",
        SituacaoPessoa.EmAnalise => "Em análise",
        SituacaoPessoa.Inativo => "Inativo",
        SituacaoPessoa.Arquivado => "Arquivado",
        _ => situacao.ToString()
    };

    public static string Natureza(NaturezaPessoa natureza) => natureza switch
    {
        NaturezaPessoa.Fisica => "Pessoa física",
        NaturezaPessoa.Juridica => "Pessoa jurídica",
        NaturezaPessoa.Estrangeiro => "Estrangeiro",
        _ => natureza.ToString()
    };

    public static string Sexo(SexoRegistro sexo) => sexo switch
    {
        SexoRegistro.Feminino => "Feminino",
        SexoRegistro.Masculino => "Masculino",
        _ => "Não informado"
    };

    public static string Genero(IdentidadeGenero genero) => genero switch
    {
        IdentidadeGenero.Mulher => "Mulher",
        IdentidadeGenero.Homem => "Homem",
        IdentidadeGenero.NaoBinario => "Não binário",
        IdentidadeGenero.Outra => "Outra",
        IdentidadeGenero.PrefereNaoInformar => "Prefere não informar",
        _ => "Não informado"
    };

    public static string CorRaca(CorRaca cor) => cor switch
    {
        Domain.Enums.CorRaca.Branca => "Branca",
        Domain.Enums.CorRaca.Preta => "Preta",
        Domain.Enums.CorRaca.Parda => "Parda",
        Domain.Enums.CorRaca.Amarela => "Amarela",
        Domain.Enums.CorRaca.Indigena => "Indígena",
        _ => "Não informado"
    };

    public static string EstadoCivil(EstadoCivil estado) => estado switch
    {
        Domain.Enums.EstadoCivil.Solteiro => "Solteiro(a)",
        Domain.Enums.EstadoCivil.Casado => "Casado(a)",
        Domain.Enums.EstadoCivil.Divorciado => "Divorciado(a)",
        Domain.Enums.EstadoCivil.Separado => "Separado(a) judicialmente",
        Domain.Enums.EstadoCivil.Viuvo => "Viúvo(a)",
        Domain.Enums.EstadoCivil.UniaoEstavel => "União estável",
        _ => "Não informado"
    };

    public static string Escolaridade(Escolaridade escolaridade) => escolaridade switch
    {
        Domain.Enums.Escolaridade.Analfabeto => "Analfabeto",
        Domain.Enums.Escolaridade.FundamentalAte5AnoIncompleto => "Fundamental: até o 5º ano incompleto",
        Domain.Enums.Escolaridade.Fundamental5AnoCompleto => "Fundamental: 5º ano completo",
        Domain.Enums.Escolaridade.Fundamental6a9AnoIncompleto => "Fundamental: 6º ao 9º ano incompleto",
        Domain.Enums.Escolaridade.FundamentalCompleto => "Fundamental completo",
        Domain.Enums.Escolaridade.MedioIncompleto => "Médio incompleto",
        Domain.Enums.Escolaridade.MedioCompleto => "Médio completo",
        Domain.Enums.Escolaridade.SuperiorIncompleto => "Superior incompleto",
        Domain.Enums.Escolaridade.SuperiorCompleto => "Superior completo",
        Domain.Enums.Escolaridade.PosGraduacao => "Pós-graduação",
        Domain.Enums.Escolaridade.Mestrado => "Mestrado",
        Domain.Enums.Escolaridade.Doutorado => "Doutorado",
        _ => "Não informado"
    };

    public static string Canal(CanalComunicacao canal) => canal switch
    {
        CanalComunicacao.Email => "E-mail",
        CanalComunicacao.WhatsApp => "WhatsApp",
        CanalComunicacao.Sms => "SMS",
        CanalComunicacao.Telefone => "Ligações",
        CanalComunicacao.Correspondencia => "Correspondência",
        _ => canal.ToString()
    };
}

/// <summary>Pergunta da ficha: este CPF (ou a raiz deste CNPJ) já está em outro cadastro?</summary>
public sealed class DocumentoEmUsoRequisicao
{
    public NaturezaPessoa Natureza { get; set; }

    /// <summary>CPF ou CNPJ completo, com ou sem máscara.</summary>
    public string Documento { get; set; } = string.Empty;

    /// <summary>O próprio cadastro (não conta como duplicado).</summary>
    public Guid IgnorarId { get; set; }
}

/// <summary>Resposta: EmUso falso quando não há outro cadastro (ou o documento não é válido).</summary>
public sealed class DocumentoEmUsoResposta
{
    public bool EmUso { get; set; }
    public Guid? Id { get; set; }
    public int Codigo { get; set; }
    public string? Nome { get; set; }
}
