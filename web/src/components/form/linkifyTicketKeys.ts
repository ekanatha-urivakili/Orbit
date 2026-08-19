export function linkifyTicketKeys(html: string): string {
  if (!html) return ''
  // 1. Linkify full ticket URLs: https://.../browse/KEY or /browse/KEY
  const processed = html.replace(
    /(?:https?:\/\/[^\s<"']+)?\/browse\/([A-Za-z0-9]{2,10}-\d+)/gi,
    '<a href="/browse/$1" data-ticket-key="$1" class="ticket-link font-semibold text-[#0052cc] hover:underline bg-[#deebff]/50 dark:bg-blue-900/40 px-1 py-0.5 rounded text-[13px]">$1</a>'
  )

  // 2. Linkify standalone ticket keys (e.g. ORB-12, TST-1) outside of existing HTML tags/attributes
  // Split by HTML tags to only transform text nodes
  const parts = processed.split(/(<[^>]+>)/g)
  let insideAnchor = false

  const linkifiedParts = parts.map((part) => {
    if (part.startsWith('<')) {
      if (/^<a\b/i.test(part)) insideAnchor = true
      else if (/^<\/a>/i.test(part)) insideAnchor = false
      return part
    }
    if (insideAnchor) return part

    // Replace standalone ticket keys: e.g. TST-123, SCRUM-2
    return part.replace(
      /\b([A-Z][A-Z0-9]{1,9}-\d+)\b/g,
      '<a href="/browse/$1" data-ticket-key="$1" class="ticket-link font-semibold text-[#0052cc] dark:text-blue-400 hover:underline bg-[#deebff]/50 dark:bg-blue-900/40 px-1 py-0.5 rounded text-[13px]">$1</a>'
    )
  })

  return linkifiedParts.join('')
}
