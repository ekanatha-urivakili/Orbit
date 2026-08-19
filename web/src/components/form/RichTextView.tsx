import { type MouseEvent } from 'react'
import DOMPurify from 'dompurify'
import { resolveAttachmentUrls } from './resolveAttachmentUrls'
import { linkifyTicketKeys } from './linkifyTicketKeys'
import type { WorkItemAttachment } from '../../api/types'

export function RichTextView({
  html,
  className,
  attachments,
  onOpenTicket,
}: {
  html: string
  className?: string
  attachments?: WorkItemAttachment[]
  onOpenTicket?: (key: string) => void
}) {
  // SEC-06: Resolve attachment URLs first, then linkify tickets, then sanitise the final HTML.
  const resolved = resolveAttachmentUrls(html, attachments)
  const linkified = linkifyTicketKeys(resolved)
  const clean = DOMPurify.sanitize(linkified, {
    // Allow data attributes and tags used by the rich-text renderer, tables, task lists, and ticket links.
    ADD_TAGS: ['table', 'thead', 'tbody', 'tr', 'th', 'td', 'colgroup', 'col', 'input', 'label', 'span'],
    ADD_ATTR: [
      'data-attachment-id',
      'data-ticket-key',
      'data-ticket-id',
      'data-ticket-link',
      'data-summary',
      'data-status',
      'data-mention-id',
      'data-type',
      'data-checked',
      'checked',
      'disabled',
      'type',
      'target',
      'style',
      'colspan',
      'rowspan',
      'colwidth',
      'class',
      'href',
      'title',
      'contenteditable',
    ],
  })

  const handleClick = (event: MouseEvent<HTMLDivElement>) => {
    const target = (event.target as HTMLElement).closest('[data-ticket-key]') as HTMLElement | null
    if (target) {
      const key = target.getAttribute('data-ticket-key')
      if (key) {
        event.preventDefault()
        if (onOpenTicket) {
          onOpenTicket(key)
        } else {
          window.dispatchEvent(new CustomEvent('orbit:open-ticket', { detail: { key } }))
        }
      }
    }
  }

  return (
    <div
      className={`rich-text-content ${className ?? ''}`}
      onClick={handleClick}
      dangerouslySetInnerHTML={{ __html: clean }}
    />
  )
}
