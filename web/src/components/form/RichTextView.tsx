import DOMPurify from 'dompurify'
import { resolveAttachmentUrls } from './resolveAttachmentUrls'
import type { WorkItemAttachment } from '../../api/types'

export function RichTextView({
  html,
  className,
  attachments,
}: {
  html: string
  className?: string
  attachments?: WorkItemAttachment[]
}) {
  // SEC-06: Resolve attachment URLs first, then sanitise the final HTML.
  // Running DOMPurify before URL resolution would leave the injected presigned
  // hrefs/srcs unsanitised, allowing a forged attachment record to inject arbitrary URLs.
  const resolved = resolveAttachmentUrls(html, attachments)
  const clean = DOMPurify.sanitize(resolved, {
    // Allow data-attachment-id attributes used by the rich-text renderer.
    ADD_ATTR: ['data-attachment-id'],
  })
  return <div className={`rich-text-content ${className ?? ''}`} dangerouslySetInnerHTML={{ __html: clean }} />
}
