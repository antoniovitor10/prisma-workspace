// Tipos de credenciais.mjs, para os arquivos TypeScript da suíte de produção.

export declare const ARQUIVO_PADRAO: string;
export declare const URL_PADRAO: string;
export declare const VARIAVEIS_OBRIGATORIAS: readonly string[];
export declare const VARIAVEL_PERMITIR_OUTRO_HOST: string;

/** Estado da leitura das credenciais: só caminho e NOMES de variáveis, nunca valores. */
export type EstadoDasCredenciais = {
  arquivo: string;
  existe: boolean;
  faltando: string[];
  invalidas: string[];
};

export declare function caminhoDasCredenciais(raizWeb: string): string;
export declare function carregarCredenciais(raizWeb: string): EstadoDasCredenciais;
export declare function mensagemDeCredenciaisAusentes(estado: EstadoDasCredenciais): string | null;
export declare function enderecoDaProducao(): string;
