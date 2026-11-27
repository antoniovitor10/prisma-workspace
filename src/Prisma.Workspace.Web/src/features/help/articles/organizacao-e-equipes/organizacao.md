---
titulo: Organização, membros e convites
resumo: Criar e trocar de organização, convidar pessoas, perfis e as configurações da organização.
ordem: 1
atualizado_em: 2026-11-27
revisar_ate: 2027-05-27
paginas:
  - /home#primeiros-passos
  - /settings#configuracoes-da-organizacao
relacionados:
  - organizacao-e-equipes/projetos
  - organizacao-e-equipes/equipes
---

A organização é o ambiente isolado da sua equipe: projetos, quadros, equipes e pessoas de uma organização não aparecem para quem está em outra.

## Primeiros passos {#primeiros-passos}

1. Crie a organização (ou aceite o convite de uma que já existe).
2. Convide as pessoas em **Configurações**, seção **Membros**.
3. Crie um projeto em **Projetos** > **Novo projeto**. Ele já nasce com um quadro.
4. Dê acesso ao projeto em **Configurações do projeto** > **Pessoas e equipes**.
5. Planeje no **Backlog** e acompanhe no **Kanban**.

## Criar a primeira organização {#criar-organizacao}

Quem ainda não pertence a nenhuma organização vê a tela **Crie sua organização**. Preencha **Nome da organização** (até 160 caracteres) e clique em **Criar ambiente**. Você passa a ser administrador dessa organização.

> Se você recebeu um convite, não crie uma organização nova: abra o link do convite. Sem o link, o cadastro cria um ambiente separado em vez de entrar no da sua equipe.

## Criar outra organização {#nova-organizacao}

No menu da conta (o círculo no canto superior direito), escolha **Nova organização**, preencha o nome e clique em **Criar organização**. O Prisma troca para a organização nova e abre o Início.

A opção só aparece para quem pode criar: quem ainda não tem organização ou o administrador da instalação. Para os demais, o servidor responde "Somente o administrador da plataforma pode criar outra organização."

## Trocar de organização {#trocar-organizacao}

Quando você participa de duas ou mais organizações, a barra superior mostra o seletor **Selecionar organização**. Ao trocar, o Prisma abre o Início da organização escolhida (ou Solicitações, para o perfil Solicitante externo).

## Configurações da organização {#configuracoes-da-organizacao}

Abra pelo menu da conta > **Configurações**. A opção aparece para quem administra a organização. A página tem estas seções:

- **Assistente de IA**: ligar a IA na organização e definir cotas (veja [Administração da instalação](/ajuda/administracao/instalacao-e-configuracao#assistente-de-ia)).
- **Preferências gerais**: nome, idioma, fuso horário e início da semana. Botão **Salvar preferências**.
- **Seu acesso**: mostra o seu perfil.
- **Membros**: lista as pessoas, com o perfil de cada uma, a caixa **Ativo** e o formulário de convite.
- **Permissões específicas**: exceções por pessoa (permitir ou negar uma permissão num escopo). Uma negação explícita prevalece.
- **Templates de workflow**: o fluxo herdado pelos projetos novos. **Criar fluxo padrão** cria A fazer, Em andamento e Concluído.
- **Auditoria**: registro das ações administrativas, com filtros.

## Convidar pessoas {#convidar-pessoas}

1. Em **Configurações**, vá até **Membros**.
2. Preencha **E-mail** e escolha o **Perfil** (o padrão é Membro da equipe).
3. Clique em **Gerar convite**.
4. O link aparece com o botão **Copiar convite**. O convite vale 7 dias.

Quem abre o link vê "Convite para" e o nome da organização. Se a conta já existe, basta entrar com a senha (**Entrar e aceitar convite**); se não existe, **Criar conta e entrar** cria a conta já dentro da organização.

### Convite sem e-mail configurado {#convite-sem-email}

Se a instalação não tem e-mail configurado, o Prisma avisa: "Não foi possível enviar o e-mail. Compartilhe o link acima com a pessoa convidada". Copie o link e envie você mesmo. Com e-mail configurado, o link continua disponível como alternativa.

### Problemas com o convite {#problemas-com-convite}

- **Convite de outra conta**: a pessoa entrou com um e-mail diferente do convidado. Use **Sair e aceitar com o e-mail convidado**.
- **Convite indisponível**: o convite venceu ou já foi usado. Gere um novo.

## Perfis da organização {#perfis}

| Perfil | Para quem |
|---|---|
| Administrador | administra a organização e todos os projetos |
| Gestor | administra todos os projetos |
| Gerente de projeto | administra todos os projetos |
| Scrum Master | conduz sprints nos projetos em que tem acesso |
| Product Owner | prioriza o backlog nos projetos em que tem acesso |
| Membro da equipe | trabalha nas tarefas dos projetos em que tem acesso |
| Desenvolvedor | trabalha nas tarefas dos projetos em que tem acesso |
| Solicitante externo | só abre e acompanha Solicitações; não vê Início, Meu trabalho nem Projetos |
| Cliente | vê, comenta e consulta relatórios |
| Visualizador | vê e consulta relatórios |

Administrador, Gestor e Gerente de projeto administram todos os projetos da organização. Os demais perfis só enxergam os projetos aos quais receberam acesso (veja [Dar acesso ao projeto](/ajuda/organizacao-e-equipes/projetos#dar-acesso-ao-projeto)).
