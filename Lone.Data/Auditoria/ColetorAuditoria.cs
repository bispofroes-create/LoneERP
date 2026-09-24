using System.Reflection;
using Lone.Core.Auditoria;
using Lone.Core.Entidades;
using Lone.Core.Enums;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.ChangeTracking;

namespace Lone.Data.Auditoria;

/// <summary>
/// Lê as mudanças pendentes no contexto e gera os registros de auditoria (uma linha por campo alterado).
/// Genérico: funciona para qualquer entidade do ERP. Uso: Coletar() antes de gravar; Finalizar() depois,
/// quando os Ids gerados pelo banco já existem.
/// </summary>
internal sealed class ColetorAuditoria
{
    private static readonly HashSet<string> CamposIgnorados =
        [nameof(EntidadeBase.CriadoEm), nameof(EntidadeBase.AtualizadoEm), nameof(AgregadoRaiz.Versao)];

    private const int TamanhoMaximoValor = 500;

    private readonly ChangeTracker _rastreador;
    private readonly string _usuario;
    private readonly OrigemAlteracao _origem;
    private readonly Guid _operacaoId = Guid.NewGuid();
    private readonly DateTime _agora = DateTime.Now;
    private readonly List<(EntityEntry Entrada, RegistroAuditoria Registro)> _pendentes = new();

    public ColetorAuditoria(ChangeTracker rastreador, string usuario, OrigemAlteracao origem)
    {
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

            switch (entrada.State)
            {
                case EntityState.Added:
                    // Partes "owned" (ex.: dados fiscais) nascem junto com o dono; a inclusão do dono já basta.
                    if (!entrada.Metadata.IsOwned())
                        Adicionar(entrada, AcaoAuditoria.Inclusao);
                    break;

                case EntityState.Deleted:
                    Adicionar(entrada, AcaoAuditoria.Exclusao);
                    break;

                case EntityState.Modified:
                    ColetarAlteracoes(entrada);
                    break;
            }
        }
    }

    /// <summary>Completa Ids gerados pelo banco e devolve os registros prontos para gravar.</summary>
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

    private RegistroAuditoria Adicionar(EntityEntry entrada, AcaoAuditoria acao)
    {
        var (raizEntidade, raizId) = Raiz(entrada);
        var registro = new RegistroAuditoria
        {
            DataHora = _agora,
            Usuario = _usuario,
            OperacaoId = _operacaoId,
            Origem = _origem,
            Entidade = entrada.Metadata.ClrType.Name,
            Acao = acao,
            RegistroId = Chave(entrada),
            RaizEntidade = raizEntidade,
            RaizId = raizId
        };
        _pendentes.Add((entrada, registro));
        return registro;
    }

    private static string Chave(EntityEntry entrada)
    {
        var chave = entrada.Metadata.FindPrimaryKey();
        if (chave is null) return string.Empty;
        return string.Join(",", chave.Properties.Select(p => entrada.Property(p.Name).CurrentValue));
    }

    /// <summary>
    /// Agregado a que o registro pertence: ele mesmo (raiz), o informado por IParteDeAgregado,
    /// ou o dono, no caso de tipos "owned". Id 0 (ainda não gravado) vira nulo e é refeito em Finalizar().
    /// </summary>
    private static (string Entidade, int? Id) Raiz(EntityEntry entrada)
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

        return (entidade, id is int n && n > 0 ? n : null);
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
    private static string Mascarar(string texto) =>
        texto.Length <= 3 ? new string('•', texto.Length) : new string('•', texto.Length - 3) + texto[^3..];
}
