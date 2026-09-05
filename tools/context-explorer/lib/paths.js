'use strict';

const path = require('path');
const fs = require('fs');

// __dirname = .../tools/context-explorer/lib
// ..        = .../tools/context-explorer
// ../..     = .../tools
// ../../..  = repo root
const REPO_ROOT = path.resolve(__dirname, '..', '..', '..');

function repoPath(...parts) {
  return path.join(REPO_ROOT, ...parts);
}

function fileExists(relativePath) {
  return fs.existsSync(repoPath(relativePath));
}

module.exports = {
  REPO_ROOT,
  repoPath,
  fileExists
};
