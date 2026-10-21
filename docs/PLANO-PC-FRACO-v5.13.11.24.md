# Checklist PC fraco — v5.13.11.24 TESTE

1. Extrair pacote .24 em pasta nova; manter AVG ativo. Se houver alerta,
   não criar exceção nem restaurar binário de quarentena.
2. Aplicativos > Inventário: pesquisar `MSIX`, confirmar linhas
   somente leitura; resultados anteriores 74 apps / 19 MSIX.
3. Abrir Investigação de resíduos somente leitura; clicar Resumo.
   Procurar distinção **fontes com falhas pontuais de leitura**
   versus **fontes não consultadas ou inacessíveis**.
4. Verificar que o aviso ANÁLISE PARCIAL continua e que filtros
   por fonte não misturam Registro, dados comuns e perfis.
5. No log sanitizado, verificar `motivos=`, `raizes_restritas=`,
   `fontes_leitura_parcial=` e
   `fontes_nao_consultadas_ou_inacessiveis=`.
   São contagens/flags; não publicar caminhos locais.
6. Medir RAM, duração da descoberta, rolagem e encerramento.
   Não alterar drivers, apagar resíduos, instalar ou desinstalar.

Os limites de segurança (dois níveis e sem seguir links) permanecem.
A cobertura parcial não é defeito suficiente para autorizar limpeza.
PR #40 permanece Draft; roadmap 26/42.
