'use strict';

const fs = require('fs');
const path = require('path');

const { buildModel } = require('./lib/build-model');

const model = buildModel();
const outputPath = path.join(__dirname, 'model.json');

fs.writeFileSync(outputPath, JSON.stringify(model, null, 2), 'utf8');

console.log(`model.json gerado em: ${outputPath}`);
console.log(`generatedAt: ${model.generatedAt}`);
console.log('--- artifactsSummary ---');
console.log(JSON.stringify(model.artifactsSummary, null, 2));
console.log('--- selections (counts) ---');
console.log(
  JSON.stringify(
    {
      specs: model.selections.specs.length,
      tasks: model.selections.tasks.length,
      domains: model.selections.domains.length
    },
    null,
    2
  )
);
console.log('--- gaps (counts by type) ---');
const gapCounts = model.gaps.reduce((acc, gap) => {
  acc[gap.type] = (acc[gap.type] || 0) + 1;
  return acc;
}, {});
console.log(JSON.stringify(gapCounts, null, 2));
console.log(`Total de gaps: ${model.gaps.length}`);
