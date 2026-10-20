#!/usr/bin/env python3
"""Triagem estrita de Issues publicas geradas pelo piloto local do GF.

Nao baixa anexos, nao executa texto da Issue, nao publica comentarios,
nao grava identificadores de pessoas e nao usa credenciais de escrita.
"""
from __future__ import annotations

import json
import os
import re
import sys
from pathlib import Path

ALLOWED = frozenset({
    "APP.FALHA_INTERFACE", "APP.FALHA_NAO_TRATADA",
    "REGISTRO_PROGRAMAS.FALHA", "MSIX.LEITURA",
    "MULTIFONTES.CONSULTA_FALHA", "MULTIFONTES.CONSULTA_CANCELADA",
    "DESINSTALACAO.FALHA", "INSTALACAO_OFICIAL.FALHA",
    "TERMINAL_GITHUB.FALHA", "JANELA.BANDEJA_RESTAURAR.INICIO",
    "JANELA.BANDEJA_RESTAURAR.FIM", "JANELA.BANDEJA_OCULTAR.INICIO",
    "JANELA.BANDEJA_OCULTAR.FIM", "DIAGNOSTICO_COMPLETO.FIM",
})
VERSION = r"v[0-9]{1,3}(?:\.[0-9]{1,4}){3}"
TITLE_RE = re.compile(r"\[GF Diagnostico\] (" + VERSION + r") - resumo voluntario\Z")
COUNT_RE = re.compile(r"- ([A-Z0-9_.]+): ([1-9][0-9]{0,2})\Z")

INTRO = (
    "## Diagnóstico voluntário do GERENCIADOR FINAL",
    "",
    "> Esta Issue será PÚBLICA e ficará associada à sua conta GitHub.",
    "> Confira o conteúdo antes de clicar em Submit new issue.",
    "> Nenhum relatório completo, nome de programa, arquivo ou caminho foi incluído.",
    "",
)
SEPARATOR = "### Ocorrências técnicas (agregadas; não representam usuários únicos)"
OUTRO = (
    "",
    "### Observações",
    "Se desejar, descreva o problema SEM publicar dados pessoais, arquivos ou logs completos.",
    "A Issue só é publicada após sua confirmação no GitHub.",
)


class RejectedReport(ValueError):
    """Conteudo nao canonico: rejeitar sem repetir o texto nao confiavel."""


def parse_issue(title: object, body: object) -> dict:
    """Exige formato EXATO do cliente, sem observar ou reproduzir notas livres."""
    if not isinstance(title, str) or not isinstance(body, str):
        raise RejectedReport("tipo invalido")
    if len(title) > 128 or len(body.encode("utf-8")) > 5000:
        raise RejectedReport("tamanho excessivo")
    title_match = TITLE_RE.fullmatch(title)
    if title_match is None:
        raise RejectedReport("titulo nao reconhecido")
    if "\r" in body.replace("\r\n", "") or "\x00" in body:
        raise RejectedReport("quebras ou controles inesperados")
    lines = body.replace("\r\n", "\n").split("\n")
    if lines and lines[-1] == "":
        lines.pop()  # AppendLine() do gerador C#
    if len(lines) < len(INTRO) + len(OUTRO) + 5:
        raise RejectedReport("formato incompleto")
    if tuple(lines[:len(INTRO)]) != INTRO:
        raise RejectedReport("cabecalho nao reconhecido")
    idx = len(INTRO)
    version = title_match.group(1)
    if lines[idx] != "**Versão do GF:** " + version:
        raise RejectedReport("versoes divergentes")
    idx += 1
    state = {
        "**Estado da sessão:** encerramento normal": "normal",
        "**Estado da sessão:** coleta em andamento ou interrompida": "incompleto",
    }.get(lines[idx])
    if state is None or lines[idx + 1] != "" or lines[idx + 2] != SEPARATOR:
        raise RejectedReport("sessao ou secao invalida")
    idx += 3

    counts: dict[str, int] = {}
    while idx < len(lines) and lines[idx].startswith("- "):
        found = COUNT_RE.fullmatch(lines[idx])
        if found is None:
            raise RejectedReport("contagem invalida")
        category, text_count = found.groups()
        amount = int(text_count)
        if category not in ALLOWED or category in counts or not (1 <= amount <= 100):
            raise RejectedReport("categoria ou quantidade nao autorizada")
        counts[category] = amount
        if len(counts) > 14:
            raise RejectedReport("categorias demais")
        idx += 1
    if not counts or list(counts) != sorted(counts):
        raise RejectedReport("categorias ausentes ou fora de ordem")
    if tuple(lines[idx:]) != OUTRO:
        # Observacoes livres sao intencionalmente ignoradas, NAO ingeridas.
        # Rejeitar o corpo todo evita classificar como confiavel uma Issue editada.
        raise RejectedReport("texto adicional ou rodape diferente")
    return {"schema": 1, "version": version, "exit": state, "counts": counts}


def main() -> int:
    event_file = os.environ.get("GITHUB_EVENT_PATH")
    if not event_file:
        print("Erro: evento GitHub indisponivel.")
        return 2
    try:
        file = Path(event_file)
        if file.stat().st_size > 256 * 1024:
            raise RejectedReport("evento muito grande")
        event = json.loads(file.read_text(encoding="utf-8"))
        issue = event.get("issue", {})
        if event.get("action") != "opened" or not isinstance(issue, dict) or "pull_request" in issue:
            raise RejectedReport("evento nao e uma Issue nova")
        report = parse_issue(issue.get("title"), issue.get("body"))
    except (OSError, UnicodeError, json.JSONDecodeError, AttributeError, RejectedReport):
        # NUNCA imprimir titulo, corpo, anexos, autor ou payload original.
        print("Triagem recusada: Issue fora do formato seguro.")
        return 1

    summary_path = os.environ.get("GITHUB_STEP_SUMMARY")
    if summary_path:
        safe_rows = [
            "## Triagem de diagnostico voluntario",
            "",
            "Apenas campos de allowlist; sem notas livres, identificadores ou anexos.",
            "",
            f"- Versao: {report['version']}",
            f"- Sessao: {report['exit']}",
            f"- Categorias reconhecidas: {len(report['counts'])}",
            "",
            "| Categoria | Ocorrencias |",
            "| --- | ---: |",
        ]
        safe_rows += [f"| {name} | {number} |" for name, number in report["counts"].items()]
        with open(summary_path, "a", encoding="utf-8") as summary:
            summary.write("\n".join(safe_rows) + "\n")
    print(f"Triagem valida: {len(report['counts'])} categorias agregadas.")
    return 0


if __name__ == "__main__":
    sys.exit(main())
