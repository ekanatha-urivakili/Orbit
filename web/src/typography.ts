import type { TypographySetting } from './api/types'

export function applyTypographySetting(setting: TypographySetting) {
  const root = document.documentElement.style
  root.setProperty('--font-left-family', setting.leftFontFamily)
  root.setProperty('--font-left-color', setting.leftFontColor)
  root.setProperty('--font-left-size', `${setting.leftFontSizePx}px`)
  root.setProperty('--font-middle-family', setting.middleFontFamily)
  root.setProperty('--font-middle-color', setting.middleFontColor)
  root.setProperty('--font-middle-size', `${setting.middleFontSizePx}px`)
  root.setProperty('--font-right-family', setting.rightFontFamily)
  root.setProperty('--font-right-color', setting.rightFontColor)
  root.setProperty('--font-right-size', `${setting.rightFontSizePx}px`)
  root.setProperty('--control-height', `${setting.controlHeightPx}px`)
  root.setProperty('--control-font-size', `${setting.controlFontSizePx}px`)
}
