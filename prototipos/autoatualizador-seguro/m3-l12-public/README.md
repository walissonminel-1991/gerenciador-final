# GERENCIADOR FINAL — L12: referência pública (somente ensaio)
Este projeto `.NET 10` isolado reutiliza a cadeia de chaves **exclusivamente de teste** Stage11/Stage3 e o contrato inerte do L11. Uma chave RSA-PSS volátil de laboratório assina referências públicas globais, sem instalaçãoId, tokens, cadastro ou tráfego HTTP. Os resultados são somente observações, **nunca autorizações**.

- **static:** referência assinada, compartilhável sem backend individual; autentica bytes mas não prova publicação mais recente, mesmo com validade se o relógio local for restaurado.
- **challenge:** simula emissor online que assina um nonce. Verifica que alguém com a chave assinou o desafio; não demonstra que o emissor é honesto nem monotonicidade contra comprometimento. Esse modo dependeria de backend signatário dinâmico e não é fornecido por GitHub Releases estáticas; **não foi implantado**.
- **Restauração integral:** mesmo binário, chave pública e piso retornam ao passado, permitindo observar checkpoint antigo; o caso PASS reproduz **falha residual**, não proteção.
- **Operador comprometido:** signatário malicioso pode criar nova assinatura de regressão e respostas conflitantes. Sem domínio externo independente/monitor, challenge não resolve A5.
- **Limites:** certificado inicial do verificador, raiz TUF, relógio confiável, PITR, privacidade, manutenção D6-02/G2, G5/G6/G8, NTFS/energia e homologação permanecem pendentes. Nenhuma conexão de rede, comando, atualização real ou build do GF acontece.

Workflow Windows 2022 em diretórios temporários e com checkout sem credenciais. Nenhuma alteração em PR #65, main, BETA .66, Cloudflare, domínio, consentimento, telemetria ou PCs. **G1–G8 BLOCKED (0/8).**
