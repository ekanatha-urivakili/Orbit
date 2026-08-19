import type { WorkItemAttachment } from '../../api/types'

export function resolveAttachmentUrls(html: string, attachments?: WorkItemAttachment[]): string {
  if (!html || !attachments || attachments.length === 0) return html
  const byId = new Map(attachments.map((attachment) => [attachment.id, attachment]))
  const doc = new DOMParser().parseFromString(html, 'text/html')
  doc.querySelectorAll<HTMLElement>('[data-attachment-id]').forEach((element) => {
    const attachment = byId.get(element.getAttribute('data-attachment-id') ?? '')
    if (!attachment) return
    if (element instanceof HTMLImageElement) element.src = attachment.downloadUrl
    const link = element.matches('a') ? element : element.querySelector('a')
    if (link instanceof HTMLAnchorElement) link.href = attachment.downloadUrl
  })
  return doc.body.innerHTML
}
