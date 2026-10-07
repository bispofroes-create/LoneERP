using System.ComponentModel;
using System.Reflection;
using Lone.Domain.Auditoria;
using Lone.Domain.Entidades;
using Lone.Domain.Enums;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.ChangeTracking;

namespace Lone.Infrastructure.Persistencia.Auditoria;

/// <summary>
/// Lê as mudanças pendentes no contexto e gera os registros de auditoria (uma linha por campo alterado).
/// Genérico: funciona para qualquer entidade do ERP. Uso: Coletar() antes de gravar; Finalizar() depois,
/// quando os Ids gerados pelo banco já existem.
/// Ações distintas (P0, D6): inclusão, alteração, inativação, reativação e exclusão física. Inativar não é excluir: a
/// mudança de <c>Ativo</c> vira uma ação própria. No cadastro de pessoas (<see cref="EntidadePessoaFilha"/>), a inclusão e
/// a exclusão física guardam a foto do conteúdo (D3-a).
/// </summary>
internal sealed class ColetorAuditoria
{
    /// <summary>
    /// Campos técnicos que não viram linha no histórico. P1-8: o número comparável e a chave de unicidade do documento
    /// são cópias do número — a mudança do número já é registrada (mascarada); registrá-las revelaria o número inteiro.
    /// </summary>
    private static readonly HashSet<string> CamposIgnorados =
        [nameof(EntidadeBase.CriadoEm), nameof(EntidadeBase.AtualizadoEm), nameof(AgregadoRaiz.Versao),
         nameof(PessoaDocumento.NumeroNormalizado), nameof(PessoaDocumento.ChaveUnicidade)];

    private const int TamanhoMaximoValor = 500;

    private readonly ChangeTracker _rastreador;
    private readonly string _usuario;
    private readonly OrigemAlteracao _origem;
    private readonly string? _motivo;
    private readonly Guid _operacaoId = Guid.NewGuid();
    private readonly DateTime _agora = DateTime.UtcNow;
    private readonly List<(EntityEntry Entrada, RegistroAuditoria Registro)> _pendentes = new();

    public ColetorAuditoria(ChangeTracker rastreador, string usuario, OrigemAlteracao origem, string? motivo = null)
    {
        _motivo = string.IsNullOrWhiteSpace(motivo) ? null : Cortar(motivo.Trim(), RegistroAuditoria.TamanhoMaximoMotivo);
        _rastreador = rastreador;
        _usuario = usuario;
        _origem = origem;
    }

    public bool TemRegistros => _pendentes.Count > 0;

    public void Coletar()
    {
        foreach (var entrada in _rastreador.Entries().ToList())
        {
            if (entrada.Entity is RegistroAuditoria || entrada.Metadata.ClrType.IsDefined(typeof(NaoAuditarAttribute), inherit: false))
                continue;

            if (entrada.Entity is IValorAuditavel valor)
            {
                ColetarValor(entrada, valor);
                continue;
            }

            switch (entrada.State)
            {
                case EntityState.Added:
                    // Partes "owned" (ex.: dados fiscais) nascem junto com o dono; a inclusão do dono já basta.
                    if (!entrada.Metadata.IsOwned())
                    {
                        Adicionar(entrada, AcaoAuditoria.Inclusao);
                        if (entrada.Entity is EntidadePessoaFilha) Fotografar(entrada, AcaoAuditoria.Inclusao);
                    }
                    break;

                case EntityState.Deleted:
                    Adicionar(entrada, AcaoAuditoria.Exclusao);
                    if (entrada.Entity is EntidadePessoaFilha) Fotografar(entrada, AcaoAuditoria.Exclusao);
                    break;

                case EntityState.Modified:
                    ColetarAlteracoes(entrada);
                    break;
            }
        }

        // Eventos de negócio por último: no histórico aparecem ao lado das alterações que os causaram.
        foreach (var entrada in _rastreador.Entries<AgregadoRaiz>().ToList())
            foreach (var descricao in entrada.Entity.RetirarEventos())
            {
                var registro = Adicionar(entrada, AcaoAuditoria.Evento);
                registro.Descricao = Cortar(descricao);
            }
    }

    /// <summary>
    /// Valor lógico em várias colunas (ex.: campo personalizado): uma linha só, com o nome do campo e o valor
    /// legível antes/depois. Incluir e remover viram "vazio → valor" e "valor → vazio".
    /// </summary>
    private void ColetarValor(EntityEntry entrada, IValorAuditavel valor)
    {
        if (entrada.State is not (EntityState.Added or EntityState.Modified or EntityState.Deleted))
            return;

        var anterior = entrada.State == EntityState.Added ? null : valor.DescreverValor(c => entrada.Property(c).OriginalValue);
        var atual = entrada.State == EntityState.Deleted ? null : valor.DescreverValor(c => entrada.Property(c).CurrentValue);
        if (anterior == atual)
            return;

        var registro = Adicionar(entrada, AcaoAuditoria.Alteracao);
        registro.Campo = Cortar(valor.CampoAuditado, 60);
        registro.ValorAnterior = Cortar(anterior);
        registro.ValorNovo = Cortar(atual);
    }

    private static string? Cortar(string? texto, int maximo = TamanhoMaximoValor) =>
        texto is { Length: var n } && n > maximo ? texto[..maximo] : texto;

    /// <summary>Relê as chaves depois da gravação e devolve os registros prontos para gravar.</summary>
    public IEnumerable<RegistroAuditoria> Finalizar()
    {
        foreach (var (entrada, registro) in _pendentes)
        {
            registro.RegistroId = Chave(entrada);
            (registro.RaizEntidade, registro.RaizId) = Raiz(entrada);
            yield return registro;
        }
    }

    private void ColetarAlteracoes(EntityEntry entrada)
    {
        foreach (var propriedade in entrada.Properties)
        {
            var nome = propriedade.Metadata.Name;
            if (!propriedade.IsModified || CamposIgnorados.Contains(nome))
                continue;

            var anterior = propriedade.OriginalValue;
            var atual = propriedade.CurrentValue;
            if (Equals(anterior, atual))
                continue;

            // Ativo (bool) mudou: inativação ou reativação, ação própria (D6). Só a mudança de Ativo decide a ação; o
            // resumo do registro é apresentação e nunca impede o registro.
            if (nome == CampoAtivo && anterior is bool estavaAtivo && atual is bool ficouAtivo)
            {
                var mudanca = Adicionar(entrada, ficouAtivo ? AcaoAuditoria.Reativacao : AcaoAuditoria.Inativacao);
                mudanca.Campo = nome;
                mudanca.ValorAnterior = Texto(estavaAtivo, sensivel: false);
                mudanca.ValorNovo = Texto(ficouAtivo, sensivel: false);
                mudanca.Descricao = Resumo(entrada.Entity);
                continue;
            }

            var info = entrada.Metadata.ClrType.GetProperty(nome);
            var registro = Adicionar(entrada, AcaoAuditoria.Alteracao);
            registro.Campo = nome;

            // Campos como senha: registra que mudou, nunca o valor.
            if (info?.IsDefined(typeof(NaoAuditarValorAttribute)) == true)
                continue;

            var sensivel = info?.IsDefined(typeof(DadoSensivelAttribute)) == true;
            registro.ValorAnterior = Texto(anterior, sensivel);
            registro.ValorNovo = Texto(atual, sensivel);
        }
    }

    private const string CampoAtivo = "Ativo";

    /// <summary>Resumo do registro (ex.: "Maria Souza (Compradora)"); qualquer falha vira "sem resumo".</summary>
    private static string? Resumo(object entidade)
    {
        try
        {
            return entidade is IResumoAuditoria r && !string.IsNullOrWhiteSpace(r.ResumoAuditoria) ? Cortar(r.ResumoAuditoria.Trim()) : null;
        }
        catch (Exception)
        {
            return null; // o resumo é só apresentação: a ação continua registrada
        }
    }

    /// <summary>
    /// Foto do conteúdo na inclusão (valores novos) ou na exclusão física (valores anteriores), uma linha por campo (D3-a):
    /// só propriedades com [DisplayName] (o nome que o usuário vê), sem os campos técnicos, com valor significativo
    /// (não nulo, texto não vazio, bool verdadeiro; números sempre, inclusive zero). [NaoAuditarValor] não entra;
    /// [DadoSensivel] entra mascarado.
    /// </summary>
    private void Fotografar(EntityEntry entrada, AcaoAuditoria acao)
    {
        var tipo = entrada.Metadata.ClrType;
        foreach (var propriedade in entrada.Properties)
        {
            var nome = propriedade.Metadata.Name;
            if (CamposIgnorados.Contains(nome) || propriedade.Metadata.IsShadowProperty()) continue;
            var info = tipo.GetProperty(nome);
            if (info is null || !info.IsDefined(typeof(DisplayNameAttribute), inherit: true)) continue;
            if (info.IsDefined(typeof(NaoAuditarValorAttribute))) continue;

            var valor = acao == AcaoAuditoria.Exclusao ? propriedade.OriginalValue : propriedade.CurrentValue;
            if (!Significativo(valor)) continue;

            var texto = Texto(valor, info.IsDefined(typeof(DadoSensivelAttribute)));
            var registro = Adicionar(entrada, acao);
            registro.Campo = nome;
            if (acao == AcaoAuditoria.Exclusao) registro.ValorAnterior = texto;
            else registro.ValorNovo = texto;
        }
    }

    private static bool Significativo(object? valor) => valor switch
    {
        null => false,
        string s => !string.IsNullOrWhiteSpace(s),
        bool b => b,
        Guid g => g != Guid.Empty,
        _ => true
    };

    private RegistroAuditoria Adicionar(EntityEntry entrada, AcaoAuditoria acao)
    {
        var (raizEntidade, raizId) = Raiz(entrada);
        var registro = new RegistroAuditoria
        {
            DataHora = _agora,
            Usuario = _usuario,
            OperacaoId = _operacaoId,
            Origem = _origem,
            Motivo = _motivo,
            Entidade = entrada.Metadata.ClrType.Name,
            Acao = acao,
            RegistroId = Chave(entrada),
            RaizEntidade = raizEntidade,
            RaizId = raizId
        };
        _pendentes.Add((entrada, registro));
        return registro;
    }

    /// <summary>
    /// Chave do registro como texto. Em partes de agregado com chave composta (ex.: permissão do perfil =
    /// PerfilId + Codigo), o Id da raiz já vai em RaizId e não se repete aqui, o que mantém a chave curta.
    /// </summary>
    private static string Chave(EntityEntry entrada)
    {
        var chave = entrada.Metadata.FindPrimaryKey();
        if (chave is null) return string.Empty;

        var valores = chave.Properties.Select(p => entrada.Property(p.Name).CurrentValue).ToList();
        if (valores.Count > 1 && entrada.Entity is IParteDeAgregado parte)
            valores.RemoveAll(v => v is Guid id && id == parte.RaizId);

        return string.Join(",", valores);
    }

    /// <summary>
    /// Agregado a que o registro pertence: ele mesmo (raiz), o informado por IParteDeAgregado,
    /// ou o dono, no caso de tipos "owned". Id vazio vira nulo e é refeito em Finalizar().
    /// </summary>
    private static (string Entidade, Guid? Id) Raiz(EntityEntry entrada)
    {
        string entidade;
        object? id;

        if (entrada.Entity is IParteDeAgregado parte)
        {
            entidade = parte.RaizEntidade;
            id = parte.RaizId;
        }
        else if (entrada.Metadata.FindOwnership() is { } dono)
        {
            entidade = dono.PrincipalEntityType.ClrType.Name;
            id = entrada.Property(dono.Properties[0].Name).CurrentValue;
        }
        else
        {
            entidade = entrada.Metadata.ClrType.Name;
            var chave = entrada.Metadata.FindPrimaryKey();
            id = chave is { Properties.Count: 1 } ? entrada.Property(chave.Properties[0].Name).CurrentValue : null;
        }

        return (entidade, id is Guid g && g != Guid.Empty ? g : null);
    }

    private static string? Texto(object? valor, bool sensivel)
    {
        var texto = valor switch
        {
            null => null,
            DateTime d => d.ToString("dd/MM/yyyy HH:mm"),
            DateOnly d => d.ToString("dd/MM/yyyy"),
            bool b => b ? "Sim" : "Não",
            decimal m => m.ToString("N2"),
            _ => valor.ToString()
        };

        if (texto is not null && sensivel)
            texto = Mascarar(texto);

        return texto is { Length: > TamanhoMaximoValor } ? texto[..TamanhoMaximoValor] : texto;
    }

    /// <summary>Mantém só os 3 últimos caracteres visíveis (LGPD): "52998224725" → "••••••••725".</summary>
    internal static string Mascarar(string texto) =>
        texto.Length <= 3 ? new string('•', texto.Length) : new string('•', texto.Length - 3) + texto[^3..];
}
