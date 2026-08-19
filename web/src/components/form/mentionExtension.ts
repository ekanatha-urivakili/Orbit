import { Node, mergeAttributes } from '@tiptap/core'

export interface MentionOptions {
  HTMLAttributes: Record<string, unknown>
}

declare module '@tiptap/core' {
  interface Commands<ReturnType> {
    mention: {
      insertMention: (attrs: { memberId: string; label: string }) => ReturnType
    }
  }
}

export const MentionChip = Node.create<MentionOptions>({
  name: 'mention',

  group: 'inline',
  inline: true,
  selectable: false,
  atom: true,

  addOptions() {
    return { HTMLAttributes: {} }
  },

  addAttributes() {
    return {
      memberId: {
        default: null,
        parseHTML: (el) => el.getAttribute('data-member-id'),
        renderHTML: (attrs) => ({ 'data-member-id': attrs.memberId }),
      },
      label: {
        default: '',
        parseHTML: (el) => el.getAttribute('data-label'),
        renderHTML: (attrs) => ({ 'data-label': attrs.label }),
      },
    }
  },

  parseHTML() {
    return [{ tag: 'span[data-mention]' }]
  },

  renderHTML({ HTMLAttributes }) {
    return [
      'span',
      mergeAttributes(this.options.HTMLAttributes, HTMLAttributes, {
        'data-mention': '',
        class: 'mention-chip',
        contenteditable: 'false',
      }),
      `@${HTMLAttributes['data-label'] ?? ''}`,
    ]
  },

  addCommands() {
    return {
      insertMention:
        (attrs) =>
        ({ chain }) => {
          return chain()
            .insertContent({
              type: this.name,
              attrs,
            })
            .insertContent(' ')
            .run()
        },
    }
  },
})
