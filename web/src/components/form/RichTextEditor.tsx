import { useEditor, EditorContent } from '@tiptap/react'
import StarterKit from '@tiptap/starter-kit'
import Underline from '@tiptap/extension-underline'
import { TextStyle } from '@tiptap/extension-text-style'
import { Color } from '@tiptap/extension-color'
import { FontFamily } from '@tiptap/extension-font-family'
import { Link } from '@tiptap/extension-link'
import { Placeholder } from '@tiptap/extension-placeholder'
import { useEffect, useRef, useState } from 'react'
import {
  Bold,
  Italic,
  UnderlineIcon,
  Strikethrough,
  List,
  ListOrdered,
  Quote,
  Link2,
  Code2,
  ImageIcon,
  Paperclip,
  Undo2,
  Redo2,
  Eraser,
} from 'lucide-react'
import { FontSize } from './fontSizeExtension'
import { AttachmentImage } from './attachmentImageExtension'
import { FileAttachment } from './fileAttachmentExtension'
import { resolveAttachmentUrls } from './resolveAttachmentUrls'
import { orbitApi } from '../../api/client'
import type { WorkItemAttachment } from '../../api/types'

const fontFamilies = [
  { label: 'Verdana (Default)', value: 'Verdana, Geneva, sans-serif' },
  { label: 'Calibri', value: 'Calibri, Candara, Segoe, Segoe UI, Optima, Arial, sans-serif' },
  { label: 'Times New Roman', value: '"Times New Roman", Times, serif' },
  { label: 'Arial', value: 'Arial, Helvetica, sans-serif' },
  { label: 'Courier New', value: '"Courier New", Courier, monospace' },
  { label: 'Sans-serif', value: 'ui-sans-serif, system-ui, sans-serif' },
  { label: 'Serif', value: 'ui-serif, Georgia, serif' },
  { label: 'Monospace', value: 'ui-monospace, SFMono-Regular, monospace' },
]

export function isRichTextEmpty(html: string): boolean {
  return html.replace(/<[^>]*>/g, '').trim().length === 0
}

const fontSizes = Array.from({ length: 20 }, (_, i) => {
  const size = `${i + 1}px`
  return { label: size, value: size }
})

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

  const editor = useEditor({
    extensions: [
      StarterKit,
      Underline,
      TextStyle,
      Color,
      FontFamily,
      FontSize,
      AttachmentImage,
      FileAttachment,
      Link.configure({ openOnClick: false, autolink: true }),
      Placeholder.configure({ placeholder }),
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

  return (
    <div className="rich-text-editor">
      <div className="rich-text-toolbar">
        <select
          aria-label="Font family"
          value={editor.getAttributes('textStyle').fontFamily || 'Verdana, Geneva, sans-serif'}
          onChange={(event) => {
            const font = event.target.value
            // Deferred so this runs after the browser finishes returning focus
            // from the native <select> popup; otherwise the focused editor's
            // stored mark for the next keystroke can be dropped.
            setTimeout(() => {
              if (font) editor.chain().focus().setFontFamily(font).run()
              else editor.chain().focus().unsetFontFamily().run()
            }, 0)
          }}
        >
          {fontFamilies.map((font) => (
            <option key={font.label} value={font.value}>{font.label}</option>
          ))}
        </select>
        <select
          aria-label="Font size"
          value={editor.getAttributes('textStyle').fontSize || '14px'}
          onChange={(event) => {
            const size = event.target.value
            setTimeout(() => {
              if (size) editor.chain().focus().setFontSize(size).run()
              else editor.chain().focus().unsetFontSize().run()
            }, 0)
          }}
        >
          {fontSizes.map((size) => (
            <option key={size.label} value={size.value}>{size.label}</option>
          ))}
        </select>
        <input
          type="color"
          aria-label="Font color"
          value={editor.getAttributes('textStyle').color || '#000000'}
          onChange={(event) => {
            const color = event.target.value
            editor.chain().focus().setColor(color).run()
          }}
          className="w-7 h-7 p-0.5 border border-gray-300 rounded cursor-pointer bg-transparent"
        />
        <span className="rich-text-toolbar-divider" />
        <button type="button" aria-label="Bold" className={editor.isActive('bold') ? 'is-active' : ''} onClick={() => editor.chain().focus().toggleBold().run()}><Bold size={15} /></button>
        <button type="button" aria-label="Italic" className={editor.isActive('italic') ? 'is-active' : ''} onClick={() => editor.chain().focus().toggleItalic().run()}><Italic size={15} /></button>
        <button type="button" aria-label="Underline" className={editor.isActive('underline') ? 'is-active' : ''} onClick={() => editor.chain().focus().toggleUnderline().run()}><UnderlineIcon size={15} /></button>
        <button type="button" aria-label="Strikethrough" className={editor.isActive('strike') ? 'is-active' : ''} onClick={() => editor.chain().focus().toggleStrike().run()}><Strikethrough size={15} /></button>
        <span className="rich-text-toolbar-divider" />
        <button type="button" aria-label="Bullet list" className={editor.isActive('bulletList') ? 'is-active' : ''} onClick={() => editor.chain().focus().toggleBulletList().run()}><List size={15} /></button>
        <button type="button" aria-label="Numbered list" className={editor.isActive('orderedList') ? 'is-active' : ''} onClick={() => editor.chain().focus().toggleOrderedList().run()}><ListOrdered size={15} /></button>
        <button type="button" aria-label="Quote" className={editor.isActive('blockquote') ? 'is-active' : ''} onClick={() => editor.chain().focus().toggleBlockquote().run()}><Quote size={15} /></button>
        <button type="button" aria-label="Code block" className={editor.isActive('codeBlock') ? 'is-active' : ''} onClick={() => editor.chain().focus().toggleCodeBlock().run()}><Code2 size={15} /></button>
        <button
          type="button"
          aria-label="Link"
          className={editor.isActive('link') ? 'is-active' : ''}
          onClick={() => {
            const previousUrl = editor.getAttributes('link').href as string | undefined
            const url = window.prompt('Link URL', previousUrl ?? '')
            if (url === null) return
            if (url === '') {
              editor.chain().focus().extendMarkRange('link').unsetLink().run()
              return
            }
            // SEC-07: Block javascript: and data: URIs — they execute when the link is clicked
            // and cannot be sanitised away once stored in the rich-text body.
            try {
              const parsed = new URL(url)
              if (parsed.protocol !== 'http:' && parsed.protocol !== 'https:') {
                alert('Only http:// and https:// links are allowed.')
                return
              }
            } catch {
              // URL() throws on relative URLs — allow them (treated as relative page links).
            }
            editor.chain().focus().extendMarkRange('link').setLink({ href: url }).run()
          }}
        ><Link2 size={15} /></button>
        {workItemId && (
          <>
            <span className="rich-text-toolbar-divider" />
            <button type="button" aria-label="Insert image" disabled={uploading !== null} onClick={() => imageInputRef.current?.click()}><ImageIcon size={15} /></button>
            <button type="button" aria-label="Attach file" disabled={uploading !== null} onClick={() => fileInputRef.current?.click()}><Paperclip size={15} /></button>
            <input ref={imageInputRef} type="file" accept="image/*" className="sr-only" onChange={(event) => handleImageSelected(event.target.files?.[0])} />
            <input ref={fileInputRef} type="file" className="sr-only" onChange={(event) => handleFileSelected(event.target.files?.[0])} />
          </>
        )}
        <span className="rich-text-toolbar-divider" />
        <button type="button" aria-label="Clear formatting" onClick={() => editor.chain().focus().clearNodes().unsetAllMarks().run()}><Eraser size={15} /></button>
        <button type="button" aria-label="Undo" onClick={() => editor.chain().focus().undo().run()}><Undo2 size={15} /></button>
        <button type="button" aria-label="Redo" onClick={() => editor.chain().focus().redo().run()}><Redo2 size={15} /></button>
        {uploading && <span className="rich-text-upload-status">{uploading === 'image' ? 'Uploading image…' : 'Uploading file…'}</span>}
      </div>
      <EditorContent editor={editor} className="rich-text-body" style={{ minHeight }} />
      {uploadError && <p className="form-error px-3 pb-2">{uploadError}</p>}
    </div>
  )
}
