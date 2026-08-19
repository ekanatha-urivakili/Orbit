import { Node, mergeAttributes } from '@tiptap/core'

export interface TicketLinkOptions {
  HTMLAttributes: Record<string, unknown>
  /** Base URL used when navigating to the ticket on click */
  browseBase: string
}

declare module '@tiptap/core' {
  interface Commands<ReturnType> {
    ticketLink: {
      insertTicketLink: (attrs: {
        ticketKey: string
        ticketId: string
        summary: string
        status: string
      }) => ReturnType
    }
  }
}

export const TicketLinkChip = Node.create<TicketLinkOptions>({
  name: 'ticketLink',

  group: 'inline',
  inline: true,
  selectable: false,
  atom: true,

  addOptions() {
    return {
      HTMLAttributes: {},
      browseBase: '/browse',
    }
  },

  addAttributes() {
    return {
      ticketKey: {
        default: '',
        parseHTML: (el) => el.getAttribute('data-ticket-key'),
        renderHTML: (attrs) => ({ 'data-ticket-key': attrs.ticketKey }),
      },
      ticketId: {
        default: '',
        parseHTML: (el) => el.getAttribute('data-ticket-id'),
        renderHTML: (attrs) => ({ 'data-ticket-id': attrs.ticketId }),
      },
      summary: {
        default: '',
        parseHTML: (el) => el.getAttribute('data-summary'),
        renderHTML: (attrs) => ({ 'data-summary': attrs.summary }),
      },
      status: {
        default: '',
        parseHTML: (el) => el.getAttribute('data-status'),
        renderHTML: (attrs) => ({ 'data-status': attrs.status }),
      },
    }
  },

  parseHTML() {
    return [{ tag: 'a[data-ticket-link]' }]
  },

  renderHTML({ HTMLAttributes }) {
    const key = (HTMLAttributes['data-ticket-key'] as string) ?? ''
    const summary = (HTMLAttributes['data-summary'] as string) ?? ''
    const status = (HTMLAttributes['data-status'] as string) ?? ''

    // Status → short label + colour class
    const statusLabels: Record<string, string> = {
      Backlog: 'Backlog',
      Selected: 'To Do',
      InProgress: 'In Progress',
      InReview: 'In Review',
      Done: 'Done',
      Blocked: 'Blocked',
    }
    const statusLabel = statusLabels[status] ?? status

    const href = `${this.options.browseBase}/${key}`

    return [
      'a',
      mergeAttributes(this.options.HTMLAttributes, HTMLAttributes, {
        'data-ticket-link': '',
        class: 'ticket-chip',
        href,
        target: '_self',
        contenteditable: 'false',
        title: `${key}: ${summary}`,
      }),
      // icon + key + summary + status
      ['span', { class: 'ticket-chip__icon' }, '🎫'],
      ['span', { class: 'ticket-chip__key' }, key],
      ['span', { class: 'ticket-chip__summary' }, summary ? `: ${summary}` : ''],
      ['span', { class: `ticket-chip__status ticket-chip__status--${status.toLowerCase()}` }, statusLabel],
    ]
  },

  addCommands() {
    return {
      insertTicketLink:
        (attrs) =>
        ({ chain }) => {
          return chain()
            .insertContent({ type: this.name, attrs })
            .insertContent(' ')
            .run()
        },
    }
  },
})
