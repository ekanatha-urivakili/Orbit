import { Node, mergeAttributes } from '@tiptap/core'

function formatFileSize(sizeBytes: number): string {
  if (sizeBytes < 1024) return `${sizeBytes} B`
  if (sizeBytes < 1024 * 1024) return `${(sizeBytes / 1024).toFixed(1)} KB`
  return `${(sizeBytes / (1024 * 1024)).toFixed(1)} MB`
}

export interface FileAttachmentAttrs {
  attachmentId: string
  fileName: string
  contentType: string
  sizeBytes: number
  url: string
}

declare module '@tiptap/core' {
  interface Commands<ReturnType> {
    fileAttachment: {
      insertFileAttachment: (attrs: FileAttachmentAttrs) => ReturnType
    }
  }
}

export const FileAttachment = Node.create({
  name: 'fileAttachment',
  group: 'block',
  atom: true,
  draggable: true,

  addAttributes() {
    return {
      attachmentId: { default: null, parseHTML: (el) => el.getAttribute('data-attachment-id') },
      fileName: { default: '', parseHTML: (el) => el.getAttribute('data-file-name') },
      contentType: { default: '', parseHTML: (el) => el.getAttribute('data-content-type') },
      sizeBytes: { default: 0, parseHTML: (el) => Number(el.getAttribute('data-size-bytes') ?? 0) },
      url: { default: '', parseHTML: (el) => el.getAttribute('data-url') },
    }
  },

  parseHTML() {
    return [{ tag: 'div[data-attachment-file]' }]
  },

  renderHTML({ HTMLAttributes, node }) {
    const { attachmentId, fileName, contentType, sizeBytes, url } = node.attrs as FileAttachmentAttrs
    return [
      'div',
      mergeAttributes(HTMLAttributes, {
        'data-attachment-file': '',
        'data-attachment-id': attachmentId,
        'data-file-name': fileName,
        'data-content-type': contentType,
        'data-size-bytes': String(sizeBytes),
        'data-url': url,
        class: 'rt-file-card',
      }),
      [
        'a',
        { href: url, target: '_blank', rel: 'noreferrer', class: 'rt-file-card-link' },
        ['span', { class: 'rt-file-card-name' }, fileName],
        ['span', { class: 'rt-file-card-size' }, formatFileSize(sizeBytes)],
      ],
    ]
  },

  addCommands() {
    return {
      insertFileAttachment:
        (attrs: FileAttachmentAttrs) =>
        ({ chain }) =>
          chain().insertContent({ type: this.name, attrs }).run(),
    }
  },
})
