'use strict';

// Constantes canonicas derivadas de:
// - workflows/feature.yaml (processNodes, processEdges, humanGates)
// - tools/agent-loop/lib/constants.js (engineStates, engineEdges)
// - profiles/runrun-loop.yaml (maxAttempts, stopOnFirstGateFailure)
// - specs/*.md (canonicalSpecs, templateNotSpec)
// - tools/agent-loop/test/*.test.js (loopEngineTests)
module.exports = {
  processNodes: 16,
  processEdges: 21,
  engineStates: 11,
  engineEdges: 21,
  humanGates: [
    'G-SPEC',
    'G-MIGRATION',
    'G-WORKFLOW',
    'G-HISTORY',
    'G-SCOPE',
    'G-DEPLOY',
    'G-COMPLETION'
  ],
  maxAttempts: 3,
  stopOnFirstGateFailure: true,
  canonicalSpecs: 39,
  canonicalTasks: 41,
  templateNotSpec: true,
  loopEngineTests: 47
};
