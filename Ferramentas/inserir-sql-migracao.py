"""
Coloca os trechos de SqlMigracaoCadastroGeral na migração única final (a mais nova depois de MeiosContatoETipos).

Uso (na raiz do repositório):  python Ferramentas/inserir-sql-migracao.py
Cada trecho entra no ponto exigido pelo comentário da constante. Se algum ponto não for encontrado, nada é gravado.
Rodar de novo não duplica (confere se o trecho já está lá).
"""
import glob, os, re, sys

PASTA = os.path.join("src", "Lone.Infrastructure", "Persistencia", "Migracoes")
BASE = "MeiosContatoETipos"


def ler(caminho):
    bruto = open(caminho, "rb").read()
    bom = bruto.startswith(b"\xef\xbb\xbf")
    texto = bruto.decode("utf-8-sig")
    crlf = "\r\n" in texto
    return texto.replace("\r\n", "\n"), bom, crlf


def fim_da_chamada(texto, inicio):
    """Posição logo depois do ');' que fecha a chamada migrationBuilder.X( que começa em 'inicio'."""
    abre = texto.index("(", inicio)
    nivel, i, em_texto = 0, abre, None
    while i < len(texto):
        c = texto[i]
        if em_texto:
            if c == "\\" and em_texto == '"':
                i += 2
                continue
            if c == em_texto:
                em_texto = None
        elif c in "\"'":
            em_texto = c
        elif c == "(":
            nivel += 1
        elif c == ")":
            nivel -= 1
            if nivel == 0:
                fim = texto.index(";", i) + 1
                return texto.index("\n", fim) + 1
        i += 1
    raise ValueError("chamada sem fim")


def chamadas(texto, metodo, **args):
    """Inícios das linhas 'migrationBuilder.<metodo>(' cujos argumentos nomeados conferem (ex.: name, table)."""
    achadas = []
    for m in re.finditer(r"^[ \t]*migrationBuilder\." + metodo + r"(<[^>]*>)?\(", texto, re.M):
        fim = fim_da_chamada(texto, m.start())
        corpo = texto[m.start():fim]
        if all(re.search(rf'\b{k}:\s*"{re.escape(v)}"', corpo) for k, v in args.items()):
            achadas.append((m.start(), fim))
    return achadas


def uma(texto, metodo, **args):
    r = chamadas(texto, metodo, **args)
    if len(r) != 1:
        raise SystemExit(f"Não encontrei exatamente uma chamada {metodo} {args} (achei {len(r)}). Nada foi gravado.")
    return r[0]


def main():
    arquivos = sorted(f for f in glob.glob(os.path.join(PASTA, "*.cs"))
                      if re.match(r"\d{14}_", os.path.basename(f)) and not f.endswith(".Designer.cs"))
    base = [f for f in arquivos if f.endswith(f"_{BASE}.cs")]
    if not base or arquivos[-1] == base[0]:
        raise SystemExit("Gere antes a migração nova (Add-Migration) depois de " + BASE + ".")
    caminho = arquivos[-1]
    texto, bom, crlf = ler(caminho)

    up_ini = texto.index("protected override void Up(")
    up_fim = texto.index("protected override void Down(")
    up = texto[up_ini:up_fim]

    def pos_depois(*chamadas_):
        return max(fim for _, fim in chamadas_)

    # Migração das finalidades de endereço (endereço × finalidade), de outra classe:
    # - MigrarFinalidades (dados): depois da tabela nova, dos dados iniciais e das colunas novas; antes dos índices únicos;
    # - CriarProtecoes (gatilhos): no fim do Up, com tudo criado e os dados já migrados;
    # - RemoverProtecoes: no começo do Down, antes de o EF desfazer tabelas e colunas.
    if chamadas(up, "CreateTable", name="PessoaEnderecoFinalidades"):
        classe = "SqlMigracaoFinalidadesEndereco"
        dados = pos_depois(uma(up, "CreateTable", name="PessoaEnderecoFinalidades"),
                           uma(up, "InsertData", table="FinalidadesEndereco"),
                           uma(up, "AddColumn", name="RevisarFinalidadesEndereco", table="Pessoas"),
                           uma(up, "AddColumn", name="RevisaoMigracao", table="PessoaEnderecos"))
        for indice in ("IX_PessoaEnderecoFinalidades_PessoaEnderecoId_FinalidadeId", "IX_PessoaEnderecoFinalidades_PessoaId_FinalidadeId"):
            if uma(up, "CreateIndex", name=indice)[0] < dados:
                raise SystemExit(f"O índice {indice} vem antes do ponto dos dados: ajuste a ordem à mão. Nada foi gravado.")
        fim_up = up.rfind("\n", 0, up.rstrip().rfind("}")) + 1   # começo da linha da chave que fecha o método Up
        pontos = {"MigrarFinalidades": dados, "CriarProtecoes": fim_up}
        texto = texto[:up_ini] + inserir(up, pontos, classe) + texto[up_fim:]

        down_ini = texto.index("protected override void Down(")
        abre = texto.index("{", down_ini) + 1
        if f"{classe}.RemoverProtecoes" not in texto[down_ini:]:
            texto = texto[:abre] + f"\n            migrationBuilder.Sql({classe}.RemoverProtecoes);\n" + texto[abre:]
        return escrever(caminho, texto, bom, crlf)

    fk_doc = uma(up, "AddForeignKey", name="FK_PessoaDocumentos_TiposDocumento_TipoDocumentoId")
    pontos = {
        "AtivarEnderecos": pos_depois(uma(up, "AddColumn", name="Ativo", table="PessoaEnderecos")),
        # Antes da FK, mas depois das colunas e do insert dos tipos de sistema.
        "LigarDocumentosAosTipos": fk_doc[0],
        "CamposVisiveis": pos_depois(uma(up, "AddColumn", name="Visivel", table="CamposPersonalizados")),
        "CarteiraDosVendedoresPadrao": pos_depois(uma(up, "CreateTable", name="CarteiraClientes"),
                                                  uma(up, "InsertData", table="TiposCarteira")),
        "HistoricoFiscalECnaes": pos_depois(uma(up, "CreateTable", name="HistoricoFiscal"),
                                            uma(up, "CreateTable", name="EstabelecimentoCnaes"),
                                            uma(up, "AddColumn", name="ProdutorRural", table="Estabelecimentos")),
    }
    # Conferências de ordem: a ligação dos documentos precisa das colunas e dos tipos já criados.
    for pre in (uma(up, "AddColumn", name="TipoDocumentoId", table="PessoaDocumentos"),
                uma(up, "AddColumn", name="Ativo", table="PessoaDocumentos"),
                uma(up, "InsertData", table="TiposDocumento")):
        if pre[1] > pontos["LigarDocumentosAosTipos"]:
            raise SystemExit("A FK dos documentos vem antes das colunas/tipos: ajuste a ordem à mão. Nada foi gravado.")

    return gravar(caminho, texto, up_ini, up_fim, up, pontos, "SqlMigracaoCadastroGeral", bom, crlf)


def inserir(up, pontos, classe):
    """Coloca migrationBuilder.Sql(classe.nome) em cada ponto do Up (de trás para frente; não repete o que já está)."""
    for nome, pos in sorted(pontos.items(), key=lambda x: -x[1]):   # de trás para frente: posições continuam valendo
        linha = f"            migrationBuilder.Sql({classe}.{nome});\n"
        if f"{classe}.{nome}" in up:
            continue
        up = up[:pos] + "\n" + linha + "\n" + up[pos:]
    return up


def escrever(caminho, texto, bom, crlf):
    if crlf:
        texto = texto.replace("\n", "\r\n")
    open(caminho, "wb").write((b"\xef\xbb\xbf" if bom else b"") + texto.encode("utf-8"))
    print(f"{os.path.basename(caminho)}: trechos de SQL conferidos/inseridos.")


def gravar(caminho, texto, up_ini, up_fim, up, pontos, classe, bom, crlf):
    texto = texto[:up_ini] + inserir(up, pontos, classe) + texto[up_fim:]
    escrever(caminho, texto, bom, crlf)


if __name__ == "__main__":
    main()
