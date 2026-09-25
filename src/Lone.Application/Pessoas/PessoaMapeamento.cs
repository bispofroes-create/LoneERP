using Lone.Contracts.CamposPersonalizados;
using Lone.Contracts.Pessoas;
using Lone.Domain.Comum;
using Lone.Domain.Entidades;

namespace Lone.Application.Pessoas;

/// <summary>
/// Conversão entre a entidade Pessoa e o PessoaDto, campo a campo (explícita: um campo novo que não for
/// mapeado aparece na revisão, em vez de ser copiado ou perdido em silêncio).
/// </summary>
public static class PessoaMapeamento
{
    public static PessoaDto ParaDto(Pessoa p) => new()
    {
        Id = p.Id,
        Versao = p.Versao,
        Codigo = p.Codigo,
        Natureza = p.Natureza,
        Situacao = p.Situacao,
        SituacaoMotivo = p.SituacaoMotivo,
        SituacaoAlteradaEm = p.SituacaoAlteradaEm is { } alterada ? DateTime.SpecifyKind(alterada, DateTimeKind.Utc) : null,
        Nome = p.Nome,
        NomeSocial = p.NomeSocial,
        NomeExibicao = p.NomeExibicao,
        Apelido = p.Apelido,
        DocumentoPrincipal = p.DocumentoPrincipal,
        DataNascimento = p.DataNascimento,
        GrupoEconomicoId = p.GrupoEconomicoId,
        MescladaEmId = p.MescladaEmId,
        Observacoes = p.Observacoes,
        Sexo = p.Sexo,
        IdentidadeGenero = p.IdentidadeGenero,
        CorRaca = p.CorRaca,
        EstadoCivil = p.EstadoCivil,
        Escolaridade = p.Escolaridade,
        Nacionalidade = p.Nacionalidade,
        NaturalidadeMunicipioId = p.NaturalidadeMunicipioId,
        NomeMae = p.NomeMae,
        NomePai = p.NomePai,
        ProfissaoId = p.ProfissaoId,
        DataAbertura = p.DataAbertura,
        Porte = p.Porte,
        CapitalSocial = p.CapitalSocial,
        Socios = p.Socios.OrderBy(s => s.Nome).Select(ParaDto).ToList(),
        OrigemCadastro = p.OrigemCadastro,
        PrimeiroContatoEm = p.PrimeiroContatoEm,
        Consentimentos = p.Consentimentos.OrderBy(c => c.Canal).Select(ParaDto).ToList(),
        EtiquetaIds = p.Etiquetas.Select(e => e.EtiquetaId).ToList(),
        ValoresPersonalizados = p.ValoresPersonalizados.Select(ParaDto).ToList(),
        Estabelecimentos = p.Estabelecimentos.OrderByDescending(e => e.Principal).ThenBy(e => e.Cnpj).Select(ParaDto).ToList(),
        Enderecos = p.Enderecos.OrderBy(e => e.Ordem).Select(ParaDto).ToList(),
        MeiosContato = p.MeiosContato.Select(ParaDto).ToList(),
        Contatos = p.Contatos.OrderByDescending(c => c.Principal).ThenBy(c => c.Nome).Select(ParaDto).ToList(),
        Documentos = p.Documentos.Select(x => ParaDto(x, p.ValoresDocumentos)).ToList(),
        Vinculos = p.Vinculos.OrderByDescending(v => v.AdmissaoEm).Select(v => ParaDto(v, p.Lotacoes)).ToList(),
        Papeis = p.Papeis.OrderBy(x => x.InicioEm).Select(ParaDto).ToList(),
        ContasCliente = p.ContasCliente.Select(ParaDto).ToList(),
        ExcecoesComerciais = p.ExcecoesComerciais.OrderByDescending(e => e.InicioEm).Select(ParaDto).ToList(),
        Carteira = p.Carteira.OrderByDescending(c => c.InicioEm).Select(ParaDto).ToList(),
        ContasFornecedor = p.ContasFornecedor.Select(ParaDto).ToList(),
        Bloqueios = p.Bloqueios.OrderByDescending(b => b.InicioEm).Select(ParaDto).ToList()
    };

    /// <summary>
    /// Entidade a gravar. Ids vazios recebem um Id novo aqui, para que referências internas
    /// (ex.: endereço fiscal de um estabelecimento) já apontem para Ids definitivos. Bloqueios não entram.
    /// </summary>
    public static Pessoa ParaEntidade(PessoaDto d)
    {
        var pessoaId = IdOuNovo(d.Id);
        var pessoa = new Pessoa
        {
            Id = pessoaId,
            Versao = d.Versao,
            Codigo = d.Codigo,
            Natureza = d.Natureza,
            Situacao = d.Situacao,
            Nome = d.Nome ?? string.Empty,
            NomeSocial = d.NomeSocial,
            NomeExibicao = d.NomeExibicao,
            Apelido = d.Apelido,
            DocumentoPrincipal = d.DocumentoPrincipal,
            DataNascimento = d.DataNascimento,
            GrupoEconomicoId = d.GrupoEconomicoId,
            MescladaEmId = d.MescladaEmId,
            Observacoes = d.Observacoes,
            Sexo = d.Sexo,
            IdentidadeGenero = d.IdentidadeGenero,
            CorRaca = d.CorRaca,
            EstadoCivil = d.EstadoCivil,
            Escolaridade = d.Escolaridade,
            Nacionalidade = d.Nacionalidade,
            NaturalidadeMunicipioId = d.NaturalidadeMunicipioId,
            NomeMae = d.NomeMae,
            NomePai = d.NomePai,
            ProfissaoId = d.ProfissaoId,
            DataAbertura = d.DataAbertura,
            Porte = d.Porte,
            CapitalSocial = d.CapitalSocial,
            Socios = d.Socios.Select(s => ParaEntidade(s, pessoaId)).ToList(),
            OrigemCadastro = d.OrigemCadastro,
            PrimeiroContatoEm = d.PrimeiroContatoEm,
            Consentimentos = d.Consentimentos.Select(c => ParaEntidade(c, pessoaId)).ToList(),
            Etiquetas = d.EtiquetaIds.Select(id => new PessoaEtiqueta { Id = IdSequencial.Novo(), PessoaId = pessoaId, EtiquetaId = id }).ToList(),
            ValoresPersonalizados = d.ValoresPersonalizados.Select(v => ParaEntidade(v, pessoaId)).ToList(),
            Estabelecimentos = d.Estabelecimentos.Select(e => ParaEntidade(e, pessoaId)).ToList(),
            Enderecos = d.Enderecos.Select(e => ParaEntidade(e, pessoaId)).ToList(),
            MeiosContato = d.MeiosContato.Select(m => ParaEntidade(m, pessoaId)).ToList(),
            Contatos = d.Contatos.Select(c => ParaEntidade(c, pessoaId)).ToList(),
            Documentos = d.Documentos.Select(x => ParaEntidade(x, pessoaId)).ToList(),
            Papeis = d.Papeis.Select(x => ParaEntidade(x, pessoaId)).ToList(),
            ContasCliente = d.ContasCliente.Select(c => ParaEntidade(c, pessoaId)).ToList(),
            ExcecoesComerciais = d.ExcecoesComerciais.Select(e => ParaEntidade(e, pessoaId)).ToList(),
            Carteira = d.Carteira.Select(c => ParaEntidade(c, pessoaId)).ToList(),
            ContasFornecedor = d.ContasFornecedor.Select(f => ParaEntidade(f, pessoaId)).ToList()
        };

        // Colaborador: vínculos e as lotações de cada um (Id do vínculo definido aqui, se veio vazio).
        foreach (var v in d.Vinculos)
        {
            var vinculo = ParaEntidade(v, pessoaId);
            pessoa.Vinculos.Add(vinculo);
            pessoa.Lotacoes.AddRange(v.Lotacoes.Select(l => ParaEntidade(l, pessoaId, vinculo.Id)));
        }

        // Valores dos campos dos documentos: ligados ao Id já definitivo de cada documento (mesma ordem da lista).
        for (var i = 0; i < d.Documentos.Count; i++)
            foreach (var v in d.Documentos[i].ValoresPersonalizados)
                pessoa.ValoresDocumentos.Add(ParaValorDocumento(v, pessoaId, pessoa.Documentos[i].Id));
        return pessoa;
    }

    private static Guid IdOuNovo(Guid id) => id == Guid.Empty ? IdSequencial.Novo() : id;

    // ---------------------------------------------------------------- Estabelecimento

    private static EstabelecimentoDto ParaDto(Estabelecimento e) => new()
    {
        Id = e.Id,
        Cnpj = e.Cnpj,
        Principal = e.Principal,
        NomeFantasia = e.NomeFantasia,
        Ativo = e.Ativo,
        SituacaoReceita = e.SituacaoReceita,
        ConsultadoReceitaEm = e.ConsultadoReceitaEm,
        IndicadorIE = e.IndicadorIE,
        InscricaoEstadual = e.InscricaoEstadual,
        InscricaoMunicipal = e.InscricaoMunicipal,
        InscricaoSuframa = e.InscricaoSuframa,
        RegimeTributario = e.RegimeTributario,
        ProdutorRural = e.ProdutorRural,
        CnaePrincipal = e.CnaePrincipal,
        NaturezaJuridica = e.NaturezaJuridica,
        CnaesSecundarios = e.CnaesSecundarios,
        EnderecoFiscalId = e.EnderecoFiscalId
    };

    private static Estabelecimento ParaEntidade(EstabelecimentoDto e, Guid pessoaId) => new()
    {
        Id = IdOuNovo(e.Id),
        PessoaId = pessoaId,
        Cnpj = e.Cnpj,
        Principal = e.Principal,
        NomeFantasia = e.NomeFantasia,
        Ativo = e.Ativo,
        SituacaoReceita = e.SituacaoReceita,
        ConsultadoReceitaEm = e.ConsultadoReceitaEm,
        IndicadorIE = e.IndicadorIE,
        InscricaoEstadual = e.InscricaoEstadual,
        InscricaoMunicipal = e.InscricaoMunicipal,
        InscricaoSuframa = e.InscricaoSuframa,
        RegimeTributario = e.RegimeTributario,
        ProdutorRural = e.ProdutorRural,
        CnaePrincipal = e.CnaePrincipal,
        NaturezaJuridica = e.NaturezaJuridica,
        CnaesSecundarios = e.CnaesSecundarios,
        EnderecoFiscalId = e.EnderecoFiscalId
    };

    // ---------------------------------------------------------------- Sócios e consentimentos

    private static SocioDto ParaDto(PessoaSocio s) => new()
    {
        Id = s.Id,
        Nome = s.Nome,
        Qualificacao = s.Qualificacao,
        Documento = s.Documento,
        EntradaEm = s.EntradaEm
    };

    private static PessoaSocio ParaEntidade(SocioDto s, Guid pessoaId) => new()
    {
        Id = IdOuNovo(s.Id),
        PessoaId = pessoaId,
        Nome = s.Nome ?? string.Empty,
        Qualificacao = s.Qualificacao,
        Documento = s.Documento,
        EntradaEm = s.EntradaEm
    };

    private static ConsentimentoDto ParaDto(PessoaConsentimento c) => new()
    {
        Id = c.Id,
        Canal = c.Canal,
        Concedido = c.Concedido,
        ConcedidoEm = c.ConcedidoEm,
        RevogadoEm = c.RevogadoEm,
        Origem = c.Origem
    };

    private static PessoaConsentimento ParaEntidade(ConsentimentoDto c, Guid pessoaId) => new()
    {
        Id = IdOuNovo(c.Id),
        PessoaId = pessoaId,
        Canal = c.Canal,
        Concedido = c.Concedido,
        ConcedidoEm = c.ConcedidoEm,
        RevogadoEm = c.RevogadoEm,
        Origem = c.Origem
    };

    // ---------------------------------------------------------------- Campos personalizados

    private static ValorPersonalizadoDto ParaDto(ValorPersonalizado v) => new()
    {
        CampoId = v.CampoId,
        Texto = v.ValorTexto,
        Numero = v.ValorNumero,
        Data = v.ValorData,
        Logico = v.ValorLogico,
        OpcaoId = v.OpcaoId
    };

    /// <summary>Id novo: o repositório reaproveita o do valor já gravado para o mesmo campo.</summary>
    private static PessoaValorPersonalizado ParaEntidade(ValorPersonalizadoDto v, Guid pessoaId) => new()
    {
        Id = IdSequencial.Novo(),
        PessoaId = pessoaId,
        CampoId = v.CampoId,
        ValorTexto = v.Texto,
        ValorNumero = v.Numero,
        ValorData = v.Data is { } data ? DateTime.SpecifyKind(data, DateTimeKind.Unspecified) : null,
        ValorLogico = v.Logico,
        OpcaoId = v.OpcaoId
    };

    private static DocumentoValorPersonalizado ParaValorDocumento(ValorPersonalizadoDto v, Guid pessoaId, Guid documentoId) => new()
    {
        Id = IdSequencial.Novo(),
        PessoaId = pessoaId,
        PessoaDocumentoId = documentoId,
        CampoId = v.CampoId,
        ValorTexto = v.Texto,
        ValorNumero = v.Numero,
        ValorData = v.Data is { } data ? DateTime.SpecifyKind(data, DateTimeKind.Unspecified) : null,
        ValorLogico = v.Logico,
        OpcaoId = v.OpcaoId
    };

    // ---------------------------------------------------------------- Comercial

    private static Lone.Contracts.Comercial.ExcecaoComercialDto ParaDto(ExcecaoComercial e) => new()
    {
        Id = e.Id, EmpresaId = e.EmpresaId, InicioEm = e.InicioEm, FimEm = e.FimEm, LimiteCredito = e.LimiteCredito,
        DescontoMaximo = e.DescontoMaximo, DiasMaximoAtraso = e.DiasMaximoAtraso, CondicaoPagamentoId = e.CondicaoPagamentoId,
        ExigeAprovacaoAcimaLimite = e.ExigeAprovacaoAcimaLimite, Motivo = e.Motivo
    };

    private static ExcecaoComercial ParaEntidade(Lone.Contracts.Comercial.ExcecaoComercialDto e, Guid pessoaId) => new()
    {
        Id = IdOuNovo(e.Id), PessoaId = pessoaId, EmpresaId = e.EmpresaId, InicioEm = e.InicioEm, FimEm = e.FimEm,
        LimiteCredito = e.LimiteCredito, DescontoMaximo = e.DescontoMaximo, DiasMaximoAtraso = e.DiasMaximoAtraso,
        CondicaoPagamentoId = e.CondicaoPagamentoId == Guid.Empty ? null : e.CondicaoPagamentoId,
        ExigeAprovacaoAcimaLimite = e.ExigeAprovacaoAcimaLimite, Motivo = e.Motivo
    };

    private static Lone.Contracts.Comercial.CarteiraDto ParaDto(CarteiraCliente c) => new()
    {
        Id = c.Id, EmpresaId = c.EmpresaId, TipoCarteiraId = c.TipoCarteiraId, VendedorId = c.VendedorId, InicioEm = c.InicioEm,
        FimEm = c.FimEm, Exclusivo = c.Exclusivo, Observacao = c.Observacao, Ativo = c.Ativo
    };

    private static CarteiraCliente ParaEntidade(Lone.Contracts.Comercial.CarteiraDto c, Guid pessoaId) => new()
    {
        Id = IdOuNovo(c.Id), PessoaId = pessoaId, EmpresaId = c.EmpresaId, TipoCarteiraId = c.TipoCarteiraId, VendedorId = c.VendedorId,
        InicioEm = c.InicioEm, FimEm = c.FimEm, Exclusivo = c.Exclusivo, Observacao = c.Observacao, Ativo = c.Ativo
    };

    // ---------------------------------------------------------------- Colaborador

    private static Lone.Contracts.Colaboradores.VinculoDto ParaDto(VinculoColaborador v, IEnumerable<LotacaoColaborador> lotacoes) => new()
    {
        Id = v.Id,
        EmpresaId = v.EmpresaId,
        Matricula = v.Matricula,
        Tipo = v.Tipo,
        AdmissaoEm = v.AdmissaoEm,
        DesligamentoEm = v.DesligamentoEm,
        MotivoDesligamento = v.MotivoDesligamento,
        JornadaSemanal = v.JornadaSemanal,
        Observacoes = v.Observacoes,
        Lotacoes = lotacoes.Where(l => l.VinculoId == v.Id).OrderByDescending(l => l.InicioEm).Select(l => new Lone.Contracts.Colaboradores.LotacaoDto
        {
            Id = l.Id,
            InicioEm = l.InicioEm,
            FimEm = l.FimEm,
            CargoId = l.CargoId,
            DepartamentoId = l.DepartamentoId,
            SetorId = l.SetorId,
            CentroCustoId = l.CentroCustoId,
            GestorId = l.GestorId
        }).ToList()
    };

    private static VinculoColaborador ParaEntidade(Lone.Contracts.Colaboradores.VinculoDto v, Guid pessoaId) => new()
    {
        Id = IdOuNovo(v.Id),
        PessoaId = pessoaId,
        EmpresaId = v.EmpresaId,
        Matricula = v.Matricula,
        Tipo = v.Tipo,
        AdmissaoEm = v.AdmissaoEm,
        DesligamentoEm = v.DesligamentoEm,
        MotivoDesligamento = v.MotivoDesligamento,
        JornadaSemanal = v.JornadaSemanal,
        Observacoes = v.Observacoes
    };

    private static LotacaoColaborador ParaEntidade(Lone.Contracts.Colaboradores.LotacaoDto l, Guid pessoaId, Guid vinculoId) => new()
    {
        Id = IdOuNovo(l.Id),
        PessoaId = pessoaId,
        VinculoId = vinculoId,
        InicioEm = l.InicioEm,
        FimEm = l.FimEm,
        CargoId = l.CargoId,
        DepartamentoId = l.DepartamentoId,
        SetorId = l.SetorId,
        CentroCustoId = l.CentroCustoId,
        GestorId = l.GestorId
    };

    // ---------------------------------------------------------------- Endereço

    private static EnderecoDto ParaDto(PessoaEndereco e) => new()
    {
        Id = e.Id,
        Descricao = e.Descricao,
        TipoEnderecoId = e.TipoEnderecoId,
        Observacoes = e.Observacoes,
        Ativo = e.Ativo,
        Finalidades = e.Finalidades,
        Ordem = e.Ordem,
        Cep = e.Cep,
        Logradouro = e.Logradouro,
        Numero = e.Numero,
        Complemento = e.Complemento,
        Bairro = e.Bairro,
        MunicipioId = e.MunicipioId,
        Cidade = e.Cidade,
        Uf = e.Uf,
        CodigoMunicipioIbge = e.CodigoMunicipioIbge,
        CodigoPais = e.CodigoPais,
        Pais = e.Pais
    };

    private static PessoaEndereco ParaEntidade(EnderecoDto e, Guid pessoaId) => new()
    {
        Id = IdOuNovo(e.Id),
        PessoaId = pessoaId,
        Descricao = e.Descricao,
        TipoEnderecoId = e.TipoEnderecoId,
        Observacoes = e.Observacoes,
        Ativo = e.Ativo,
        Finalidades = e.Finalidades,
        Ordem = e.Ordem,
        Cep = e.Cep,
        Logradouro = e.Logradouro ?? string.Empty,
        Numero = e.Numero,
        Complemento = e.Complemento,
        Bairro = e.Bairro,
        MunicipioId = e.MunicipioId,
        Cidade = e.Cidade ?? string.Empty,
        Uf = e.Uf,
        CodigoMunicipioIbge = e.CodigoMunicipioIbge,
        CodigoPais = string.IsNullOrWhiteSpace(e.CodigoPais) ? PessoaEndereco.CodigoPaisBrasil : e.CodigoPais,
        Pais = e.Pais ?? string.Empty
    };

    // ---------------------------------------------------------------- Contatos e documentos

    private static MeioContatoDto ParaDto(MeioContato m) => new()
    {
        Id = m.Id,
        Tipo = m.Tipo,
        Valor = m.Valor,
        TipoMeioContatoId = m.TipoMeioContatoId,
        Ramal = m.Ramal,
        WhatsApp = m.WhatsApp,
        Sms = m.Sms,
        Finalidades = m.Finalidades,
        Descricao = m.Descricao,
        Principal = m.Principal,
        PermiteComunicacao = m.PermiteComunicacao,
        Ativo = m.Ativo
    };

    private static MeioContato ParaEntidade(MeioContatoDto m, Guid pessoaId) => new()
    {
        Id = IdOuNovo(m.Id),
        PessoaId = pessoaId,
        Tipo = m.Tipo,
        Valor = m.Valor ?? string.Empty,
        TipoMeioContatoId = m.TipoMeioContatoId,
        Ramal = m.Ramal,
        WhatsApp = m.WhatsApp,
        Sms = m.Sms,
        Finalidades = m.Finalidades,
        Descricao = m.Descricao,
        Principal = m.Principal,
        PermiteComunicacao = m.PermiteComunicacao,
        Ativo = m.Ativo
    };

    private static ContatoDto ParaDto(Contato c) => new()
    {
        Id = c.Id,
        Nome = c.Nome,
        Cargo = c.Cargo,
        Departamento = c.Departamento,
        Telefone = c.Telefone,
        Celular = c.Celular,
        CelularWhatsApp = c.CelularWhatsApp,
        Email = c.Email,
        Observacoes = c.Observacoes,
        Principal = c.Principal,
        PessoaVinculadaId = c.PessoaVinculadaId
    };

    private static Contato ParaEntidade(ContatoDto c, Guid pessoaId) => new()
    {
        Id = IdOuNovo(c.Id),
        PessoaId = pessoaId,
        Nome = c.Nome ?? string.Empty,
        Cargo = c.Cargo,
        Departamento = c.Departamento,
        Telefone = c.Telefone,
        Celular = c.Celular,
        CelularWhatsApp = c.CelularWhatsApp,
        Email = c.Email,
        Observacoes = c.Observacoes,
        Principal = c.Principal,
        PessoaVinculadaId = c.PessoaVinculadaId
    };

    private static DocumentoDto ParaDto(PessoaDocumento x, IEnumerable<DocumentoValorPersonalizado> valores) => new()
    {
        ValoresPersonalizados = valores.Where(v => v.PessoaDocumentoId == x.Id).Select(ParaDto).ToList(),
        Id = x.Id,
        TipoDocumentoId = x.TipoDocumentoId,
        Tipo = x.Tipo,
        Ativo = x.Ativo,
        Numero = x.Numero,
        OrgaoEmissor = x.OrgaoEmissor,
        Uf = x.Uf,
        EmitidoEm = x.EmitidoEm,
        ValidoAte = x.ValidoAte,
        Observacoes = x.Observacoes
    };

    private static PessoaDocumento ParaEntidade(DocumentoDto x, Guid pessoaId) => new()
    {
        Id = IdOuNovo(x.Id),
        PessoaId = pessoaId,
        TipoDocumentoId = x.TipoDocumentoId,
        Tipo = x.Tipo,
        Ativo = x.Ativo,
        Numero = x.Numero ?? string.Empty,
        OrgaoEmissor = x.OrgaoEmissor,
        Uf = x.Uf,
        EmitidoEm = x.EmitidoEm,
        ValidoAte = x.ValidoAte,
        Observacoes = x.Observacoes
    };

    // ---------------------------------------------------------------- Papéis e contas

    private static PapelDto ParaDto(PessoaPapel x) => new()
    {
        Id = x.Id,
        PapelId = x.PapelId,
        Papel = x.Papel,
        Ativo = x.Ativo,
        InicioEm = x.InicioEm,
        FimEm = x.FimEm,
        Observacoes = x.Observacoes
    };

    private static PessoaPapel ParaEntidade(PapelDto x, Guid pessoaId) => new()
    {
        Id = IdOuNovo(x.Id),
        PessoaId = pessoaId,
        PapelId = x.PapelId,
        Papel = x.Papel, // conferido e sobrescrito pelo cadastro de papéis ao gravar (RegrasPapel.Aplicar)
        Ativo = x.Ativo,
        InicioEm = x.InicioEm,
        FimEm = x.FimEm,
        Observacoes = x.Observacoes
    };

    private static ContaClienteDto ParaDto(ContaCliente c) => new()
    {
        Id = c.Id,
        EmpresaId = c.EmpresaId,
        LimiteCredito = c.LimiteCredito,
        DiasMaximoAtraso = c.DiasMaximoAtraso,
        DescontoMaximo = c.DescontoMaximo,
        CondicaoPagamento = c.CondicaoPagamento,
        ExigeAprovacaoAcimaLimite = c.ExigeAprovacaoAcimaLimite,
        VendedorPadraoId = c.VendedorPadraoId,
        PerfilComercialId = c.PerfilComercialId,
        CondicaoPagamentoId = c.CondicaoPagamentoId,
        Observacoes = c.Observacoes
    };

    private static ContaCliente ParaEntidade(ContaClienteDto c, Guid pessoaId) => new()
    {
        Id = IdOuNovo(c.Id),
        PessoaId = pessoaId,
        EmpresaId = c.EmpresaId,
        LimiteCredito = c.LimiteCredito,
        DiasMaximoAtraso = c.DiasMaximoAtraso,
        DescontoMaximo = c.DescontoMaximo,
        CondicaoPagamento = c.CondicaoPagamento,
        ExigeAprovacaoAcimaLimite = c.ExigeAprovacaoAcimaLimite,
        VendedorPadraoId = c.VendedorPadraoId,
        PerfilComercialId = c.PerfilComercialId,
        CondicaoPagamentoId = c.CondicaoPagamentoId,
        Observacoes = c.Observacoes
    };

    private static ContaFornecedorDto ParaDto(ContaFornecedor f) => new()
    {
        Id = f.Id,
        EmpresaId = f.EmpresaId,
        CondicaoPagamento = f.CondicaoPagamento,
        PrazoMedioDias = f.PrazoMedioDias,
        LeadTimeDias = f.LeadTimeDias,
        TransportadoraPadraoId = f.TransportadoraPadraoId,
        Avaliacao = f.Avaliacao,
        Observacoes = f.Observacoes
    };

    private static ContaFornecedor ParaEntidade(ContaFornecedorDto f, Guid pessoaId) => new()
    {
        Id = IdOuNovo(f.Id),
        PessoaId = pessoaId,
        EmpresaId = f.EmpresaId,
        CondicaoPagamento = f.CondicaoPagamento,
        PrazoMedioDias = f.PrazoMedioDias,
        LeadTimeDias = f.LeadTimeDias,
        TransportadoraPadraoId = f.TransportadoraPadraoId,
        Avaliacao = f.Avaliacao,
        Observacoes = f.Observacoes
    };

    private static BloqueioDto ParaDto(Bloqueio b) => new()
    {
        Id = b.Id,
        EmpresaId = b.EmpresaId,
        Escopo = b.Escopo,
        Origem = b.Origem,
        Motivo = b.Motivo,
        InicioEm = b.InicioEm,
        InicioPor = b.InicioPor,
        FimEm = b.FimEm,
        FimPor = b.FimPor,
        MotivoLiberacao = b.MotivoLiberacao
    };
}
