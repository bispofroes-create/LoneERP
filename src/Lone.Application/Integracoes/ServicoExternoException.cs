namespace Lone.Application.Integracoes;

/// <summary>Serviço externo (CNPJ, CEP...) fora do ar ou com erro. Na API vira HTTP 502.</summary>
public sealed class ServicoExternoException : Exception
{
    public ServicoExternoException(string mensagem, Exception? interna = null) : base(mensagem, interna) { }
}
