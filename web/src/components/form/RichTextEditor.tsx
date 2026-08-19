import { useEditor, EditorContent } from '@tiptap/react'
import StarterKit from '@tiptap/starter-kit'
import Underline from '@tiptap/extension-underline'
import { TextStyle } from '@tiptap/extension-text-style'
import { Color } from '@tiptap/extension-color'
import { FontFamily } from '@tiptap/extension-font-family'
import { Link } from '@tiptap/extension-link'
import { Placeholder } from '@tiptap/extension-placeholder'
import { Table } from '@tiptap/extension-table'
import { TableRow } from '@tiptap/extension-table-row'
import { TableCell } from '@tiptap/extension-table-cell'
import { TableHeader } from '@tiptap/extension-table-header'
import { TaskList } from '@tiptap/extension-task-list'
import { TaskItem } from '@tiptap/extension-task-item'
import { useEffect, useRef, useState } from 'react'
import {
  Bold,
  Italic,
  Underline as UnderlineIcon,
  Strikethrough,
  List,
  ListOrdered,
  ListTodo,
  Quote,
  Link2,
  Code2,
  ImageIcon,
  Paperclip,
  Undo2,
  Redo2,
  Eraser,
  Table as TableIcon,
  ChevronDown,
  Check,
  Smile,
  Trash2,
  Plus,
  Columns,
  Rows,
  Split,
  Combine,
} from 'lucide-react'
import { FontSize } from './fontSizeExtension'
import { AttachmentImage } from './attachmentImageExtension'
import { FileAttachment } from './fileAttachmentExtension'
import { resolveAttachmentUrls } from './resolveAttachmentUrls'
import { orbitApi } from '../../api/client'
import type { WorkItemAttachment } from '../../api/types'

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

const EMOJI_CATEGORIES: { name: string; emojis: string[] }[] = [
  {
    name: 'Smileys & People',
    emojis: ['😀', '😃', '😄', '😁', '😅', '😂', '🤣', '😊', '😇', '🙂', '😉', '😌', '😍', '🥰', '😘', '😋', '😜', '🤪', '😎', '🤩', '🥳', '😏', '😒', '😞', '😔', '😟', '😕', '🙁', '😣', '😖', '😫', '😩', '🥺', '😢', '😭', '😤', '😠', '😡', '🤯', '😳', '🥵', '🥶', '😱', '😨', '😰', '🤔', '🤫', '🤭', '🥱', '🤗'],
  },
  {
    name: 'Gestures & Work',
    emojis: ['👍', '👎', '👌', '✌️', '🤞', '🤟', '🤘', '🤙', '👈', '👉', '👆', '👇', '☝️', '✋', '🤚', '🖐️', '🖖', '👋', '🤝', '👏', '🙌', '👐', '🤲', '🙏', '💪', '🧠', '👀', '👁️', '🧑‍💻', '👨‍💻', '👩‍💻', '🕵️', '🚀', '🎯', '🔥', '✨', '💡', '🎉'],
  },
  {
    name: 'Objects & Symbols',
    emojis: ['✅', '❌', '⚠️', '⚡', '⭐', '🌟', '💥', '💯', '❤️', '🧡', '💛', '💚', '💙', '💜', '🖤', '🤍', '🤎', '💔', '📌', '📍', '📎', '📝', '📋', '📁', '📂', '🔒', '🔓', '🔑', '🏷️', '📦', '🔔', '🔕', '💬', '💭', '⏱️', '⏳', '📊', '📈', '📉', '🛠️', '⚙️', '🔗'],
  },
]

export function isRichTextEmpty(html: string): boolean {
  return html.replace(/<[^>]*>/g, '').trim().length === 0
}

async function uploadWorkItemAttachment(workItemId: string, file: File) {
  const presigned = await orbitApi.presignWorkItemAttachmentUpload(workItemId, file.name, file.type, file.size)
  await orbitApi.uploadAttachmentFile(presigned.uploadUrl, file)
  return orbitApi.confirmWorkItemAttachment(workItemId, {
    fileName: file.name,
    contentType: file.type,
    sizeBytes: file.size,
    objectKey: presigned.objectKey,
  })
}

export function RichTextEditor({
  value,
  onChange,
  placeholder,
  minHeight = 120,
  disabled = false,
  workItemId,
  attachments,
  onAttachmentUploaded,
}: {
  value: string
  onChange: (html: string) => void
  placeholder?: string
  minHeight?: number
  disabled?: boolean
  workItemId?: string
  attachments?: WorkItemAttachment[]
  onAttachmentUploaded?: () => void
}) {
  const imageInputRef = useRef<HTMLInputElement>(null)
  const fileInputRef = useRef<HTMLInputElement>(null)
  const [uploading, setUploading] = useState<'image' | 'file' | null>(null)
  const [uploadError, setUploadError] = useState<string | null>(null)

  // Popover / dropdown states
  const [stylesMenuOpen, setStylesMenuOpen] = useState(false)
  const [colorMenuOpen, setColorMenuOpen] = useState(false)
  const [listsMenuOpen, setListsMenuOpen] = useState(false)
  const [tableOptionsOpen, setTableOptionsOpen] = useState(false)
  const [emojiMenuOpen, setEmojiMenuOpen] = useState(false)
  const [emojiSearch, setEmojiSearch] = useState('')

  const editorContainerRef = useRef<HTMLDivElement>(null)

  const closeAllPopovers = () => {
    setStylesMenuOpen(false)
    setColorMenuOpen(false)
    setListsMenuOpen(false)
    setTableOptionsOpen(false)
    setEmojiMenuOpen(false)
  }

  const editor = useEditor({
    extensions: [
      StarterKit.configure({
        heading: {
          levels: [1, 2, 3, 4, 5, 6],
        },
        link: false,
        underline: false,
      }),
      Underline,
      TextStyle,
      Color,
      FontFamily,
      FontSize,
      AttachmentImage,
      FileAttachment,
      Table.configure({
        resizable: true,
        HTMLAttributes: {
          class: 'jira-table',
        },
      }),
      TableRow,
      TableHeader,
      TableCell,
      TaskList,
      TaskItem.configure({
        nested: true,
      }),
      Link.configure({ openOnClick: false, autolink: true }),
      Placeholder.configure({
        placeholder: placeholder || "Write description or type '/' for commands...",
      }),
    ],
    content: resolveAttachmentUrls(value, attachments),
    editable: !disabled,
    onUpdate: ({ editor }) => onChange(editor.getHTML()),
    editorProps: {
      attributes: { class: 'rich-text-content' },
    },
  })

  useEffect(() => {
    if (!editor) return
    editor.setEditable(!disabled)
  }, [editor, disabled])

  useEffect(() => {
    if (!editor) return
    const resolved = resolveAttachmentUrls(value, attachments)
    if (resolved !== editor.getHTML() && document.activeElement !== editor.view.dom) {
      editor.commands.setContent(resolved, { emitUpdate: false })
    }
  }, [value, attachments, editor])

  // Global click outside & escape listener to close popovers
  useEffect(() => {
    const handleOutsideClick = (e: MouseEvent) => {
      const target = e.target as HTMLElement
      if (target.closest('.jira-popover') || target.closest('.jira-toolbar-btn') || target.closest('.jira-table-bar-btn')) {
        return
      }
      closeAllPopovers()
    }
    const handleKeyDown = (e: KeyboardEvent) => {
      if (e.key === 'Escape') {
        closeAllPopovers()
      }
    }
    document.addEventListener('mousedown', handleOutsideClick)
    document.addEventListener('keydown', handleKeyDown)
    return () => {
      document.removeEventListener('mousedown', handleOutsideClick)
      document.removeEventListener('keydown', handleKeyDown)
    }
  }, [])

  if (!editor) return null

  const handleImageSelected = async (file: File | undefined) => {
    if (!file || !workItemId) return
    setUploadError(null)
    setUploading('image')
    try {
      const attachment = await uploadWorkItemAttachment(workItemId, file)
      editor.chain().focus().setImage({ src: attachment.downloadUrl, alt: attachment.fileName, attachmentId: attachment.id } as never).run()
      onAttachmentUploaded?.()
    } catch (error) {
      setUploadError(error instanceof Error ? error.message : 'Image upload failed.')
    } finally {
      setUploading(null)
      if (imageInputRef.current) imageInputRef.current.value = ''
    }
  }

  const handleFileSelected = async (file: File | undefined) => {
    if (!file || !workItemId) return
    setUploadError(null)
    setUploading('file')
    try {
      const attachment = await uploadWorkItemAttachment(workItemId, file)
      editor.chain().focus().insertFileAttachment({
        attachmentId: attachment.id,
        fileName: attachment.fileName,
        contentType: attachment.contentType,
        sizeBytes: attachment.sizeBytes,
        url: attachment.downloadUrl,
      }).run()
      onAttachmentUploaded?.()
    } catch (error) {
      setUploadError(error instanceof Error ? error.message : 'File upload failed.')
    } finally {
      setUploading(null)
      if (fileInputRef.current) fileInputRef.current.value = ''
    }
  }

  // Determine current active text style label
  const getCurrentStyle = () => {
    for (let level = 1; level <= 6; level++) {
      if (editor.isActive('heading', { level })) {
        return TEXT_STYLES.find((s) => s.level === level) || TEXT_STYLES[0]
      }
    }
    const fontSize = editor.getAttributes('textStyle').fontSize
    if (fontSize === '12px') {
      return TEXT_STYLES.find((s) => s.id === 'small') || TEXT_STYLES[0]
    }
    return TEXT_STYLES[0] // Normal text
  }

  const currentStyle = getCurrentStyle()
  const currentColor = editor.getAttributes('textStyle').color || null
  const isInsideTable = editor.isActive('table')

  // Apply a selected text style
  const applyTextStyle = (style: typeof TEXT_STYLES[number]) => {
    if (style.level) {
      editor.chain().focus().setHeading({ level: style.level as 1 | 2 | 3 | 4 | 5 | 6 }).run()
    } else if (style.id === 'small') {
      editor.chain().focus().setParagraph().setFontSize('12px').run()
    } else {
      editor.chain().focus().setParagraph().unsetFontSize().run()
    }
    setStylesMenuOpen(false)
  }

  // Insert Table helper
  const handleInsertTable = (rows = 3, cols = 3) => {
    closeAllPopovers()
    editor.chain().focus().insertTable({ rows, cols, withHeaderRow: true }).run()
  }

  // Filter emojis based on search
  const filteredEmojiCategories = EMOJI_CATEGORIES.map((category) => ({
    name: category.name,
    emojis: category.emojis.filter((emoji) =>
      emojiSearch ? emoji.includes(emojiSearch) : true
    ),
  })).filter((c) => c.emojis.length > 0)

  return (
    <div className="rich-text-editor jira-rich-editor" ref={editorContainerRef}>
      {/* Jira Top Toolbar */}
      <div className="rich-text-toolbar jira-editor-toolbar" role="toolbar" aria-label="Text formatting">
        {/* 1. Text styles dropdown */}
        <div className="relative inline-block">
          <button
            type="button"
            className={`jira-toolbar-btn jira-dropdown-btn ${stylesMenuOpen ? 'is-active' : ''}`}
            onMouseDown={(e) => e.preventDefault()}
            onClick={() => {
              setStylesMenuOpen(!stylesMenuOpen)
              setColorMenuOpen(false)
              setListsMenuOpen(false)
              setEmojiMenuOpen(false)
              setTableOptionsOpen(false)
            }}
            title="Text styles"
            aria-label="Text styles"
            aria-expanded={stylesMenuOpen}
            aria-haspopup="true"
          >
            <span className="jira-btn-text">{currentStyle.tag}</span>
            <ChevronDown size={11} className="jira-chevron" />
          </button>

          {stylesMenuOpen && (
            <div className="jira-popover jira-styles-menu" role="menu">
              <div className="jira-popover-header">Text styles</div>
              <div className="jira-styles-list">
                {TEXT_STYLES.map((style) => {
                  const isSelected = style.id === currentStyle.id
                  return (
                    <button
                      key={style.id}
                      type="button"
                      className={`jira-style-option ${isSelected ? 'is-selected' : ''}`}
                      onMouseDown={(e) => e.preventDefault()}
                      onClick={() => applyTextStyle(style)}
                      role="menuitem"
                    >
                      <span className="jira-style-tag">{style.tag}</span>
                      <span
                        className="jira-style-preview"
                        style={{
                          fontSize: style.size,
                          fontWeight: style.weight,
                        }}
                      >
                        {style.label}
                      </span>
                      <span className="jira-style-shortcut">{style.shortcut}</span>
                    </button>
                  )
                })}
              </div>
            </div>
          )}
        </div>

        {/* 2. Bold, Italic, Underline, Strike */}
        <button
          type="button"
          aria-label="Bold (⌘B)"
          title="Bold (⌘B)"
          className={`jira-toolbar-btn ${editor.isActive('bold') ? 'is-active' : ''}`}
          onMouseDown={(e) => e.preventDefault()}
          onClick={() => {
            closeAllPopovers()
            editor.chain().focus().toggleBold().run()
          }}
        >
          <Bold size={15} />
        </button>
        <button
          type="button"
          aria-label="Italic (⌘I)"
          title="Italic (⌘I)"
          className={`jira-toolbar-btn ${editor.isActive('italic') ? 'is-active' : ''}`}
          onMouseDown={(e) => e.preventDefault()}
          onClick={() => {
            closeAllPopovers()
            editor.chain().focus().toggleItalic().run()
          }}
        >
          <Italic size={15} />
        </button>
        <button
          type="button"
          aria-label="Underline (⌘U)"
          title="Underline (⌘U)"
          className={`jira-toolbar-btn ${editor.isActive('underline') ? 'is-active' : ''}`}
          onMouseDown={(e) => e.preventDefault()}
          onClick={() => {
            closeAllPopovers()
            editor.chain().focus().toggleUnderline().run()
          }}
        >
          <UnderlineIcon size={15} />
        </button>
        <button
          type="button"
          aria-label="Strikethrough (⌘⇧S)"
          title="Strikethrough (⌘⇧S)"
          className={`jira-toolbar-btn ${editor.isActive('strike') ? 'is-active' : ''}`}
          onMouseDown={(e) => e.preventDefault()}
          onClick={() => {
            closeAllPopovers()
            editor.chain().focus().toggleStrike().run()
          }}
        >
          <Strikethrough size={15} />
        </button>

        {/* 3. Text colour picker */}
        <div className="relative inline-block">
          <button
            type="button"
            className={`jira-toolbar-btn jira-color-trigger ${colorMenuOpen ? 'is-active' : ''}`}
            onMouseDown={(e) => e.preventDefault()}
            onClick={() => {
              setColorMenuOpen(!colorMenuOpen)
              setStylesMenuOpen(false)
              setListsMenuOpen(false)
              setEmojiMenuOpen(false)
              setTableOptionsOpen(false)
            }}
            title="Text colour ⌘\B"
            aria-label="Text colour"
            aria-expanded={colorMenuOpen}
          >
            <div className="flex flex-col items-center justify-center">
              <span className="font-bold text-[13px] leading-tight font-serif">A</span>
              <span
                className="jira-color-bar"
                style={{ backgroundColor: currentColor || 'var(--ink, #172B4D)' }}
              />
            </div>
            <ChevronDown size={11} className="jira-chevron" />
          </button>

          {colorMenuOpen && (
            <div className="jira-popover jira-color-popover" role="dialog" aria-label="Text colour picker">
              <div className="jira-popover-title">Text colour</div>
              <div className="jira-color-grid">
                {JIRA_COLOR_PALETTE.map((color) => {
                  const isSelected = currentColor?.toLowerCase() === color.hex.toLowerCase()
                  return (
                    <button
                      key={color.hex}
                      type="button"
                      className={`jira-color-swatch ${isSelected ? 'is-selected' : ''}`}
                      style={{ backgroundColor: color.hex }}
                      title={color.label}
                      onMouseDown={(e) => e.preventDefault()}
                      onClick={() => {
                        editor.chain().focus().setColor(color.hex).run()
                        setColorMenuOpen(false)
                      }}
                    >
                      {isSelected && <Check size={12} className="jira-color-check text-white" strokeWidth={3} />}
                    </button>
                  )
                })}
              </div>
              <button
                type="button"
                className="jira-remove-color-btn"
                onMouseDown={(e) => e.preventDefault()}
                onClick={() => {
                  editor.chain().focus().unsetColor().run()
                  setColorMenuOpen(false)
                }}
              >
                Remove colour
              </button>
            </div>
          )}
        </div>

        <span className="rich-text-toolbar-divider" />

        {/* 4. Lists Dropdown (Bullets, Numbered, Task list) */}
        <div className="relative inline-block">
          <button
            type="button"
            className={`jira-toolbar-btn jira-dropdown-btn ${
              editor.isActive('bulletList') || editor.isActive('orderedList') || editor.isActive('taskList')
                ? 'is-active'
                : ''
            } ${listsMenuOpen ? 'is-active' : ''}`}
            onMouseDown={(e) => e.preventDefault()}
            onClick={() => {
              setListsMenuOpen(!listsMenuOpen)
              setStylesMenuOpen(false)
              setColorMenuOpen(false)
              setEmojiMenuOpen(false)
              setTableOptionsOpen(false)
            }}
            title="Lists"
            aria-label="Lists"
          >
            {editor.isActive('orderedList') ? (
              <ListOrdered size={15} />
            ) : editor.isActive('taskList') ? (
              <ListTodo size={15} />
            ) : (
              <List size={15} />
            )}
            <ChevronDown size={11} className="jira-chevron" />
          </button>

          {listsMenuOpen && (
            <div className="jira-popover jira-dropdown-menu" role="menu">
              <button
                type="button"
                className={`jira-menu-item ${editor.isActive('bulletList') ? 'is-selected' : ''}`}
                onMouseDown={(e) => e.preventDefault()}
                onClick={() => {
                  editor.chain().focus().toggleBulletList().run()
                  setListsMenuOpen(false)
                }}
              >
                <List size={15} className="text-gray-500" />
                <span>Bulleted list</span>
                <span className="jira-item-shortcut">⌘⇧8</span>
              </button>
              <button
                type="button"
                className={`jira-menu-item ${editor.isActive('orderedList') ? 'is-selected' : ''}`}
                onMouseDown={(e) => e.preventDefault()}
                onClick={() => {
                  editor.chain().focus().toggleOrderedList().run()
                  setListsMenuOpen(false)
                }}
              >
                <ListOrdered size={15} className="text-gray-500" />
                <span>Numbered list</span>
                <span className="jira-item-shortcut">⌘⇧7</span>
              </button>
              <button
                type="button"
                className={`jira-menu-item ${editor.isActive('taskList') ? 'is-selected' : ''}`}
                onMouseDown={(e) => e.preventDefault()}
                onClick={() => {
                  editor.chain().focus().toggleTaskList().run()
                  setListsMenuOpen(false)
                }}
              >
                <ListTodo size={15} className="text-gray-500" />
                <span>Task list</span>
                <span className="jira-item-shortcut">⌘⇧6</span>
              </button>
            </div>
          )}
        </div>

        {/* 5. Insert Table button */}
        <button
          type="button"
          className={`jira-toolbar-btn ${editor.isActive('table') ? 'is-active' : ''}`}
          onMouseDown={(e) => e.preventDefault()}
          onClick={() => handleInsertTable(3, 3)}
          title="Insert table"
          aria-label="Insert table"
        >
          <TableIcon size={15} />
        </button>

        {/* 6. Blockquote & Code block */}
        <button
          type="button"
          aria-label="Quote (⌘⇧9)"
          title="Quote (⌘⇧9)"
          className={`jira-toolbar-btn ${editor.isActive('blockquote') ? 'is-active' : ''}`}
          onMouseDown={(e) => e.preventDefault()}
          onClick={() => {
            closeAllPopovers()
            editor.chain().focus().toggleBlockquote().run()
          }}
        >
          <Quote size={15} />
        </button>
        <button
          type="button"
          aria-label="Code block (```)"
          title="Code block (```)"
          className={`jira-toolbar-btn ${editor.isActive('codeBlock') ? 'is-active' : ''}`}
          onMouseDown={(e) => e.preventDefault()}
          onClick={() => {
            closeAllPopovers()
            editor.chain().focus().toggleCodeBlock().run()
          }}
        >
          <Code2 size={15} />
        </button>

        {/* 7. Emoji Picker */}
        <div className="relative inline-block">
          <button
            type="button"
            className={`jira-toolbar-btn ${emojiMenuOpen ? 'is-active' : ''}`}
            onMouseDown={(e) => e.preventDefault()}
            onClick={() => {
              setEmojiMenuOpen(!emojiMenuOpen)
              setStylesMenuOpen(false)
              setColorMenuOpen(false)
              setListsMenuOpen(false)
              setTableOptionsOpen(false)
            }}
            title="Emoji :"
            aria-label="Emoji"
          >
            <Smile size={15} />
          </button>

          {emojiMenuOpen && (
            <div className="jira-popover jira-emoji-popover" role="dialog" aria-label="Emoji Picker">
              <div className="p-2.5 border-b border-gray-200 dark:border-gray-700">
                <input
                  type="text"
                  placeholder="Search..."
                  value={emojiSearch}
                  onChange={(e) => setEmojiSearch(e.target.value)}
                  className="w-full text-xs px-2.5 py-1.5 border border-gray-300 dark:border-gray-600 rounded bg-white dark:bg-gray-800 text-gray-800 dark:text-gray-100 outline-none focus:border-blue-500"
                  autoFocus
                />
              </div>
              <div className="jira-emoji-scroll">
                {filteredEmojiCategories.map((category) => (
                  <div key={category.name} className="p-2.5">
                    <div className="text-[11px] font-bold text-gray-500 dark:text-gray-400 mb-1.5 uppercase tracking-wider">
                      {category.name}
                    </div>
                    <div className="grid grid-cols-7 gap-1">
                      {category.emojis.map((emoji) => (
                        <button
                          key={emoji}
                          type="button"
                          className="jira-emoji-btn"
                          onMouseDown={(e) => e.preventDefault()}
                          onClick={() => {
                            editor.chain().focus().insertContent(emoji).run()
                            setEmojiMenuOpen(false)
                            setEmojiSearch('')
                          }}
                        >
                          {emoji}
                        </button>
                      ))}
                    </div>
                  </div>
                ))}
              </div>
            </div>
          )}
        </div>

        {/* 8. Link */}
        <button
          type="button"
          aria-label="Link (⌘K)"
          title="Link (⌘K)"
          className={`jira-toolbar-btn ${editor.isActive('link') ? 'is-active' : ''}`}
          onMouseDown={(e) => e.preventDefault()}
          onClick={() => {
            closeAllPopovers()
            const previousUrl = editor.getAttributes('link').href as string | undefined
            const url = window.prompt('Link URL', previousUrl ?? '')
            if (url === null) return
            if (url === '') {
              editor.chain().focus().extendMarkRange('link').unsetLink().run()
              return
            }
            try {
              const parsed = new URL(url)
              if (parsed.protocol !== 'http:' && parsed.protocol !== 'https:') {
                alert('Only http:// and https:// links are allowed.')
                return
              }
            } catch {
              // Relative URL allowed
            }
            editor.chain().focus().extendMarkRange('link').setLink({ href: url }).run()
          }}
        >
          <Link2 size={15} />
        </button>

        {/* 9. Attachments */}
        {workItemId && (
          <>
            <span className="rich-text-toolbar-divider" />
            <button
              type="button"
              aria-label="Insert image"
              title="Insert image"
              disabled={uploading !== null}
              className="jira-toolbar-btn"
              onMouseDown={(e) => e.preventDefault()}
              onClick={() => {
                closeAllPopovers()
                imageInputRef.current?.click()
              }}
            >
              <ImageIcon size={15} />
            </button>
            <button
              type="button"
              aria-label="Attach file"
              title="Attach file"
              disabled={uploading !== null}
              className="jira-toolbar-btn"
              onMouseDown={(e) => e.preventDefault()}
              onClick={() => {
                closeAllPopovers()
                fileInputRef.current?.click()
              }}
            >
              <Paperclip size={15} />
            </button>
            <input ref={imageInputRef} type="file" accept="image/*" className="sr-only" onChange={(event) => handleImageSelected(event.target.files?.[0])} />
            <input ref={fileInputRef} type="file" className="sr-only" onChange={(event) => handleFileSelected(event.target.files?.[0])} />
          </>
        )}

        <span className="rich-text-toolbar-divider" />

        {/* 10. Eraser, Undo, Redo */}
        <button
          type="button"
          aria-label="Clear formatting"
          title="Clear formatting"
          className="jira-toolbar-btn"
          onMouseDown={(e) => e.preventDefault()}
          onClick={() => {
            closeAllPopovers()
            editor.chain().focus().clearNodes().unsetAllMarks().run()
          }}
        >
          <Eraser size={15} />
        </button>
        <button
          type="button"
          aria-label="Undo (⌘Z)"
          title="Undo (⌘Z)"
          className="jira-toolbar-btn"
          onMouseDown={(e) => e.preventDefault()}
          onClick={() => {
            closeAllPopovers()
            editor.chain().focus().undo().run()
          }}
        >
          <Undo2 size={15} />
        </button>
        <button
          type="button"
          aria-label="Redo (⌘⇧Z)"
          title="Redo (⌘⇧Z)"
          className="jira-toolbar-btn"
          onMouseDown={(e) => e.preventDefault()}
          onClick={() => {
            closeAllPopovers()
            editor.chain().focus().redo().run()
          }}
        >
          <Redo2 size={15} />
        </button>

        {uploading && (
          <span className="rich-text-upload-status">
            {uploading === 'image' ? 'Uploading image…' : 'Uploading file…'}
          </span>
        )}
      </div>

      {/* Jira Floating Table Contextual Bar (when inside a table) */}
      {isInsideTable && (
        <div className="jira-table-floating-bar" role="toolbar" aria-label="Table options">
          <div className="relative inline-block">
            <button
              type="button"
              className="jira-table-bar-btn jira-dropdown-btn font-semibold"
              onMouseDown={(e) => e.preventDefault()}
              onClick={() => setTableOptionsOpen(!tableOptionsOpen)}
            >
              <span>Table options</span>
              <ChevronDown size={11} className="ml-1" />
            </button>
            {tableOptionsOpen && (
              <div className="jira-popover jira-dropdown-menu table-options-popover" role="menu">
                <button
                  type="button"
                  className="jira-menu-item"
                  onMouseDown={(e) => e.preventDefault()}
                  onClick={() => {
                    editor.chain().focus().toggleHeaderRow().run()
                    setTableOptionsOpen(false)
                  }}
                >
                  <Rows size={14} className="text-gray-500" />
                  <span>Header row</span>
                </button>
                <button
                  type="button"
                  className="jira-menu-item"
                  onMouseDown={(e) => e.preventDefault()}
                  onClick={() => {
                    editor.chain().focus().toggleHeaderColumn().run()
                    setTableOptionsOpen(false)
                  }}
                >
                  <Columns size={14} className="text-gray-500" />
                  <span>Header column</span>
                </button>
                <button
                  type="button"
                  className="jira-menu-item"
                  onMouseDown={(e) => e.preventDefault()}
                  onClick={() => {
                    editor.chain().focus().mergeCells().run()
                    setTableOptionsOpen(false)
                  }}
                >
                  <Combine size={14} className="text-gray-500" />
                  <span>Merge cells</span>
                </button>
                <button
                  type="button"
                  className="jira-menu-item"
                  onMouseDown={(e) => e.preventDefault()}
                  onClick={() => {
                    editor.chain().focus().splitCell().run()
                    setTableOptionsOpen(false)
                  }}
                >
                  <Split size={14} className="text-gray-500" />
                  <span>Split cell</span>
                </button>
                <div className="border-t border-gray-200 dark:border-gray-700 my-1" />
                <button
                  type="button"
                  className="jira-menu-item text-red-600 hover:bg-red-50 dark:hover:bg-red-950/30"
                  onMouseDown={(e) => e.preventDefault()}
                  onClick={() => {
                    editor.chain().focus().deleteTable().run()
                    setTableOptionsOpen(false)
                  }}
                >
                  <Trash2 size={14} className="text-red-500" />
                  <span>Delete table</span>
                </button>
              </div>
            )}
          </div>

          <span className="jira-bar-divider" />

          {/* Quick Row/Col controls */}
          <button
            type="button"
            className="jira-table-bar-btn"
            title="Insert row above"
            onMouseDown={(e) => e.preventDefault()}
            onClick={() => editor.chain().focus().addRowBefore().run()}
          >
            <Plus size={12} className="mr-0.5" />
            <Rows size={13} />
            <span className="text-[11px] ml-1">Row Above</span>
          </button>
          <button
            type="button"
            className="jira-table-bar-btn"
            title="Insert row below"
            onMouseDown={(e) => e.preventDefault()}
            onClick={() => editor.chain().focus().addRowAfter().run()}
          >
            <Plus size={12} className="mr-0.5" />
            <Rows size={13} />
            <span className="text-[11px] ml-1">Row Below</span>
          </button>

          <span className="jira-bar-divider" />

          <button
            type="button"
            className="jira-table-bar-btn"
            title="Insert column left"
            onMouseDown={(e) => e.preventDefault()}
            onClick={() => editor.chain().focus().addColumnBefore().run()}
          >
            <Plus size={12} className="mr-0.5" />
            <Columns size={13} />
            <span className="text-[11px] ml-1">Col Left</span>
          </button>
          <button
            type="button"
            className="jira-table-bar-btn"
            title="Insert column right"
            onMouseDown={(e) => e.preventDefault()}
            onClick={() => editor.chain().focus().addColumnAfter().run()}
          >
            <Plus size={12} className="mr-0.5" />
            <Columns size={13} />
            <span className="text-[11px] ml-1">Col Right</span>
          </button>

          <span className="jira-bar-divider" />

          <button
            type="button"
            className="jira-table-bar-btn text-red-600 hover:text-red-700"
            title="Delete row"
            onMouseDown={(e) => e.preventDefault()}
            onClick={() => editor.chain().focus().deleteRow().run()}
          >
            <Trash2 size={13} />
            <span className="text-[11px] ml-1">Row</span>
          </button>
          <button
            type="button"
            className="jira-table-bar-btn text-red-600 hover:text-red-700"
            title="Delete column"
            onMouseDown={(e) => e.preventDefault()}
            onClick={() => editor.chain().focus().deleteColumn().run()}
          >
            <Trash2 size={13} />
            <span className="text-[11px] ml-1">Col</span>
          </button>
          <button
            type="button"
            className="jira-table-bar-btn text-red-600 hover:text-red-700 ml-auto"
            title="Delete table"
            onMouseDown={(e) => e.preventDefault()}
            onClick={() => editor.chain().focus().deleteTable().run()}
          >
            <Trash2 size={13} />
          </button>
        </div>
      )}

      {/* Body */}
      <EditorContent
        editor={editor}
        className="rich-text-body jira-editor-body"
        style={{ minHeight }}
        onClick={closeAllPopovers}
      />

      {uploadError && <p className="form-error px-3 pb-2">{uploadError}</p>}
    </div>
  )
}
