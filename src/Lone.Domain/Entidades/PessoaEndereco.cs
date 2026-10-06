using System.ComponentModel;
using Lone.Domain.Enderecos.ConferenciaCep;
using Lone.Domain.Enums;

namespace Lone.Domain.Entidades;

/// <summary>
/// Endereço físico da pessoa. Os usos (finalidades) e o principal de cada uso ficam em PessoaEnderecoFinalidades:
/// o mesmo lugar não é cadastrado de novo para outra finalidade, e nada é decidido pela Ordem.
/// </summary>
[DisplayName("Endereço")]
public class PessoaEndereco : EntidadePessoaFilha
{
    public const string CodigoPaisBrasil = "1058";

    /// <summary>Identificação livre (ex.: "Loja centro", "Depósito").</summary>
    [DisplayName("Descrição")]
    public string? Descricao { get; set; }

    /// <summary>Classificação do cadastro de tipos de endereço (Sede, Depósito...); opcional.</summary>
    [DisplayName("Tipo")]
    public Guid? TipoEnderecoId { get; set; }

    [DisplayName("Observações")]
    public string? Observacoes { get; set; }

    /// <summary>Falso = removido na ficha: fica gravado (histórico), não é principal nem endereço fiscal.</summary>
    [DisplayName("Ativo")]
    public bool Ativo { get; set; } = true;

    /// <summary>
    /// Cópia em bits das finalidades ativas deste endereço, gravada pela API a partir de PessoaEnderecoFinalidades
    /// (que é a fonte, com o principal de cada finalidade). Mantida para compatibilidade; nada decide por ela.
    /// </summary>
    [DisplayName("Finalidades")]
    public FinalidadeEndereco Finalidades { get; set; } = FinalidadeEndereco.Nenhuma;

    /// <summary>Ordem de exibição (não decide o principal: ele é explícito em cada finalidade).</summary>
    [DisplayName("Ordem")]
    public int Ordem { get; set; }

    [DisplayName("CEP")]
    public string? Cep { get; set; }

    [DisplayName("Logradouro")]
    public string Logradouro { get; set; } = string.Empty;

    [DisplayName("Número")]
    public string? Numero { get; set; }

    [DisplayName("Complemento")]
    public string? Complemento { get; set; }

    [DisplayName("Bairro")]
    public string? Bairro { get; set; }

    /// <summary>
    /// Município (código IBGE, na tabela Municipios). Obrigatório no Brasil; nulo no exterior.
    /// Cidade, UF e código IBGE abaixo são cópias gravadas a partir dele pela API (o texto exato da época).
    /// </summary>
    [DisplayName("Município")]
    public int? MunicipioId { get; set; }

    /// <summary>Nome do município (cópia do IBGE) ou, no exterior, a cidade digitada.</summary>
    [DisplayName("Cidade")]
    public string Cidade { get; set; } = string.Empty;

    [DisplayName("UF")]
    public string? Uf { get; set; }

    /// <summary>Código IBGE do município (7 dígitos), obrigatório na NF-e. Cópia de MunicipioId.</summary>
    [DisplayName("Código IBGE")]
    public string? CodigoMunicipioIbge { get; set; }

    /// <summary>Código do país na tabela do BACEN (Brasil = 1058).</summary>
    [DisplayName("Código do país")]
    public string CodigoPais { get; set; } = CodigoPaisBrasil;

    [DisplayName("País")]
    public string Pais { get; set; } = "Brasil";

    public bool EhBrasil => CodigoPais == CodigoPaisBrasil;
    /// <summary>
    /// Endereço duplicado que foi consolidado neste outro (fica inativo e aponta para o que ficou). Nulo = não mesclado.
    /// O banco garante que o destino é endereço da MESMA pessoa (FK composta) e que o consolidado fica inativo (CHECK).
    /// Só a operação de consolidação da API preenche; a ficha não altera.
    /// </summary>
    [DisplayName("Consolidado em")]
    public Guid? MescladoEmId { get; set; }

    /// <summary>
    /// Motivo de revisão deixado pela migração do cadastro antigo (só a migração liga; a API só desliga).
    /// </summary>
    [DisplayName("Revisão da migração")]
    public MotivoRevisaoEndereco RevisaoMigracao { get; set; } = MotivoRevisaoEndereco.Nenhum;

    // ---- Conferência do CEP persistida (F3). Só a API grava (EstadoConferenciaCep, no Salvar); a ficha nunca envia. ----

    /// <summary>
    /// Situação da última conferência do CEP que ainda vale para os dados gravados (Conferido, Divergente, NaoEncontrado)
    /// ou NaoConferido. Endereço antigo começa NaoConferido (nada é presumido). Mudou CEP, UF, município, logradouro,
    /// número ("Sem número") ou bairro sem nova conferência: volta a NaoConferido. Não é procedência do CEP (DM3).
    /// </summary>
    public CepSituacao CepSituacao { get; set; } = CepSituacao.NaoConferido;

    /// <summary>Fonte original da conferência (ViaCEP, BrasilAPI...); nunca "cache". Nula quando não conferido.</summary>
    public CepFonte? CepFonte { get; set; }

    /// <summary>Quando a conferência que sustenta a situação aconteceu (UTC). Abrir, ler cache ou salvar sem conferir não muda.</summary>
    public DateTime? CepConferidoEm { get; set; }
}
