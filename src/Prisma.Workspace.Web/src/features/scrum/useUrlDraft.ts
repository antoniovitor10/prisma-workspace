import { useCallback, useEffect, useRef, useState } from 'react';

/**
 * Campo de texto ligado à URL sem perder teclas (T-0016).
 *
 * O que a pessoa digita vai para o estado local na hora e só segue para a URL depois de
 * `delay` ms sem digitar, ou ao sair do campo (`flush`). Se o campo fosse controlado pela
 * URL, cada tecla esperaria o React Router aplicar a navegação (assíncrona) e as teclas
 * mais rápidas que isso partiriam do valor antigo.
 *
 * Mudanças da URL que vêm de fora (voltar/avançar, link aberto, "Limpar filtros") atualizam
 * o campo, menos enquanto há digitação ainda não enviada: nesse caso vale o que a pessoa
 * está digitando, que chega à URL em seguida.
 */
export function useUrlDraft(urlValue: string, commit: (value: string) => void, delay = 300) {
  const [draft, setDraft] = useState(urlValue);
  const timer = useRef<number | undefined>(undefined);
  const values = useRef({ draft: urlValue, committed: urlValue });
  const commitRef = useRef(commit);

  useEffect(() => { commitRef.current = commit; }, [commit]);

  const flush = useCallback(() => {
    if (timer.current === undefined) return;
    window.clearTimeout(timer.current);
    timer.current = undefined;
    values.current.committed = values.current.draft;
    commitRef.current(values.current.draft);
  }, []);

  const change = useCallback((value: string) => {
    values.current.draft = value;
    setDraft(value);
    window.clearTimeout(timer.current);
    timer.current = window.setTimeout(flush, delay);
  }, [delay, flush]);

  // Troca o valor sem enviar nada à URL: para quem já vai gravar a URL por conta própria.
  const replace = useCallback((value: string) => {
    window.clearTimeout(timer.current);
    timer.current = undefined;
    values.current = { draft: value, committed: value };
    setDraft(value);
  }, []);

  useEffect(() => {
    if (timer.current !== undefined || urlValue === values.current.committed) return;
    values.current = { draft: urlValue, committed: urlValue };
    setDraft(urlValue);
  }, [urlValue]);

  // Ao sair da tela, descarta o envio pendente: gravá-lo depois alteraria a URL da tela nova.
  useEffect(() => () => {
    window.clearTimeout(timer.current);
    timer.current = undefined;
  }, []);

  return { value: draft, change, flush, replace };
}
