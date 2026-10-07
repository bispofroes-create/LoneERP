"""
Gera o catálogo oficial de países da NF-e (cPais) a partir da planilha oficial da NT 2018.003.

Uso (de qualquer pasta; o arquivo é gravado na raiz do repositório onde o script está):
    python Ferramentas/gerar-paises-nfe.py "<caminho da planilha .ods>"

Fluxo: fonte oficial -> este processo controlado -> artefato determinístico versionado
(src/Lone.Domain/Enderecos/PaisesNFeOficiais.cs) -> HasData -> migration. O Lone nunca lê a planilha nem a internet.

Regras (autorização do V2-1):
- só a aba "cPais_2" (versão 1.01); o SHA-256 do arquivo tem de ser o conferido, senão nada é gravado;
- o código vira texto de 4 dígitos (a planilha guarda número e perde o zero à esquerda: 639 -> "0639");
  nunca há conversão de código (249 não vira 2496);
- uma linha por código; as 3 renomeações conhecidas (duas linhas com o mesmo código) viram uma só, com o nome
  novo, e o nome anterior vai para o relatório; qualquer outra repetição de código interrompe a geração;
- sucessor só nas 5 trocas de código explícitas (ALTERADO), conferidas uma a uma; nada é convertido nos dados;
- datas em data da planilha ou em texto dd/mm/aaaa; qualquer outra forma interrompe a geração.
Rodar de novo com a mesma planilha gera o mesmo arquivo, byte a byte.
"""
import hashlib, os, re, sys, zipfile
import xml.etree.ElementTree as ET
from datetime import date

HASH_ESPERADO = "76386b536812e13baf5f09f49c057e78650be387a96c576377c3998f39358a9f"
ABA = "cPais_2"
FONTE = "Tabela de Países da NF-e - NT 2018.003"
VERSAO = "1.01"
DATA_REFERENCIA = date(2026, 10, 7)  # data em que a contagem de vigentes foi conferida

RAIZ = os.path.dirname(os.path.dirname(os.path.abspath(__file__)))  # a raiz do repositório, de onde quer que se rode
SAIDA_CS = os.path.join("src", "Lone.Domain", "Enderecos", "PaisesNFeOficiais.cs")

# Renomeações: código -> (nome anterior, nome atual), exatamente como na planilha.
RENOMEACOES = {
    "1988": ("COVEITE", "KUWAIT"),
    "4499": ("MACEDONIA, ANT.REP.IUGOSLAVA", "MACEDONIA DO NORTE"),
    "7544": ("SUAZILANDIA", "ESSUATINI"),
}
# Trocas de código explícitas: código antigo (ALTERADO, encerrado em 31/05/2018) -> código novo (mesmo nome, aberto).
SUCESSORES = {"0200": "2003", "3599": "0990", "1504": "3212", "1508": "3930", "4885": "4898"}

NS = {
    "table": "urn:oasis:names:tc:opendocument:xmlns:table:1.0",
    "office": "urn:oasis:names:tc:opendocument:xmlns:office:1.0",
    "text": "urn:oasis:names:tc:opendocument:xmlns:text:1.0",
}


def q(prefixo, nome):
    return "{%s}%s" % (NS[prefixo], nome)


def falha(msg):
    print("ERRO: " + msg, file=sys.stderr)
    sys.exit(1)


def texto_no(no):
    """Texto de um elemento ODF, com os espaços codificados (<text:s text:c="n"/>) e tabulações; sem isso
    "REPUBLICA  DA" (dois espaços, como na planilha) viraria "REPUBLICA DA"."""
    partes = [no.text or ""]
    for filho in no:
        if filho.tag == q("text", "s"):
            partes.append(" " * int(filho.get(q("text", "c"), "1")))
        elif filho.tag == q("text", "tab"):
            partes.append("\t")
        elif filho.tag == q("text", "line-break"):
            partes.append("\n")
        else:
            partes.append(texto_no(filho))
        partes.append(filho.tail or "")
    return "".join(partes)


def texto_celula(cel):
    return "\n".join(texto_no(p) for p in cel.findall(q("text", "p")))


def valor_celula(cel):
    tipo = cel.get(q("office", "value-type"))
    if tipo == "float":
        return ("numero", cel.get(q("office", "value")))
    if tipo == "date":
        return ("data", cel.get(q("office", "date-value")))
    t = texto_celula(cel)
    return ("texto", t) if t != "" else ("vazio", None)


def ler_aba(caminho, aba):
    with zipfile.ZipFile(caminho) as z:
        raiz = ET.fromstring(z.read("content.xml"))
    for tabela in raiz.iter(q("table", "table")):
        if tabela.get(q("table", "name")) != aba:
            continue
        linhas = []
        for tr in tabela.iter(q("table", "table-row")):
            celulas = []
            for tc in tr:
                if tc.tag not in (q("table", "table-cell"), q("table", "covered-table-cell")):
                    continue
                rep = int(tc.get(q("table", "number-columns-repeated"), "1"))
                v = valor_celula(tc)
                # Só células vazias repetidas são cortadas (o fim da linha costuma repetir milhares de vazias).
                celulas.extend([v] * (min(rep, 10) if v[0] == "vazio" else rep))
            while celulas and celulas[-1][0] == "vazio":
                celulas.pop()
            rep_l = int(tr.get(q("table", "number-rows-repeated"), "1"))
            if not celulas:
                continue  # linhas vazias (inclusive as repetidas até o fim da aba)
            linhas.extend([celulas] * rep_l)
        return linhas
    falha(f'aba "{aba}" não encontrada')


def data(v, campo, linha):
    tipo, bruto = v
    if tipo == "vazio":
        return None
    if tipo == "data":
        m = re.fullmatch(r"(\d{4})-(\d{2})-(\d{2})(T00:00:00)?", bruto)
        if m:
            return date(int(m[1]), int(m[2]), int(m[3]))
    if tipo == "texto":
        m = re.fullmatch(r"(\d{2})/(\d{2})/(\d{4})", bruto.strip())
        if m:
            return date(int(m[3]), int(m[2]), int(m[1]))
    falha(f"linha {linha}: {campo} em formato inesperado: {v!r}")


def texto(v):
    return None if v[0] == "vazio" else str(v[1])


def cs(s):
    return '"' + s.replace("\\", "\\\\").replace('"', '\\"') + '"'


def cs_data(d):
    return f"new DateOnly({d.year}, {d.month}, {d.day})"


def main():
    if len(sys.argv) != 2:
        falha('uso: python Ferramentas/gerar-paises-nfe.py "<planilha .ods>"')
    caminho = sys.argv[1]
    with open(caminho, "rb") as f:
        hash_atual = hashlib.sha256(f.read()).hexdigest()
    if hash_atual != HASH_ESPERADO:
        falha(f"SHA-256 diferente do conferido.\n  esperado {HASH_ESPERADO}\n  obtido   {hash_atual}")

    linhas = ler_aba(caminho, ABA)
    if len(linhas) < 3:
        falha("aba sem cabeçalho ou sem dados")
    titulo = (texto(linhas[0][0]) if linhas[0] else None) or ""
    cab = [(texto(c) or "").strip() for c in linhas[1]]
    if not titulo.startswith("Tabela de Pa") or cab[:5] != ["cPais", "Nome País", "SITUAÇÃO", "Data Início", "Data Fim"]:
        falha(f"cabeçalho inesperado: {titulo!r} / {cab!r}")

    brutas = []
    for i, cel in enumerate(linhas[2:], start=3):
        cel = cel + [("vazio", None)] * (5 - len(cel))
        if len(cel) > 5:
            falha(f"linha {i}: colunas a mais: {cel!r}")
        tipo, cod = cel[0]
        if tipo != "numero" or not re.fullmatch(r"\d{1,4}(\.0+)?", cod):
            falha(f"linha {i}: cPais inválido: {cel[0]!r}")
        codigo = "%04d" % int(float(cod))
        nome = texto(cel[1])
        if not nome or not nome.strip():
            falha(f"linha {i}: nome vazio")
        brutas.append({
            "linha": i, "codigo": codigo, "nome": nome.strip(), "situacao": (texto(cel[2]) or "").strip() or None,
            "inicio": data(cel[3], "Data Início", i), "fim": data(cel[4], "Data Fim", i),
        })

    por_codigo = {}
    for r in brutas:
        por_codigo.setdefault(r["codigo"], []).append(r)

    paises, renomeados = [], []
    for codigo in sorted(por_codigo):
        rs = por_codigo[codigo]
        if len(rs) == 1:
            p = dict(rs[0])
            p["anterior"] = None
        else:
            if codigo not in RENOMEACOES or len(rs) != 2:
                falha(f"código repetido não previsto: {codigo} ({len(rs)} linhas)")
            antigo, novo = RENOMEACOES[codigo]
            a, n = rs
            if (a["nome"], n["nome"]) != (antigo, novo):
                falha(f"renomeação de {codigo} diferente da conferida: {a['nome']!r} -> {n['nome']!r}")
            if a["situacao"] != n["situacao"] or a["fim"] is not None or n["fim"] is not None:
                falha(f"renomeação de {codigo} com situação ou vigência inesperada")
            p = dict(n)
            p["anterior"] = antigo
            p["inicio"] = min(a["inicio"], n["inicio"])
            renomeados.append((codigo, antigo, novo, a, n))
        if p["inicio"] is None:
            falha(f"{codigo}: sem Data Início")
        if p["fim"] is not None and p["fim"] < p["inicio"]:
            falha(f"{codigo}: Data Fim antes da Data Início")
        p["sucessor"] = SUCESSORES.get(codigo)
        paises.append(p)

    codigos = {p["codigo"] for p in paises}
    for antigo, novo in SUCESSORES.items():
        a = next((p for p in paises if p["codigo"] == antigo), None)
        n = next((p for p in paises if p["codigo"] == novo), None)
        if a is None or n is None:
            falha(f"sucessão {antigo}->{novo}: código ausente")
        # Inequívoca: o antigo está ALTERADO e encerrado; o novo tem o mesmo nome, está aberto, e é o único outro
        # código com esse nome.
        mesmo_nome = [p["codigo"] for p in paises if p["nome"] == a["nome"] and p["codigo"] != antigo]
        if a["situacao"] != "ALTERADO" or a["fim"] is None or n["fim"] is not None or mesmo_nome != [novo]:
            falha(f"sucessão {antigo}->{novo}: não é uma troca de código explícita na planilha")
    encerrados_alterados = sorted(p["codigo"] for p in paises if p["situacao"] == "ALTERADO" and p["fim"] is not None)
    if encerrados_alterados != sorted(SUCESSORES):
        falha(f"códigos ALTERADO encerrados diferentes dos conferidos: {encerrados_alterados}")

    vigentes = [p for p in paises if p["inicio"] <= DATA_REFERENCIA and (p["fim"] is None or DATA_REFERENCIA <= p["fim"])]
    if len(paises) != 261 or len(vigentes) != 249:
        falha(f"contagem diferente da conferida: {len(paises)} códigos, {len(vigentes)} vigentes")
    if max(len(p["nome"]) for p in paises) > 60:
        falha("nome com mais de 60 caracteres")

    # --- C# ---
    L = []
    L.append("// <auto-generated>")
    L.append("// Gerado por Ferramentas/gerar-paises-nfe.py. NÃO EDITAR À MÃO: corrija a fonte ou o gerador e gere de novo.")
    L.append(f"// Fonte: {FONTE}, versão {VERSAO}, aba {ABA}.")
    L.append(f"// SHA-256 da planilha: {HASH_ESPERADO}")
    L.append("// </auto-generated>")
    L.append("#nullable enable")  # arquivo marcado como gerado: sem isto o compilador desliga o contexto de nulos
    L.append("")
    L.append("namespace Lone.Domain.Enderecos;")
    L.append("")
    L.append("/// <summary>Uma linha do catálogo oficial de países da NF-e (cPais), como veio da fonte.</summary>")
    L.append("/// <param name=\"CodigoPaisNFe\">cPais com 4 dígitos (zeros à esquerda preservados).</param>")
    L.append("/// <param name=\"NomeFiscal\">Nome atual na tabela oficial, sem ajuste.</param>")
    L.append("/// <param name=\"SituacaoFonte\">Coluna SITUAÇÃO da planilha (nula quando vazia).</param>")
    L.append("/// <param name=\"CodigoSucessor\">Código novo, só nas trocas de código explícitas. Nada é convertido automaticamente.</param>")
    L.append("/// <param name=\"NomeAnterior\">Nome antes da renomeação (só para conferência; não vai para o banco).</param>")
    L.append("public sealed record PaisNFeOficial(")
    L.append("    string CodigoPaisNFe, string NomeFiscal, string? SituacaoFonte,")
    L.append("    DateOnly VigenciaInicio, DateOnly? VigenciaFim, string? CodigoSucessor, string? NomeAnterior);")
    L.append("")
    L.append("/// <summary>Catálogo oficial de países da NF-e, NT 2018.003 v1.01: 261 códigos (249 vigentes em 07/10/2026).</summary>")
    L.append("public static class PaisesNFeOficiais")
    L.append("{")
    L.append(f"    public const string Fonte = {cs(FONTE)};")
    L.append(f"    public const string VersaoFonte = {cs(VERSAO)};")
    L.append(f"    public const string HashFonte = {cs(HASH_ESPERADO)};")
    L.append("")
    L.append("    public static IReadOnlyList<PaisNFeOficial> Todos { get; } =")
    L.append("    [")
    for i, p in enumerate(paises):
        partes = [
            cs(p["codigo"]), cs(p["nome"]), cs(p["situacao"]) if p["situacao"] else "null",
            cs_data(p["inicio"]), cs_data(p["fim"]) if p["fim"] else "null",
            cs(p["sucessor"]) if p["sucessor"] else "null", cs(p["anterior"]) if p["anterior"] else "null",
        ]
        L.append("        new(" + ", ".join(partes) + ")" + ("," if i < len(paises) - 1 else ""))
    L.append("    ];")
    L.append("}")
    conteudo = "\n".join(L) + "\n"
    destino = os.path.join(RAIZ, SAIDA_CS)
    if not os.path.isdir(os.path.dirname(destino)):
        falha(f"pasta de destino não encontrada: {os.path.dirname(destino)}")
    with open(destino, "w", encoding="utf-8", newline="\n") as f:
        f.write(conteudo)

    # --- relatório (saída padrão) ---
    situacoes = {}
    for r in brutas:
        situacoes[r["situacao"] or "(vazia)"] = situacoes.get(r["situacao"] or "(vazia)", 0) + 1
    print(f"Planilha: SHA-256 {hash_atual} (conferido)")
    print(f"Aba {ABA}: {len(brutas)} linhas de dados; {len(paises)} códigos distintos; {len(vigentes)} vigentes em {DATA_REFERENCIA:%d/%m/%Y}")
    print(f"Encerrados: {len(paises) - len(vigentes)}: " + ", ".join(f"{p['codigo']} (fim {p['fim']:%d/%m/%Y})" for p in paises if p not in vigentes))
    print(f"Códigos abaixo de 1000 (zero à esquerda restaurado): {sum(1 for p in paises if p['codigo'] < '1000')}")
    print("Situações (linhas): " + "; ".join(f"{k}: {v}" for k, v in sorted(situacoes.items())))
    print("Renomeações consolidadas:")
    for codigo, antigo, novo, a, n in renomeados:
        print(f"  {codigo}: {antigo!r} (linha {a['linha']}) -> {novo!r} (linha {n['linha']})")
    print("Sucessões registradas (sem conversão de dados):")
    for antigo, novo in SUCESSORES.items():
        print(f"  {antigo} -> {novo}")
    print(f"Gerado: {SAIDA_CS} ({len(conteudo.encode('utf-8'))} bytes, SHA-256 {hashlib.sha256(conteudo.encode('utf-8')).hexdigest()})")


if __name__ == "__main__":
    main()
