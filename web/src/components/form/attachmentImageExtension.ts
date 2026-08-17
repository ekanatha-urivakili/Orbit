import Image from '@tiptap/extension-image'

export const AttachmentImage = Image.extend({
  addAttributes() {
    return {
      ...this.parent?.(),
      attachmentId: {
        default: null,
        parseHTML: (element: HTMLElement) => element.getAttribute('data-attachment-id'),
        renderHTML: (attributes: { attachmentId?: string | null }) =>
          attributes.attachmentId ? { 'data-attachment-id': attributes.attachmentId } : {},
      },
    }
  },
})
