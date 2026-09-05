'use strict';

const ACCEPTANCE_HEADING = /(acceptance criteria|crit[eé]rios? de aceit)/i;

function cleanMarkdown(value) {
  return String(value || '')
    .replace(/^\s*(?:[-*+]\s+|\d+[.)]\s+)/, '')
    .replace(/\[(.*?)\]\([^)]*\)/g, '$1')
    .replace(/[`*_>#]/g, '')
    .replace(/\s+/g, ' ')
    .trim();
}

function scenarioBlocks(body) {
  const blocks = [];
  let current = [];

  for (const line of String(body || '').split(/\r?\n/)) {
    if (/^\s*(?:[-*+]\s+|\d+[.)]\s+)/.test(line)) {
      if (current.length) blocks.push(current.join(' '));
      current = [line];
    } else if (current.length && line.trim()) {
      current.push(line);
    }
  }

  if (current.length) blocks.push(current.join(' '));
  return blocks.map(cleanMarkdown).filter(Boolean);
}

function parseScenario(raw) {
  const text = cleanMarkdown(raw);
  const match = text.match(
    /^(?:Given|Dado)\s+(.+?)[,;]?\s+(?:When|Quando)\s+(.+?)[,;]?\s+(?:Then|Então|Entao)\s+(.+?)\.?$/i
  );

  if (!match) {
    return { given: '', when: '', then: text, raw: text };
  }

  return {
    given: match[1].trim(),
    when: match[2].trim(),
    then: match[3].trim(),
    raw: text
  };
}

function inferActor(given, when) {
  const text = `${given} ${when}`;
  const actors = [
    /Administrador(?:a)?(?:\s+da plataforma)?\s+ou\s+Gestor(?:a)?(?:\s+autorizad[oa])?/i,
    /Administrador(?:a)?(?:\s+da plataforma|\s+da organização)?/i,
    /Gestor(?:a)?(?:\s+autorizad[oa])?/i,
    /Product Owner/i,
    /Scrum Master/i,
    /responsável principal/i,
    /participante(?:\s+autorizad[oa])?/i,
    /usuário(?:a)?(?:\s+autorizad[oa])?/i,
    /solicitante/i,
    /membro/i,
    /visualizador/i,
    /externo/i
  ];

  for (const pattern of actors) {
    const match = text.match(pattern);
    if (match) return match[0];
  }

  return 'pessoa autorizada';
}

function storyTitle(scenario, specTitle) {
  const source = scenario.when || scenario.then || specTitle;
  const value = cleanMarkdown(source)
    .replace(/^(?:o|a|um|uma)\s+(?:usuário|pessoa|sistema)\s+/i, '')
    .replace(/^alguém\s+/i, '')
    .replace(/\.$/, '');
  const title = value.charAt(0).toUpperCase() + value.slice(1);
  return title.length > 105 ? `${title.slice(0, 102).trim()}…` : title;
}

function storyNarrative(actor, scenario) {
  const expected = scenario.then || scenario.raw;
  return `Como ${actor}, preciso que ${expected.charAt(0).toLowerCase()}${expected.slice(1)}.`;
}

function deriveUserStories(specs) {
  return specs.flatMap((spec) => {
    if (String(spec.status).toLowerCase() === 'superseded') return [];

    const scenarios = spec.sections
      .filter((section) => ACCEPTANCE_HEADING.test(section.heading))
      .flatMap((section) => scenarioBlocks(section.body).map((raw) => ({
        sourceHeading: section.heading,
        scenario: parseScenario(raw)
      })));

    return scenarios.map(({ sourceHeading, scenario }, index) => {
      const sequence = String(index + 1).padStart(3, '0');
      const actor = inferActor(scenario.given, scenario.when);
      return {
        id: `US-${spec.id.replace(/^SPEC-/, '')}-${sequence}`,
        specId: spec.id,
        specTitle: spec.title,
        specPath: spec.path,
        sourceHeading,
        title: storyTitle(scenario, spec.title),
        narrative: storyNarrative(actor, scenario),
        actor,
        scenario
      };
    });
  });
}

module.exports = {
  deriveUserStories,
  parseScenario,
  scenarioBlocks
};
