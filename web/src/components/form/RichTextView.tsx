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
  const clean = resolveAttachmentUrls(DOMPurify.sanitize(html), attachments)
  return <div className={`rich-text-content ${className ?? ''}`} dangerouslySetInnerHTML={{ __html: clean }} />
}
