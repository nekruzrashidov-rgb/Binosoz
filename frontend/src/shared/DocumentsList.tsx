import { useState } from 'react';
import { motion } from 'motion/react';
import { Download, FileImage, FileText, FileType2 } from 'lucide-react';
import { openSignedLink } from '@/api/client';
import { dealData } from '@/api/endpoints';
import type { Realm } from '@/api/session';
import type { DocumentResponse } from '@/api/types';
import { Badge, EmptyState, Spinner, useToast } from '@/components/ui';
import { dateShort, documentTypeLabel, fileSize } from '@/lib/format';

function iconFor(contentType: string) {
  if (contentType.startsWith('image/')) return FileImage;
  if (contentType === 'application/pdf') return FileText;
  return FileType2;
}

// Документы сделки. Скачивание: сервер проверяет права и выдаёт ссылку на 15 минут.
export function DocumentsList({ documents, realm }: { documents: DocumentResponse[]; realm: Realm }) {
  const toast = useToast();
  const [busy, setBusy] = useState<string | null>(null);

  const open = async (doc: DocumentResponse) => {
    setBusy(doc.id);
    try {
      const link = await dealData(realm).documentLink(doc.id);
      openSignedLink(link.url);
    } catch (e) {
      toast.error(e);
    } finally {
      setBusy(null);
    }
  };

  if (documents.length === 0)
    return <EmptyState title="Документов пока нет" text="Договор, акты и квитанции появятся здесь, как только их загрузит офис." icon={<FileText className="size-6" />} />;

  return (
    <div className="grid gap-3 sm:grid-cols-2">
      {documents.map((d, i) => {
        const Icon = iconFor(d.contentType);
        return (
          <motion.button
            key={d.id}
            initial={{ opacity: 0, y: 10 }}
            animate={{ opacity: 1, y: 0 }}
            transition={{ delay: i * 0.04 }}
            onClick={() => open(d)}
            className="group flex items-center gap-4 rounded-2xl border border-line/80 bg-white p-4 text-left transition-all duration-300 hover:-translate-y-0.5 hover:border-ink/15 hover:shadow-lift"
          >
            <span className="flex size-12 shrink-0 items-center justify-center rounded-xl bg-paper text-gold-2 transition-colors duration-300 group-hover:bg-ink group-hover:text-gold">
              <Icon className="size-5" />
            </span>
            <span className="min-w-0 flex-1">
              <span className="flex items-center gap-2">
                <Badge tone="gold">{documentTypeLabel[d.type]}</Badge>
                {d.signedAt && <span className="text-xs text-muted">подписан {dateShort(d.signedAt)}</span>}
              </span>
              <span className="mt-1.5 block truncate text-sm font-medium">{d.fileName}</span>
              <span className="block text-xs text-muted">
                {fileSize(d.sizeBytes)} · загружен {dateShort(d.createdAt)}
              </span>
            </span>
            <span className="flex size-9 items-center justify-center rounded-full text-muted transition-all group-hover:bg-paper group-hover:text-ink">
              {busy === d.id ? <Spinner className="size-4" /> : <Download className="size-4 transition-transform group-hover:translate-y-0.5" />}
            </span>
          </motion.button>
        );
      })}
    </div>
  );
}
