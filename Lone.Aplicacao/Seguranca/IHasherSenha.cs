namespace Lone.Aplicacao.Seguranca;

public interface IHasherSenha
{
    /// <summary>Gera o texto a gravar (algoritmo, iterações, sal e hash). Nunca devolve a senha.</summary>
    string Gerar(string senha);

    bool Verificar(string senha, string hashGravado);
}
