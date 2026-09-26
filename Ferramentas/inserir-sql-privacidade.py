"""
Fase 3 (privacidade): coloca migrationBuilder.Sql(SqlMigracaoPrivacidade.ConsentimentosAnteriores) na migração
PrivacidadeConsentimentos, DEPOIS da coluna PessoaConsentimentos.FinalidadeId e dos dados iniciais de
FinalidadesTratamento, e ANTES da FK PessoaConsentimentos → FinalidadesTratamento.

Uso (na raiz do repositório, depois do Add-Migration PrivacidadeConsentimentos):
    python Ferramentas/inserir-sql-privacidade.py
Se algum ponto não for encontrado, nada é gravado. Rodar de novo não duplica.
"""
import glob, os, re

PASTA = os.path.join("src", "Lone.Infrastructure", "Persistencia", "Migracoes")
NOME = "PrivacidadeConsentimentos"
LINHA = "            migrationBuilder.Sql(SqlMigracaoPrivacidade.ConsentimentosAnteriores);\n"


def ler(caminho):
    bruto = open(caminho, "rb").read()
    bom = bruto.startswith(b"\xef\xbb\xbf")
    texto = bruto.decode("utf-8-sig")
    crlf = "\r\n" in texto
    return texto.replace("\r\n", "\n"), bom, crlf


def fim_da_chamada(texto, inicio):
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


def uma(texto, metodo, **args):
    achadas = []
    for m in re.finditer(r"^[ \t]*migrationBuilder\." + metodo + r"(<[^>]*>)?\(", texto, re.M):
        fim = fim_da_chamada(texto, m.start())
        corpo = texto[m.start():fim]
        if all(re.search(rf'\b{k}:\s*"{re.escape(v)}"', corpo) for k, v in args.items()):
            achadas.append((m.start(), fim))
    if len(achadas) != 1:
        raise SystemExit(f"Não encontrei exatamente uma chamada {metodo} {args} (achei {len(achadas)}). Nada foi gravado.")
    return achadas[0]


def main():
    arquivos = [f for f in glob.glob(os.path.join(PASTA, f"*_{NOME}.cs")) if not f.endswith(".Designer.cs")]
    if len(arquivos) != 1:
        raise SystemExit(f"Gere antes a migração: Add-Migration {NOME} (achei {len(arquivos)} arquivo(s)).")
    caminho = arquivos[0]
    texto, bom, crlf = ler(caminho)
    if "SqlMigracaoPrivacidade.ConsentimentosAnteriores" in texto:
        print(f"{os.path.basename(caminho)}: o SQL já está lá.")
        return

    up_ini = texto.index("protected override void Up(")
    up_fim = texto.index("protected override void Down(")
    up = texto[up_ini:up_fim]

    coluna = uma(up, "AddColumn", name="FinalidadeId", table="PessoaConsentimentos")
    dados = uma(up, "InsertData", table="FinalidadesTratamento")
    fk = uma(up, "AddForeignKey", name="FK_PessoaConsentimentos_FinalidadesTratamento_FinalidadeId")
    if coluna[1] > fk[0] or dados[1] > fk[0]:
        raise SystemExit("A FK vem antes da coluna ou dos dados iniciais: ajuste a ordem à mão. Nada foi gravado.")

    up = up[:fk[0]] + LINHA + "\n" + up[fk[0]:]
    texto = texto[:up_ini] + up + texto[up_fim:]
    if crlf:
        texto = texto.replace("\n", "\r\n")
    open(caminho, "wb").write((b"\xef\xbb\xbf" if bom else b"") + texto.encode("utf-8"))
    print(f"{os.path.basename(caminho)}: SQL dos consentimentos anteriores inserido antes da FK.")


if __name__ == "__main__":
    main()
