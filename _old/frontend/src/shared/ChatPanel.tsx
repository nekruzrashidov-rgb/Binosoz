import { useEffect, useMemo, useRef, useState } from 'react';
import { useInfiniteQuery, useQueryClient, type InfiniteData } from '@tanstack/react-query';
import { AnimatePresence, motion } from 'motion/react';
import { HubConnectionBuilder, HubConnectionState, LogLevel, type HubConnection } from '@microsoft/signalr';
import { CheckCheck, Loader2, MessageCircle, Send, Wifi, WifiOff } from 'lucide-react';
import { getSession, type Realm } from '@/api/session';
import { dealData } from '@/api/endpoints';
import type { ChatMessage, PagedResult } from '@/api/types';
import { EmptyState, ErrorState, Spinner, useToast } from '@/components/ui';
import { cn } from '@/lib/cn';
import { date, time } from '@/lib/format';

type Pages = InfiniteData<PagedResult<ChatMessage>, number>;

// Чат по сделке: история через REST (от новых к старым), новые сообщения — через SignalR.
// Отправка идёт через хаб, а при обрыве соединения — через REST.
export function ChatPanel({ dealId, realm, className }: { dealId: string; realm: Realm; className?: string }) {
  const api = useMemo(() => dealData(realm), [realm]);
  const queryClient = useQueryClient();
  const toast = useToast();
  const key = ['messages', realm, dealId];
  const [text, setText] = useState('');
  const [sending, setSending] = useState(false);
  const [state, setState] = useState<'connecting' | 'online' | 'offline'>('connecting');
  const hub = useRef<HubConnection | null>(null);
  const scroller = useRef<HTMLDivElement>(null);
  const me = getSession(realm)?.user.id;

  const query = useInfiniteQuery({
    queryKey: key,
    queryFn: ({ pageParam }) => api.messages(dealId, pageParam),
    initialPageParam: 1,
    getNextPageParam: (last) => (last.page * last.pageSize < last.totalCount ? last.page + 1 : undefined),
  });

  // Сообщения в порядке отображения: старые сверху.
  const messages = useMemo(() => {
    const all = query.data?.pages.flatMap((p) => p.items) ?? [];
    const unique = new Map(all.map((m) => [m.id, m]));
    return [...unique.values()].sort((a, b) => a.createdAt.localeCompare(b.createdAt));
  }, [query.data]);

  const append = (message: ChatMessage) => {
    queryClient.setQueryData<Pages>(key, (data) => {
      if (!data) return data;
      if (data.pages.some((p) => p.items.some((m) => m.id === message.id))) return data;
      const [first, ...rest] = data.pages;
      return { ...data, pages: [{ ...first, items: [message, ...first.items], totalCount: first.totalCount + 1 }, ...rest] };
    });
  };

  useEffect(() => {
    const connection = new HubConnectionBuilder()
      .withUrl('/hubs/chat', { accessTokenFactory: () => getSession(realm)?.accessToken ?? '' })
      .withAutomaticReconnect()
      .configureLogging(LogLevel.Warning)
      .build();
    hub.current = connection;
    connection.on('messageReceived', (m: ChatMessage) => m.dealId === dealId && append(m));
    connection.onreconnecting(() => setState('connecting'));
    connection.onreconnected(() => {
      setState('online');
      connection.invoke('JoinDeal', dealId).catch(() => {});
      query.refetch();
    });
    connection.onclose(() => setState('offline'));
    let stopped = false;
    connection
      .start()
      .then(() => (stopped ? undefined : connection.invoke('JoinDeal', dealId)))
      .then(() => !stopped && setState('online'))
      .catch(() => !stopped && setState('offline'));
    return () => {
      stopped = true;
      if (connection.state === HubConnectionState.Connected) connection.invoke('LeaveDeal', dealId).catch(() => {});
      connection.stop();
    };
    // Соединение пересоздаём только при смене сделки или сессии.
  }, [dealId, realm]);

  // Прокрутка вниз при новых сообщениях.
  const lastId = messages.at(-1)?.id;
  useEffect(() => {
    scroller.current?.scrollTo({ top: scroller.current.scrollHeight, behavior: 'smooth' });
  }, [lastId]);

  const send = async () => {
    const value = text.trim();
    if (!value || sending) return;
    setSending(true);
    try {
      const connection = hub.current;
      const message =
        connection?.state === HubConnectionState.Connected
          ? await connection.invoke<ChatMessage>('SendMessage', dealId, value)
          : await api.sendMessage(dealId, value);
      append(message);
      setText('');
    } catch (e) {
      toast.error(e instanceof Error && e.message ? e.message : 'Сообщение не отправлено');
    } finally {
      setSending(false);
    }
  };

  return (
    <div className={cn('flex h-[560px] flex-col overflow-hidden rounded-2xl border border-line/80 bg-white shadow-soft', className)}>
      <div className="flex items-center justify-between border-b border-line/70 px-5 py-3">
        <div className="flex items-center gap-2 font-semibold">
          <MessageCircle className="size-4 text-gold-2" /> Чат по сделке
        </div>
        <span className={cn('flex items-center gap-1.5 text-xs', state === 'online' ? 'text-free' : state === 'offline' ? 'text-muted' : 'text-booked')}>
          {state === 'online' ? <Wifi className="size-3.5" /> : state === 'offline' ? <WifiOff className="size-3.5" /> : <Loader2 className="size-3.5 animate-spin" />}
          {state === 'online' ? 'онлайн' : state === 'offline' ? 'без обновлений' : 'подключение'}
        </span>
      </div>

      <div ref={scroller} className="scrollbar-thin flex-1 overflow-y-auto bg-paper/50 px-4 py-4 sm:px-6">
        {query.hasNextPage && (
          <div className="mb-4 flex justify-center">
            <button onClick={() => query.fetchNextPage()} className="rounded-full bg-white px-4 py-1.5 text-xs text-muted shadow-soft hover:text-ink">
              {query.isFetchingNextPage ? 'Загрузка…' : 'Показать раньше'}
            </button>
          </div>
        )}
        {query.isLoading ? (
          <div className="flex h-full items-center justify-center text-muted"><Spinner /></div>
        ) : query.error ? (
          <ErrorState error={query.error} onRetry={() => query.refetch()} />
        ) : messages.length === 0 ? (
          <EmptyState title="Сообщений пока нет" text="Напишите первым — ответ придёт сюда же." icon={<MessageCircle className="size-6" />} />
        ) : (
          <AnimatePresence initial={false}>
            {messages.map((m, i) => {
              const mine = m.senderId === me;
              const newDay = i === 0 || messages[i - 1].createdAt.slice(0, 10) !== m.createdAt.slice(0, 10);
              return (
                <div key={m.id}>
                  {newDay && <p className="my-4 text-center text-[11px] uppercase tracking-widest text-muted">{date(m.createdAt)}</p>}
                  <motion.div
                    initial={{ opacity: 0, y: 10, scale: 0.98 }}
                    animate={{ opacity: 1, y: 0, scale: 1 }}
                    transition={{ duration: 0.22 }}
                    className={cn('mb-2 flex', mine ? 'justify-end' : 'justify-start')}
                  >
                    <div
                      className={cn(
                        'max-w-[78%] rounded-2xl px-4 py-2.5 text-[15px] leading-snug shadow-soft',
                        mine ? 'rounded-br-md bg-ink text-white' : 'rounded-bl-md bg-white',
                      )}
                    >
                      {!mine && <p className="mb-0.5 text-xs font-semibold text-gold-2">{m.senderName}</p>}
                      <p className="whitespace-pre-wrap break-words">{m.text}</p>
                      <p className={cn('mt-1 flex items-center justify-end gap-1 text-[11px]', mine ? 'text-white/45' : 'text-muted')}>
                        {time(m.createdAt)}
                        {mine && <CheckCheck className={cn('size-3.5', m.readAt ? 'text-gold' : '')} />}
                      </p>
                    </div>
                  </motion.div>
                </div>
              );
            })}
          </AnimatePresence>
        )}
      </div>

      <form
        onSubmit={(e) => {
          e.preventDefault();
          send();
        }}
        className="flex items-end gap-2 border-t border-line/70 p-3"
      >
        <textarea
          value={text}
          onChange={(e) => setText(e.target.value)}
          onKeyDown={(e) => {
            if (e.key === 'Enter' && !e.shiftKey) {
              e.preventDefault();
              send();
            }
          }}
          rows={1}
          maxLength={2000}
          placeholder="Сообщение…"
          className="max-h-32 min-h-11 flex-1 resize-none rounded-2xl border border-line bg-paper/60 px-4 py-2.5 text-[15px] outline-none transition focus:border-gold focus:bg-white focus:ring-4 focus:ring-gold/15"
        />
        <motion.button
          whileTap={{ scale: 0.9 }}
          type="submit"
          disabled={!text.trim() || sending}
          className="flex size-11 shrink-0 items-center justify-center rounded-full bg-ink text-white transition hover:bg-gold hover:text-ink disabled:opacity-30"
          aria-label="Отправить"
        >
          {sending ? <Spinner className="size-4" /> : <Send className="size-4" />}
        </motion.button>
      </form>
    </div>
  );
}
