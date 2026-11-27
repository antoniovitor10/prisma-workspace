import ts from 'typescript';

/**
 * Verificação de cobertura do loading padrão (HU-0001, CA-6; T-0003).
 *
 * Percorre os `.tsx` e acusa todo botão (`<button>` ou componente feito com `styled.button`)
 * que dispara uma ação no servidor e não usa o `ActionButton` (nem `as={ActionButton}`).
 *
 * Conta como ação no servidor um handler que, direto ou por uma função declarada no mesmo
 * arquivo, chama `mutate`, `mutateAsync` ou `refetch`, um método de cliente de API (`api.x()`,
 * `methodApi.x()`, `workflowApi.x()`...), uma função de serviço importada de `services`, ou o
 * `run` de uma ação do método (`useMethodAction`). Vale para `onClick` e para o botão de envio
 * de um formulário cujo `onSubmit` faz uma dessas chamadas.
 *
 * Limites conhecidos: handler recebido por prop (o arquivo não sabe o que ele faz) e
 * componentes de botão que não são `styled.button` não são verificados.
 */

export interface SourceFile { path: string; source: string }
export interface Violation { path: string; line: number; snippet: string; reason: string }

const SERVER_METHODS = new Set(['mutate', 'mutateAsync', 'refetch']);
const API_OBJECT = /^(api|[a-z]\w*Api)$/;
const ACTION_OBJECT = /action/i;
/** Métodos dos clientes de API que só leem ou gravam a sessão local, sem rede. */
const LOCAL_API_METHODS = new Set([
  'getToken', 'getUserId', 'setToken', 'clearToken', 'getOrganizationId', 'setOrganizationId', 'clearOrganizationId',
  'webhookUrl', 'externalAttachmentUrl',
]);

type Scope = Map<string, ts.Node>;

function isStyledButton(init: ts.Expression | undefined, buttons: Set<string>): boolean {
  if (!init) return false;
  let tag: ts.Expression = init;
  if (ts.isTaggedTemplateExpression(tag)) tag = tag.tag;
  // styled.button<Props>`...` chega como TaggedTemplate com typeArguments; .attrs(...) e .withConfig(...) também.
  while (ts.isCallExpression(tag) && ts.isPropertyAccessExpression(tag.expression)
    && ['attrs', 'withConfig'].includes(tag.expression.name.text)) tag = tag.expression.expression;
  if (ts.isPropertyAccessExpression(tag)) {
    return ts.isIdentifier(tag.expression) && tag.expression.text === 'styled' && tag.name.text === 'button';
  }
  if (ts.isCallExpression(tag) && ts.isIdentifier(tag.expression) && tag.expression.text === 'styled') {
    const [target] = tag.arguments;
    return Boolean(target && ts.isIdentifier(target) && buttons.has(target.text));
  }
  return false;
}

function resolveImport(from: string, specifier: string, known: Set<string>): string | undefined {
  if (!specifier.startsWith('.')) return undefined;
  const parts = from.split('/').slice(0, -1);
  for (const piece of specifier.split('/')) {
    if (piece === '.') continue;
    if (piece === '..') parts.pop(); else parts.push(piece);
  }
  const base = parts.join('/');
  return [`${base}.tsx`, `${base}.ts`, `${base}/index.tsx`, `${base}/index.ts`].find(candidate => known.has(candidate));
}

interface Declaration { name: string; init: ts.Expression | undefined; exported: boolean }

interface Parsed {
  path: string;
  file: ts.SourceFile;
  declarations: Declaration[];
  imports: Map<string, { module: string; name: string }>;
  serviceImports: Set<string>;
}

interface ResolvedButtons { all: Set<string>; exported: Set<string> }

function parse(path: string, source: string): Parsed {
  const file = ts.createSourceFile(path, source, ts.ScriptTarget.Latest, true, path.endsWith('.tsx') ? ts.ScriptKind.TSX : ts.ScriptKind.TS);
  const declarations: Declaration[] = [];
  const imports = new Map<string, { module: string; name: string }>();
  const serviceImports = new Set<string>();
  for (const statement of file.statements) {
    if (ts.isImportDeclaration(statement) && ts.isStringLiteral(statement.moduleSpecifier)) {
      const module = statement.moduleSpecifier.text;
      const named = statement.importClause?.namedBindings;
      if (named && ts.isNamedImports(named)) {
        for (const element of named.elements) {
          imports.set(element.name.text, { module, name: (element.propertyName ?? element.name).text });
          if (/(^|\/)services(\/|$)/.test(module)) serviceImports.add(element.name.text);
        }
      }
    }
    if (ts.isVariableStatement(statement)) {
      const exported = statement.modifiers?.some(modifier => modifier.kind === ts.SyntaxKind.ExportKeyword);
      for (const declaration of statement.declarationList.declarations) {
        if (ts.isIdentifier(declaration.name)) {
          declarations.push({ name: declaration.name.text, init: declaration.initializer, exported: Boolean(exported) });
        }
      }
    }
  }
  return { path, file, declarations, imports, serviceImports };
}

/**
 * Botões do arquivo: `styled.button` local, botões importados de outro arquivo e
 * `styled(Botao)` sobre qualquer um deles, inclusive em cadeia entre arquivos.
 */
function resolveButtons(entry: Parsed, byPath: Map<string, Parsed>, known: Set<string>,
  memo: Map<string, ResolvedButtons>, visiting = new Set<string>()): ResolvedButtons {
  const cached = memo.get(entry.path);
  if (cached) return cached;
  if (visiting.has(entry.path)) return { all: new Set(), exported: new Set() };
  visiting.add(entry.path);
  const all = new Set<string>();
  for (const [local, { module, name }] of entry.imports) {
    const target = resolveImport(entry.path, module, known);
    const targetEntry = target ? byPath.get(target) : undefined;
    if (targetEntry && resolveButtons(targetEntry, byPath, known, memo, visiting).exported.has(name)) all.add(local);
  }
  for (let changed = true; changed;) {
    changed = false;
    for (const declaration of entry.declarations) {
      if (!all.has(declaration.name) && isStyledButton(declaration.init, all)) { all.add(declaration.name); changed = true; }
    }
  }
  const resolved = { all, exported: new Set(entry.declarations.filter(d => d.exported && all.has(d.name)).map(d => d.name)) };
  memo.set(entry.path, resolved);
  visiting.delete(entry.path);
  return resolved;
}

function collectFunctions(file: ts.SourceFile): Scope {
  const scope: Scope = new Map();
  const visit = (node: ts.Node) => {
    if (ts.isFunctionDeclaration(node) && node.name && node.body) scope.set(node.name.text, node.body);
    if (ts.isVariableDeclaration(node) && ts.isIdentifier(node.name) && node.initializer) {
      let init: ts.Expression = node.initializer;
      // useCallback(fn, deps)
      if (ts.isCallExpression(init) && ts.isIdentifier(init.expression) && init.expression.text === 'useCallback' && init.arguments[0]) init = init.arguments[0];
      if (ts.isArrowFunction(init) || ts.isFunctionExpression(init)) scope.set(node.name.text, init.body);
    }
    ts.forEachChild(node, visit);
  };
  visit(file);
  return scope;
}

function callsServer(node: ts.Node, scope: Scope, services: Set<string>, seen = new Set<ts.Node>()): boolean {
  if (seen.has(node)) return false;
  seen.add(node);
  let found = false;
  const visit = (current: ts.Node) => {
    if (found) return;
    if (ts.isCallExpression(current)) {
      const callee = current.expression;
      if (ts.isPropertyAccessExpression(callee)) {
        const method = callee.name.text;
        const owner = callee.expression;
        if (SERVER_METHODS.has(method)) { found = true; return; }
        if (ts.isIdentifier(owner) && API_OBJECT.test(owner.text) && !LOCAL_API_METHODS.has(method)) { found = true; return; }
        if (method === 'run' && ts.isIdentifier(owner) && ACTION_OBJECT.test(owner.text)) { found = true; return; }
      }
      if (ts.isIdentifier(callee) && services.has(callee.text)) { found = true; return; }
    }
    if (ts.isIdentifier(current) && !ts.isPropertyAccessExpression(current.parent)) {
      const body = scope.get(current.text);
      if (body && callsServer(body, scope, services, seen)) { found = true; return; }
    }
    ts.forEachChild(current, visit);
  };
  visit(node);
  return found;
}

function attribute(opening: ts.JsxOpeningLikeElement, name: string): ts.JsxAttribute | undefined {
  return opening.attributes.properties.find((property): property is ts.JsxAttribute =>
    ts.isJsxAttribute(property) && ts.isIdentifier(property.name) && property.name.text === name);
}

function attributeValue(attr: ts.JsxAttribute | undefined): ts.Node | undefined {
  if (!attr?.initializer) return undefined;
  if (ts.isJsxExpression(attr.initializer)) return attr.initializer.expression;
  return attr.initializer;
}

function stringAttribute(opening: ts.JsxOpeningLikeElement, name: string): string | undefined {
  const value = attributeValue(attribute(opening, name));
  return value && ts.isStringLiteral(value) ? value.text : undefined;
}

function tagName(opening: ts.JsxOpeningLikeElement): string {
  return opening.tagName.getText();
}

function usesActionButton(opening: ts.JsxOpeningLikeElement): boolean {
  if (tagName(opening) === 'ActionButton') return true;
  const as = attributeValue(attribute(opening, 'as'));
  return Boolean(as && ts.isIdentifier(as) && as.text === 'ActionButton');
}

/** `<Button as="a">` ou `as={Link}`: o estilo é de botão, mas o elemento é outro (link). */
function rendersOtherElement(opening: ts.JsxOpeningLikeElement): boolean {
  return Boolean(attribute(opening, 'as')) && !usesActionButton(opening);
}

function enclosingSubmitHandler(node: ts.Node): ts.Node | undefined {
  for (let current = node.parent; current; current = current.parent) {
    if (ts.isJsxElement(current)) {
      const handler = attributeValue(attribute(current.openingElement, 'onSubmit'));
      if (handler) return handler;
    }
  }
  return undefined;
}

export function findActionButtonViolations(files: SourceFile[]): Violation[] {
  const parsed = files.map(({ path, source }) => parse(path, source));
  const byPath = new Map(parsed.map(entry => [entry.path, entry]));
  const known = new Set(byPath.keys());
  const violations: Violation[] = [];
  const memo = new Map<string, ResolvedButtons>();

  for (const entry of parsed) {
    if (!entry.path.endsWith('.tsx')) continue;
    const buttons = resolveButtons(entry, byPath, known, memo).all;
    const scope = collectFunctions(entry.file);

    const visit = (node: ts.Node) => {
      if (ts.isJsxOpeningElement(node) || ts.isJsxSelfClosingElement(node)) {
        const name = tagName(node);
        if ((name === 'button' || buttons.has(name)) && !usesActionButton(node) && !rendersOtherElement(node)) {
          const onClick = attributeValue(attribute(node, 'onClick'));
          const type = stringAttribute(node, 'type');
          let reason = '';
          if (onClick && callsServer(onClick, scope, entry.serviceImports)) reason = 'onClick chama o servidor';
          else if (type === 'submit' || (type === undefined && !onClick)) {
            const submit = enclosingSubmitHandler(node);
            if (submit && callsServer(submit, scope, entry.serviceImports)) reason = 'envia um formulário que chama o servidor';
          }
          if (reason) {
            const { line } = entry.file.getLineAndCharacterOfPosition(node.getStart());
            const element = ts.isJsxOpeningElement(node) ? node.parent : node;
            violations.push({
              path: entry.path,
              line: line + 1,
              snippet: element.getText().replace(/\s+/g, ' ').slice(0, 140),
              reason,
            });
          }
        }
      }
      ts.forEachChild(node, visit);
    };
    visit(entry.file);
  }
  return violations;
}
