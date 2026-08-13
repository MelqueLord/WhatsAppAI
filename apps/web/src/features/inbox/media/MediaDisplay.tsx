import { useState } from 'react'
import { Download, FileText, Image, Mic, Play } from 'lucide-react'

interface MediaDisplayProps {
  messageId: string
  type: string
  mediaId?: string
  caption?: string
}

export function MediaDisplay({ messageId, type, mediaId, caption }: MediaDisplayProps) {
  const [loading, setLoading] = useState(false)
  const [error, setError] = useState<string | null>(null)

  const handleDownload = async () => {
    if (!mediaId) return

    setLoading(true)
    setError(null)

    try {
      const response = await fetch(`/api/media/${messageId}/download`, {
        credentials: 'include',
      })

      if (!response.ok) {
        throw new Error('Failed to download media')
      }

      const blob = await response.blob()
      const url = window.URL.createObjectURL(blob)
      const a = document.createElement('a')
      a.href = url
      a.download = `media-${messageId}`
      document.body.appendChild(a)
      a.click()
      window.URL.revokeObjectURL(url)
      document.body.removeChild(a)
    } catch (err) {
      setError('Erro ao baixar mídia')
    } finally {
      setLoading(false)
    }
  }

  const getIcon = () => {
    switch (type) {
      case 'Image':
        return <Image className="w-8 h-8 text-blue-500" />
      case 'Document':
        return <FileText className="w-8 h-8 text-orange-500" />
      case 'Audio':
        return <Mic className="w-8 h-8 text-green-500" />
      case 'Video':
        return <Play className="w-8 h-8 text-purple-500" />
      default:
        return <FileText className="w-8 h-8 text-gray-500" />
    }
  }

  const getLabel = () => {
    switch (type) {
      case 'Image':
        return 'Imagem'
      case 'Document':
        return 'Documento'
      case 'Audio':
        return 'Áudio'
      case 'Video':
        return 'Vídeo'
      default:
        return 'Mídia'
    }
  }

  return (
    <div className="flex items-center gap-3 p-3 bg-gray-50 rounded-lg">
      {getIcon()}
      <div className="flex-1 min-w-0">
        <p className="text-sm font-medium text-gray-700">{getLabel()}</p>
        {caption && (
          <p className="text-xs text-gray-500 truncate">{caption}</p>
        )}
        {error && (
          <p className="text-xs text-red-500">{error}</p>
        )}
      </div>
      {mediaId && (
        <button
          onClick={handleDownload}
          disabled={loading}
          className="p-2 hover:bg-gray-200 rounded-full disabled:opacity-50"
          title="Baixar"
        >
          <Download className={`w-4 h-4 text-gray-600 ${loading ? 'animate-spin' : ''}`} />
        </button>
      )}
    </div>
  )
}
