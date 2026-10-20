import { mergeAttributes } from '@tiptap/core';
import { DOMSerializer, Schema } from '@tiptap/pm/model';
import { describe, expect, it } from 'vitest';

const importedAttributes = () => JSON.parse('{"__proto__":{"data-inherited-canary":"untrusted","onerror":"untrusted-handler"}}');

describe('atributos importados no editor', () => {
  it('não herda atributos de um __proto__ recebido por JSON', () => {
    const attributes = mergeAttributes({ alt: 'Imagem' }, importedAttributes());
    expect(attributes.alt).toBe('Imagem');
    expect(attributes).not.toHaveProperty('data-inherited-canary');
    expect(attributes).not.toHaveProperty('onerror');
  });

  it('não transforma atributos herdados em atributos executáveis na serialização DOM', () => {
    const attributes = mergeAttributes({ alt: 'Imagem' }, importedAttributes());
    const schema = new Schema({ nodes: {
      doc: { content: 'image' },
      image: { toDOM: () => ['img', attributes] },
      text: {},
    } });
    const documentNode = schema.node('doc', null, [schema.node('image')]);
    const fragment = DOMSerializer.fromSchema(schema).serializeFragment(documentNode.content) as DocumentFragment;
    const image = fragment.querySelector('img');
    expect(image).not.toBeNull();
    expect(image).toHaveAttribute('alt', 'Imagem');
    expect(image).not.toHaveAttribute('data-inherited-canary');
    expect(image).not.toHaveAttribute('onerror');
  });
});
