#!/usr/bin/env node
// Mostra um arquivo de texto com os dados pessoais mascarados, para o agente ler sem que o dado
// real vá para o provedor do modelo. Roda localmente: só a saída mascarada entra no contexto.
//
// Uso:
//   node metodo/nucleo/scripts/mascarar.mjs docs/insumos/brutos/conversa.txt
//   node metodo/nucleo/scripts/mascarar.mjs arquivo.csv --placas      # mascara também placas de veículo
//   pdftotext arquivo.pdf - | node metodo/nucleo/scripts/mascarar.mjs - # PDF, docx etc.: converta para texto antes
//
// Mascara: CPF, CNPJ, e-mail, telefone, cartão, qualquer sequência de 11 dígitos (CPF ou CNH sem pontuação),
// RG com rótulo e, com --placas, placas. Não é infalível: nome e endereço não são detectados.
// Arquivo marcado com "dados pessoais: sim" no INDICE.md só é lido por aqui, salvo autorização do desenvolvedor.

import fs from 'node:fs';

const args = process.argv.slice(2);
const alvo = args.find((a) => !a.startsWith('--'));
if (!alvo) { console.error('Uso: node mascarar.mjs <arquivo | -> [--placas]'); process.exit(2); }
const placas = args.includes('--placas');

let texto;
try { texto = alvo === '-' ? fs.readFileSync(0, 'utf8') : fs.readFileSync(alvo, 'utf8'); }
catch (e) { console.error(`Não consegui ler ${alvo}: ${e.message}`); process.exit(2); }
if (texto.includes('\u0000')) { console.error('Arquivo binário: converta para texto antes (pdftotext, pandoc) e passe por stdin com "-".'); process.exit(2); }

const conta = {};
const troca = (rotulo) => (m) => { conta[rotulo] = (conta[rotulo] || 0) + 1; return `[${rotulo}]`; };

const REGRAS = [
  ['EMAIL', /[A-Za-z0-9._%+-]+@[A-Za-z0-9.-]+\.[A-Za-z]{2,}/g],
  ['CNPJ', /\b\d{2}\.?\d{3}\.?\d{3}\/?\d{4}-?\d{2}\b/g],
  ['CPF', /\b\d{3}\.\d{3}\.\d{3}-\d{2}\b/g],
  ['CARTAO', /\b(?:\d[ -]?){13,19}\b/g],
  ['TELEFONE', /(?:\+?55\s?)?\(?\b\d{2}\)?\s?9?\d{4}[-\s]?\d{4}\b/g],
  ['DOC11', /\b\d{11}\b/g],
  ['RG', /\b(RG|R\.G\.|identidade)\s*[:nº.-]*\s*[\dXx.\-]{5,14}/gi],
];
if (placas) REGRAS.push(['PLACA', /\b[A-Z]{3}-?\d[A-Z0-9]\d{2}\b/g]);

for (const [rotulo, re] of REGRAS) texto = texto.replace(re, troca(rotulo));

process.stdout.write(texto);
const resumo = Object.entries(conta).map(([k, v]) => `${k}: ${v}`).join(' · ') || 'nada encontrado';
console.error(`\n[mascarar] ${resumo}. Nomes e endereços não são detectados: trate o conteúdo como dado pessoal.`);
