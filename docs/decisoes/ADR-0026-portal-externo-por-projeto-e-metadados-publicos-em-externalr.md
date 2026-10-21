---
id: ADR-0026
titulo: "Portal externo por projeto e metadados públicos em ExternalRequest"
status: aceita
data: 2026-07-20
origem: "DECISIONS.md D26"
substitui: ""
---

# ADR-0026 · Portal externo por projeto e metadados públicos em ExternalRequest

## Decisão

- **D26** — O Portal Externo é configurado por projeto e quadro de entrada, mas cada solicitação
  continua sendo o mesmo `WorkItem` conforme D16/D21. Metadados públicos ficam em
  `ExternalRequest`: protocolo, chave de acompanhamento armazenada apenas como hash, conversa,
  avaliação e confirmação. O acesso pode usar link público, JWT, convite ou código de e-mail;
  códigos só retornam na resposta quando o host está em Development e não há SMTP configurado.
  (2026-07-20)
