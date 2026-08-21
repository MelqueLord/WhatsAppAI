import { useState } from 'react'
import { useQuery, useMutation, useQueryClient } from '@tanstack/react-query'
import { api } from '../lib/api'
import { Plus, X, Loader2 } from 'lucide-react'

interface ClientTag {
  id: string
  name: string
  color?: string
  description?: string
  isActive: boolean
}

interface TagAssignerProps {
  contactId: string
  compact?: boolean
}

export function TagAssigner({ contactId, compact = false }: TagAssignerProps) {
  const [showPicker, setShowPicker] = useState(false)
  const queryClient = useQueryClient()

  const { data: allTags = [] } = useQuery<ClientTag[]>({
    queryKey: ['client-tags'],
    queryFn: () => api.tags.list(),
  })

  const { data: contactTags = [], isLoading } = useQuery<ClientTag[]>({
    queryKey: ['contact-tags', contactId],
    queryFn: () => api.tags.getContactTags(contactId),
    enabled: !!contactId,
  })

  const assignMutation = useMutation({
    mutationFn: (tagId: string) => api.tags.assignToContact(contactId, tagId),
    onSuccess: () => {
      queryClient.invalidateQueries({ queryKey: ['contact-tags', contactId] })
    },
  })

  const removeMutation = useMutation({
    mutationFn: (tagId: string) => api.tags.removeFromContact(contactId, tagId),
    onSuccess: () => {
      queryClient.invalidateQueries({ queryKey: ['contact-tags', contactId] })
    },
  })

  const contactTagIds = new Set(contactTags.map((tag) => tag.id))
  const availableTags = allTags.filter((tag) => tag.isActive && !contactTagIds.has(tag.id))

  if (isLoading) {
    return compact ? <Loader2 className="w-3 h-3 animate-spin text-slate-400" /> : null
  }

  return (
    <div className="relative">
      {/* Current Tags */}
      <div className="flex items-center gap-1 flex-wrap">
        {contactTags.map((tag) => (
          <span
            key={tag.id}
            className="inline-flex items-center gap-1 px-2 py-0.5 rounded-full text-xs font-medium text-white"
            style={{ backgroundColor: tag.color || '#6B7280' }}
          >
            {tag.name}
            <button
              onClick={() => removeMutation.mutate(tag.id)}
              className="hover:bg-white/20 rounded-full p-0.5 transition-colors"
            >
              <X className="w-3 h-3" />
            </button>
          </span>
        ))}
        <button
          onClick={() => setShowPicker(!showPicker)}
          className={`inline-flex items-center gap-1 px-2 py-0.5 rounded-full text-xs font-medium transition-colors ${
            contactTags.length === 0
              ? 'bg-slate-100 text-slate-500 hover:bg-slate-200'
              : 'bg-slate-100 text-slate-400 hover:bg-slate-200'
          }`}
        >
          <Plus className="w-3 h-3" />
          {!compact && 'Tag'}
        </button>
      </div>

      {/* Tag Picker Dropdown */}
      {showPicker && (
        <div className="absolute top-full left-0 mt-1 w-56 bg-white rounded-lg shadow-lg border border-slate-200 z-50 py-1">
          {availableTags.length === 0 ? (
            <div className="px-3 py-2 text-sm text-slate-500">
              {allTags.length === 0 ? 'Nenhuma tag criada' : 'Todas as tags já atribuídas'}
            </div>
          ) : (
            availableTags.map((tag) => (
              <button
                key={tag.id}
                onClick={() => {
                  assignMutation.mutate(tag.id)
                  setShowPicker(false)
                }}
                disabled={assignMutation.isPending}
                className="w-full flex items-center gap-2 px-3 py-2 text-sm text-slate-700 hover:bg-slate-50 transition-colors"
              >
                <div className="w-3 h-3 rounded-full flex-shrink-0" style={{ backgroundColor: tag.color || '#6B7280' }} />
                <span className="truncate">{tag.name}</span>
                {tag.description && (
                  <span className="text-xs text-slate-400 truncate ml-auto">{tag.description}</span>
                )}
              </button>
            ))
          )}
        </div>
      )}
    </div>
  )
}
