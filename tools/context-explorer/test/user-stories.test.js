'use strict';

const test = require('node:test');
const assert = require('node:assert/strict');

const { buildModel } = require('../lib/build-model');
const { parseScenario } = require('../lib/derive-user-stories');

test('parseScenario reconhece Dado/Quando/Então em português', () => {
  const scenario = parseScenario(
    '**Dado** um usuário autorizado, **quando** move uma tarefa, **então** a nova posição fica salva.'
  );
  assert.equal(scenario.given, 'um usuário autorizado');
  assert.equal(scenario.when, 'move uma tarefa');
  assert.equal(scenario.then, 'a nova posição fica salva');
});

test('toda spec ativa possui histórias rastreáveis aos critérios de aceite', () => {
  const model = buildModel();
  const activeSpecs = model.specs.filter((spec) => spec.status.toLowerCase() !== 'superseded');
  assert.ok(model.userStories.length > activeSpecs.length);

  for (const spec of activeSpecs) {
    const stories = model.userStories.filter((story) => story.specId === spec.id);
    assert.ok(stories.length > 0, `${spec.id} deveria possuir ao menos uma história`);
    for (const story of stories) {
      assert.match(story.id, /^US-[A-Z0-9-]+-\d{3}$/);
      assert.equal(story.specPath, spec.path);
      assert.ok(story.narrative.startsWith('Como '));
      assert.ok(story.scenario.then);
    }
  }
});
