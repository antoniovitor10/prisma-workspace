# Integração contínua

O workflow `CI` executa quatro gates independentes em pushes e pull requests para `main`:

- restore e build do backend .NET; execução de testes retirada deste job por solicitação do mantenedor em 22/09/2026;
- instalação reprodutível, lint, testes e build do frontend;
- testes do modelo de governança/Context Explorer;
- construção completa da imagem Docker.

As Actions externas são fixadas por SHA e atualizadas pelo Dependabot. O frontend também bloqueia advisories npm de
severidade crítica. Vulnerabilidades já inventariadas de severidade alta/moderada continuam no backlog de correção
controlada e não devem ser “corrigidas” com atualização major automática.

O repositório foi tornado público pelo mantenedor. Dependency review ainda não integra este workflow.
Os testes backend continuam no repositório e podem ser executados sob demanda em ambiente com banco exclusivo.
Build aprovado comprova compilação, não substitui validação funcional. O ambiente `npm run dev` não executa testes.

## Referências oficiais

- [Build e testes .NET no GitHub Actions](https://docs.github.com/en/actions/tutorials/build-and-test-code/net)
- [Build e testes Node.js no GitHub Actions](https://docs.github.com/en/actions/tutorials/build-and-test-code/nodejs)
- [Uso seguro do GitHub Actions](https://docs.github.com/en/actions/reference/security/secure-use)
- [Dependency review e disponibilidade](https://docs.github.com/en/code-security/concepts/supply-chain-security/dependency-review)
