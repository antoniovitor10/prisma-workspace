---
name: Prisma WorkSpace D68
description: Identidade existente da SPA, extraída dos tokens e controles implementados.
colors:
  brand: "#2563EB"
  brand-control: "#1D4ED8"
  violet: "#7C3AED"
  cyan: "#06B6D4"
  magenta: "#DB2777"
  orange: "#F97316"
  light-bg: "#F6F7FB"
  light-surface: "#FFFFFF"
  light-text: "#0F172A"
  light-muted: "#475569"
  light-border: "#E5E8F0"
  dark-bg: "#070B17"
  dark-surface: "#0E1424"
  dark-text: "#F8FAFC"
  dark-muted: "#CBD5E1"
typography:
  body:
    fontFamily: "'Inter', system-ui, sans-serif"
    fontSize: "1rem"
    fontWeight: 400
  title:
    fontFamily: "'Inter', system-ui, sans-serif"
    fontSize: "1.5rem"
    fontWeight: 700
rounded:
  sm: "6px"
  md: "10px"
  lg: "14px"
spacing:
  sm: "8px"
  md: "16px"
  lg: "24px"
components:
  button-primary:
    backgroundColor: "{colors.brand-control}"
    textColor: "{colors.light-surface}"
    rounded: "{rounded.md}"
---

# Prisma WorkSpace D68

## Overview

Registro da identidade existente, sem proposta de novo estilo. Fonte normativa de
implementação: src/styles/theme.ts; autoridade de produto: D68 em DECISIONS.md.
O espectro Prisma identifica a marca. Controles funcionais usam cores acessíveis,
superfícies do tema, hierarquia de leitura e linguagem direta.

## Colors

O espectro reúne violeta, azul, cyan, magenta e laranja. O azul de controles é
brandControlBackground; textos de ação usam brandAccessible, que varia por tema.
Fundos, bordas e texto devem vir do tema, preservando as variantes clara e escura.
Valores desta frontmatter descrevem tokens existentes; não substituem o objeto AppTheme.

## Typography

Inter é a família de interface, com fallback de sistema. A escala existente usa
12, 14, 16, 18, 24 e 32px. Instrument Serif continua disponível no tema para as
superfícies que já o usam. No assistente, rótulos usam 12px, respostas 14px com
entrelinha 1.6 e títulos de seção 24px. Textos e identificadores longos quebram linha.

## Layout

Espaçamento deriva da escala de 4px, com controles agrupados por finalidade.
O chat lateral mede até 480px; expandido, até 1100px, com histórico de 230px.
Em até 768px, ocupa a tela e oferece histórico recolhível. Em até 480px, cada
seletor tem sua própria linha. A resposta rola independentemente do compositor.
Administração mantém poucos campos principais e detalhes expansíveis.

## Elevation & Depth

A interface usa separação por bordas e superfícies. O painel flutuante usa shadow.lg
do tema existente. Não introduzir materiais simulados ou sombras novas como identidade.

## Shapes

Controles usam radius.md; o painel desktop usa radius.lg. No mobile, o painel é
retangular e ocupa a tela. Campos do assistente mantêm foco visível e altura de
42px ou mais; formulários de conexão usam 44px.

## Components

A ação principal usa azul e texto branco; ações secundárias usam a superfície e
borda do tema. Ações textuais têm sublinhado e foco visível. Estados de carregamento,
erro, falta de login e indisponibilidade devem indicar uma ação possível.
Seletores do chat representam a conversa; configuração e credenciais ficam na
administração. O histórico deve permanecer acessível sem consumir toda a leitura.

## Do's and Don'ts

- Reutilizar tokens D68 e respeitar tema claro/escuro, contraste e teclado.
- Preservar nomes das conexões para distinguir contas do mesmo provedor.
- Bloquear mudanças enquanto a conversa gera uma resposta.
- Não introduzir ícones, emojis, bibliotecas ou identidade visual novos nesta extensão.
- Não copiar recursos externos sem contrato aprovado nem apresentar limites de assinatura como medidos.
