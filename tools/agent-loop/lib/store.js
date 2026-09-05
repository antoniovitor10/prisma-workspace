'use strict';

const fs = require('node:fs');
const path = require('node:path');

const DEFAULT_TASKS_DIR = path.join(process.cwd(), '.agent-state', 'tasks');

function resolveTasksDir(baseDir) {
  return baseDir ? path.resolve(baseDir) : DEFAULT_TASKS_DIR;
}

function ensureDir(dir) {
  fs.mkdirSync(dir, { recursive: true });
}

function taskFilePath(tasksDir, taskId) {
  return path.join(resolveTasksDir(tasksDir), `${taskId}.yaml`);
}

/**
 * Persists the envelope as valid JSON inside a `.yaml`-named file
 * (per project convention: content is JSON, extension stays `.yaml`).
 */
function saveTask(envelope, tasksDir) {
  const dir = resolveTasksDir(tasksDir);
  ensureDir(dir);
  const filePath = taskFilePath(dir, envelope.task_id);
  fs.writeFileSync(filePath, JSON.stringify(envelope, null, 2) + '\n', 'utf8');
  return filePath;
}

function loadTask(taskId, tasksDir) {
  const filePath = taskFilePath(tasksDir, taskId);
  if (!fs.existsSync(filePath)) {
    return null;
  }
  const raw = fs.readFileSync(filePath, 'utf8');
  return JSON.parse(raw);
}

function taskExists(taskId, tasksDir) {
  return fs.existsSync(taskFilePath(tasksDir, taskId));
}

function listTasks(tasksDir) {
  const dir = resolveTasksDir(tasksDir);
  if (!fs.existsSync(dir)) {
    return [];
  }
  return fs
    .readdirSync(dir)
    .filter((name) => name.endsWith('.yaml'))
    .map((name) => name.slice(0, -'.yaml'.length));
}

function deleteTask(taskId, tasksDir) {
  const filePath = taskFilePath(tasksDir, taskId);
  if (fs.existsSync(filePath)) {
    fs.unlinkSync(filePath);
    return true;
  }
  return false;
}

module.exports = {
  DEFAULT_TASKS_DIR,
  resolveTasksDir,
  taskFilePath,
  saveTask,
  loadTask,
  taskExists,
  listTasks,
  deleteTask
};
