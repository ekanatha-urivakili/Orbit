// Jira's authentic 21-swatch color palette (3 rows x 7 cols)
export const JIRA_COLOR_PALETTE = [
  // Row 1: Deep / Dark shades
  { label: 'Dark Slate', hex: '#172B4D' },
  { label: 'Navy Blue', hex: '#0052CC' },
  { label: 'Teal Forest', hex: '#00875A' },
  { label: 'Deep Cyan', hex: '#00A3BF' },
  { label: 'Amber Orange', hex: '#FF8B00' },
  { label: 'Coral Red', hex: '#DE350B' },
  { label: 'Royal Purple', hex: '#5243AA' },
  // Row 2: Vibrant / Medium shades
  { label: 'Slate Grey', hex: '#42526E' },
  { label: 'Bright Blue', hex: '#0065FF' },
  { label: 'Mint Green', hex: '#36B37E' },
  { label: 'Sky Cyan', hex: '#00B8D9' },
  { label: 'Golden Yellow', hex: '#FFAB00' },
  { label: 'Flame Red', hex: '#FF5630' },
  { label: 'Amethyst Purple', hex: '#6554C0' },
  // Row 3: Pastel / Light shades
  { label: 'Light Grey', hex: '#DFE1E6' },
  { label: 'Pastel Blue', hex: '#B3D4FF' },
  { label: 'Pastel Green', hex: '#ABF5D1' },
  { label: 'Pastel Cyan', hex: '#B3F5FF' },
  { label: 'Pastel Yellow', hex: '#FFE380' },
  { label: 'Pastel Coral', hex: '#FFBDAD' },
  { label: 'Pastel Lavender', hex: '#EAE6FF' },
]

export const TEXT_STYLES = [
  { id: 'p', label: 'Normal text', shortcut: '⌘⌥0', tag: 'T', size: '14px', weight: '400' },
  { id: 'small', label: 'Small text', shortcut: '⌘⌥7', tag: 'Ts', size: '12px', weight: '400' },
  { id: 'h1', label: 'Heading 1', shortcut: '⌘⌥1', tag: 'H₁', size: '18px', weight: '700', level: 1 },
  { id: 'h2', label: 'Heading 2', shortcut: '⌘⌥2', tag: 'H₂', size: '16px', weight: '650', level: 2 },
  { id: 'h3', label: 'Heading 3', shortcut: '⌘⌥3', tag: 'H₃', size: '15px', weight: '650', level: 3 },
  { id: 'h4', label: 'Heading 4', shortcut: '⌘⌥4', tag: 'H₄', size: '14px', weight: '600', level: 4 },
  { id: 'h5', label: 'Heading 5', shortcut: '⌘⌥5', tag: 'H₅', size: '13px', weight: '600', level: 5 },
  { id: 'h6', label: 'Heading 6', shortcut: '⌘⌥6', tag: 'H₆', size: '12px', weight: '600', level: 6 },
]

export function isRichTextEmpty(html: string): boolean {
  return html.replace(/<[^>]*>/g, '').trim().length === 0
}
