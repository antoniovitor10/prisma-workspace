# Integração contínua

O workflow `CI` executa quatro gates independentes em pushes e pull requests para `main`:

- restore, build e 105 testes do backend .NET;
- instalação reprodutível, lint, 46 testes e build do frontend;
- 42 testes do modelo de governança/Context Explorer;
- construção completa da imagem Docker.

As Actions externas são fixadas por SHA e atualizadas pelo Dependabot. O frontend também bloqueia advisories npm de
severidade crítica. Vulnerabilidades já inventariadas de severidade alta/moderada continuam no backlog de correção
controlada e não devem ser “corrigidas” com atualização major automática.

O dependency review será habilitado quando o repositório se tornar público; no estado privado atual ele pode exigir
GitHub Code Security e, por isso, ainda não integra o workflow obrigatório.

## Referências oficiais

- [Build e testes .NET no GitHub Actions](https://docs.github.com/en/actions/tutorials/build-and-test-code/net)
- [Build e testes Node.js no GitHub Actions](https://docs.github.com/en/actions/tutorials/build-and-test-code/nodejs)
- [Uso seguro do GitHub Actions](https://docs.github.com/en/actions/reference/security/secure-use)
- [Dependency review e disponibilidade](https://docs.github.com/en/code-security/concepts/supply-chain-security/dependency-review)
