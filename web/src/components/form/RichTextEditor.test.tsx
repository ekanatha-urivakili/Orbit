import { describe, it, expect, vi } from 'vitest'
import { render, screen, fireEvent } from '@testing-library/react'
import { RichTextEditor, isRichTextEmpty, JIRA_COLOR_PALETTE, TEXT_STYLES } from './RichTextEditor'
import { RichTextView, linkifyTicketKeys } from './RichTextView'

describe('RichTextEditor utilities & constants', () => {
  it('identifies empty html correctly', () => {
    expect(isRichTextEmpty('')).toBe(true)
    expect(isRichTextEmpty('<p></p>')).toBe(true)
    expect(isRichTextEmpty('<p><br></p>')).toBe(true)
    expect(isRichTextEmpty('<p>   </p>')).toBe(true)
    expect(isRichTextEmpty('<p>Hello world</p>')).toBe(false)
  })

  it('contains Jira authentic 21-swatch color palette', () => {
    expect(JIRA_COLOR_PALETTE.length).toBe(21)
    expect(JIRA_COLOR_PALETTE[0].hex).toBe('#172B4D')
    expect(JIRA_COLOR_PALETTE.some((c) => c.hex === '#0052CC')).toBe(true)
    expect(JIRA_COLOR_PALETTE.some((c) => c.hex === '#DE350B')).toBe(true)
  })

  it('contains Jira text styles from Normal to Heading 6 and Small text', () => {
    expect(TEXT_STYLES.length).toBe(8)
    expect(TEXT_STYLES[0].label).toBe('Normal text')
    expect(TEXT_STYLES[1].label).toBe('Small text')
    expect(TEXT_STYLES[2].label).toBe('Heading 1')
    expect(TEXT_STYLES[7].label).toBe('Heading 6')
  })
})

describe('RichTextView with Jira elements', () => {
  it('renders tables, headers, and cells safely', () => {
    const tableHtml = `
      <table class="jira-table">
        <thead>
          <tr><th>Col 1</th><th>Col 2</th></tr>
        </thead>
        <tbody>
          <tr><td>Cell A</td><td>Cell B</td></tr>
        </tbody>
      </table>
    `
    const { container } = render(<RichTextView html={tableHtml} />)
    expect(container.querySelector('table')).toBeTruthy()
    expect(container.querySelector('th')?.textContent).toBe('Col 1')
    expect(container.querySelector('td')?.textContent).toBe('Cell A')
  })

  it('renders task list items safely with checkboxes', () => {
    const taskListHtml = `
      <ul data-type="taskList">
        <li data-type="taskItem" data-checked="true">
          <label><input type="checkbox" checked disabled /></label>
          <div>Done item</div>
        </li>
        <li data-type="taskItem" data-checked="false">
          <label><input type="checkbox" disabled /></label>
          <div>Pending item</div>
        </li>
      </ul>
    `
    const { container } = render(<RichTextView html={taskListHtml} />)
    const inputs = container.querySelectorAll('input[type="checkbox"]')
    expect(inputs.length).toBe(2)
    expect((inputs[0] as HTMLInputElement).checked).toBe(true)
    expect((inputs[1] as HTMLInputElement).checked).toBe(false)
  })

  it('preserves text styles and color spans', () => {
    const styledHtml = '<p><span style="color: rgb(0, 82, 204);">Blue text</span></p>'
    const { container } = render(<RichTextView html={styledHtml} />)
    const span = container.querySelector('span')
    expect(span).toBeTruthy()
    expect(span?.textContent).toBe('Blue text')
  })

  it('linkifies ticket keys within html', () => {
    const text = linkifyTicketKeys('<p>Please check SCRUM-12 and ORB-99</p>')
    expect(text).toContain('data-ticket-key="SCRUM-12"')
    expect(text).toContain('data-ticket-key="ORB-99"')
  })
})

describe('RichTextEditor component', () => {
  it('renders Jira-styled toolbar buttons and opens popovers', () => {
    const handleChange = vi.fn()
    render(<RichTextEditor value="<p>Test</p>" onChange={handleChange} />)

    // Check toolbar buttons
    expect(screen.getByTitle('Text styles')).toBeDefined()
    expect(screen.getByTitle('Bold (⌘B)')).toBeDefined()
    expect(screen.getByTitle(/Text colour/i)).toBeDefined()
    expect(screen.getByTitle('Lists')).toBeDefined()
    expect(screen.getByTitle('Insert table')).toBeDefined()
    expect(screen.getByTitle(/Emoji/i)).toBeDefined()

    // Test opening Text Styles menu
    const stylesBtn = screen.getByTitle('Text styles')
    fireEvent.click(stylesBtn)
    expect(screen.getByText('Heading 1')).toBeDefined()
    expect(screen.getByText('Heading 2')).toBeDefined()
    expect(screen.getByText('Small text')).toBeDefined()

    // Test opening Color popover
    const colorBtn = screen.getByTitle(/Text colour/i)
    fireEvent.click(colorBtn)
    expect(screen.getByText('Remove colour')).toBeDefined()

    // Test opening Lists dropdown
    const listsBtn = screen.getByTitle('Lists')
    fireEvent.click(listsBtn)
    expect(screen.getByText('Bulleted list')).toBeDefined()
    expect(screen.getByText('Numbered list')).toBeDefined()
    expect(screen.getByText('Task list')).toBeDefined()
  })
})
