import { api } from '../../services/api';
import type { GlobalSearchResponse } from './types';

export const searchPlatform = (query:string):Promise<GlobalSearchResponse> => api.globalSearch(query,6);
