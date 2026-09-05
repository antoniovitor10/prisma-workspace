import { api } from '../../services/api';
import type { AuditPage } from './types';

export const searchAudit=(filters:Record<string,string>):Promise<AuditPage>=>{
  const query=new URLSearchParams(Object.entries(filters).filter(([,value])=>Boolean(value))).toString();
  return api.getAuditLogs(query);
};
