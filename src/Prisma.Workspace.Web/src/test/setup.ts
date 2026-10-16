import '@testing-library/jest-dom/vitest';
import { act } from '@testing-library/react';
import { notifyManager } from '@tanstack/react-query';

// Em jsdom, uma busca custosa por role pode esgotar seu prazo antes do setTimeout
// usado pelo React Query para notificar dados já resolvidos. Microtarefas preservam
// a assincronia; act conclui o render antes de testar o DOM, sem ampliar timeouts.
notifyManager.setScheduler(queueMicrotask);
notifyManager.setNotifyFunction(callback => { act(() => { callback(); }); });
