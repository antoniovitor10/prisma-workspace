import { describe, expect, it } from 'vitest';
import { liveTimeSeconds } from './liveTime';

describe('totais com cronômetro já aberto',()=>{
  const started='2026-10-14T15:44:00Z';
  const snapshot='2026-10-15T10:12:00Z';
  const seconds=18*3600+28*60;
  it('na recarga preserva 18h28 e acrescenta somente os segundos após o cálculo',()=>{
    expect(liveTimeSeconds(seconds,snapshot,started,Date.parse(snapshot))).toBe(seconds);
    expect(liveTimeSeconds(seconds,snapshot,started,Date.parse(snapshot)+5000)).toBe(seconds+5);
    expect(liveTimeSeconds(seconds+120,snapshot,started,Date.parse(snapshot)+5000)).toBe(seconds+125);
  });
  it('timer iniciado após a leitura soma só desde o início; parado conserva total',()=>{
    expect(liveTimeSeconds(120,'2026-10-14T15:40:00Z',started,Date.parse(started)+5000)).toBe(125);
    expect(liveTimeSeconds(120,snapshot,null,Date.parse(snapshot)+5000)).toBe(120);
  });
  it('não duplica totais sem snapshot nem subtrai por relógio anterior',()=>{
    expect(liveTimeSeconds(seconds,undefined,started,Date.parse(snapshot))).toBe(seconds);
    expect(liveTimeSeconds(seconds,snapshot,started,Date.parse(snapshot)-5000)).toBe(seconds);
  });
});
