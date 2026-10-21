---
id: ADR-0102
titulo: "Parâmetros provisórios do corte 2a da integração Git do método"
status: proposta
data: 2026-10-16
origem: "DECISIONS.md D102"
substitui: ""
---

# ADR-0102 · Parâmetros provisórios do corte 2a da integração Git do método

## Decisão

- **D102** — Parâmetros da fase 2a da integração Git do método, adotados **provisoriamente** pelo Maestro em 2026-10-16, por delegação do PO ("colocar alguns goals e já publicar em produção", "roda todo o fluxo com testes bem feitos de QA"), **a confirmar pelo PO**. Adota as recomendações de `specs/metodo-git-integration.md` para: **PD-01** (quadro do método por projeto com as sete colunas; só Concluída com categoria concluída; tasks de topo, HU só em campo e tag; sem automação de descendência nem conclusão de HU; habilitado só por provisionamento explícito de um admin); **PD-05, apenas o corte 2a** (token padrão 30 dias e máximo 90; `LastUsedAtUtc` em janela de 5 min; rotação HMAC com o segredo anterior válido por 24 h; corpo do webhook até 5 MiB, acima disso 413 sem efeito e aviso visível; entregas terminadas retidas 30 dias com expurgo diário; fatos e histórico permanentes); **PD-06** (merge retido por outro PR que depois fecha sem merge não promove pelo merge antigo; PR reaberto não draft conta como nova abertura só com a task em desenvolvimento; volta a draft só registra fato; vale a última review não dispensada de cada revisor no HEAD vigente e qualquer changes requested efetivo impede promoção; vínculo tardio não desfaz intervenção humana posterior). Salvaguarda: a 2a entra em produção desligada; nada muda para usuários humanos na UI e nada acontece em projeto não provisionado ou sem GitHub App instalada. PD-04, PD-07 e o agendamento da 2b continuam abertos para o PO. Se o PO não confirmar, os parâmetros são ajustados antes de habilitar a integração em qualquer projeto.
