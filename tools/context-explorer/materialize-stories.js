'use strict';

const fs = require('fs');
const path = require('path');
const { readSpecs } = require('./lib/read-artifacts');
const { deriveUserStories, parseScenario, scenarioBlocks } = require('./lib/derive-user-stories');
const { repoPath } = require('./lib/paths');

const specs = readSpecs().canonicalSpecs;
const storiesDirectory = repoPath('stories');

function section(body, heading) {
  const match = body.match(new RegExp(`## ${heading}\\s*\\r?\\n([\\s\\S]*?)(?=\\r?\\n## |$)`, 'i'));
  return match ? match[1].trim() : '';
}

function readStandaloneStories() {
  return fs.readdirSync(storiesDirectory)
    .filter((name) => /^US-[A-Z0-9-]+\.md$/i.test(name))
    .map((name) => {
      const body = fs.readFileSync(path.join(storiesDirectory, name), 'utf8');
      const header = body.match(/^#\s+(US-[A-Z0-9-]+):\s*(.+)$/im);
      const spec = body.match(/\*\*Spec vinculada:\*\*\s*`?(SPEC-[A-Z0-9-]+)`?/i);
      if (!header || !spec) return null;

      const history = section(body, 'História')
        .replace(/\*\*/g, '')
        .replace(/\s+/g, ' ')
        .trim();
      const scenariosBody = section(body, 'Cenários esperados') || section(body, 'Cenário principal');
      const firstScenario = scenarioBlocks(scenariosBody)[0] || 'A intenção humana registrada deve ser preservada.';
      const scenario = parseScenario(firstScenario);
      const actorMatch = history.match(/^Como\s+(.+?)(?:\s+Quero|,\s*quero)/i);

      return {
        id: header[1],
        specId: spec[1],
        specTitle: specs.find((item) => item.id === spec[1])?.title || spec[1],
        specPath: specs.find((item) => item.id === spec[1])?.path || '',
        sourceHeading: 'História',
        title: header[2].trim(),
        narrative: history.startsWith('Como ') ? history : `Como pessoa autorizada, preciso que ${history}`,
        actor: actorMatch ? actorMatch[1].trim() : 'pessoa autorizada',
        scenario,
        status: 'active',
        origin: 'human_authored'
      };
    })
    .filter(Boolean);
}

const standaloneStories = readStandaloneStories();
const standaloneSpecIds = new Set(standaloneStories.map((story) => story.specId));
const derivedStories = deriveUserStories(specs)
  .filter((story) => !standaloneSpecIds.has(story.specId))
  .map((story) => ({
  ...story,
  status: 'active',
  origin: 'migrated_from_approved_spec'
}));
const stories = [...standaloneStories, ...derivedStories];
const output = {
  version: 1,
  generatedAt: new Date().toISOString(),
  migrationNote: 'Baseline materializado dos critérios de aceite aprovados antes da adoção do fluxo story-first da D67.',
  stories
};
const outputPath = repoPath('stories', 'catalog.json');
fs.mkdirSync(path.dirname(outputPath), { recursive: true });
fs.writeFileSync(outputPath, `${JSON.stringify(output, null, 2)}\n`, 'utf8');
console.log(`${stories.length} histórias materializadas em ${outputPath}`);
