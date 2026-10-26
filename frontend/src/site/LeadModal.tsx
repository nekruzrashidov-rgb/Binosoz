import { createContext, useContext, useState, type ReactNode } from 'react';
import { AnimatePresence, motion } from 'motion/react';
import { Check } from 'lucide-react';
import { ApiError } from '@/api/client';
import { catalog } from '@/api/endpoints';
import { Button, Input, Modal, PhoneInput, Textarea } from '@/components/ui';
import { normalizePhone } from '@/lib/format';

interface LeadTarget {
  apartmentId?: string;
  title?: string;
}

const LeadContext = createContext<(target?: LeadTarget) => void>(() => {});
export const useLeadModal = () => useContext(LeadContext);

export function LeadModalProvider({ children }: { children: ReactNode }) {
  const [target, setTarget] = useState<LeadTarget | null>(null);
  return (
    <LeadContext.Provider value={(t) => setTarget(t ?? {})}>
      {children}
      <LeadModal target={target} onClose={() => setTarget(null)} />
    </LeadContext.Provider>
  );
}

function LeadModal({ target, onClose }: { target: LeadTarget | null; onClose: () => void }) {
  const [name, setName] = useState('');
  const [phone, setPhone] = useState('');
  const [comment, setComment] = useState('');
  const [error, setError] = useState<ApiError | null>(null);
  const [loading, setLoading] = useState(false);
  const [done, setDone] = useState(false);

  const close = () => {
    onClose();
    setTimeout(() => {
      setDone(false);
      setError(null);
      setComment('');
    }, 300);
  };

  const submit = async (e: React.FormEvent) => {
    e.preventDefault();
    setError(null);
    if (phone.length < 9) {
      setError(new ApiError(400, 'Введите номер телефона полностью', { phone: ['Введите номер телефона полностью'] }));
      return;
    }
    setLoading(true);
    try {
      await catalog.createLead({
        name: name.trim(),
        phone: normalizePhone(phone),
        apartmentId: target?.apartmentId ?? null,
        source: 'Website',
        comment: comment.trim() || null,
      });
      setDone(true);
    } catch (err) {
      setError(err instanceof ApiError ? err : new ApiError(0, 'Не удалось отправить заявку'));
    } finally {
      setLoading(false);
    }
  };

  return (
    <Modal open={!!target} onClose={close} title={done ? undefined : 'Оставить заявку'} subtitle={done ? undefined : target?.title ?? 'Менеджер перезвонит в рабочее время'}>
      <AnimatePresence mode="wait">
        {done ? (
          <motion.div key="ok" initial={{ opacity: 0, scale: 0.96 }} animate={{ opacity: 1, scale: 1 }} className="flex flex-col items-center py-8 text-center">
            <motion.div
              initial={{ scale: 0 }}
              animate={{ scale: 1 }}
              transition={{ type: 'spring', bounce: 0.5, delay: 0.1 }}
              className="mb-5 flex size-16 items-center justify-center rounded-full bg-free text-white"
            >
              <Check className="size-8" strokeWidth={2.5} />
            </motion.div>
            <h3 className="display text-3xl">Заявка отправлена</h3>
            <p className="mt-2 max-w-xs text-muted">Менеджер свяжется с вами в ближайшее время.</p>
            <Button className="mt-7" onClick={close}>
              Хорошо
            </Button>
          </motion.div>
        ) : (
          <motion.form key="form" onSubmit={submit} className="flex flex-col gap-4" exit={{ opacity: 0 }}>
            <Input label="Ваше имя" value={name} onChange={(e) => setName(e.target.value)} required autoFocus error={error?.field('name')} />
            <PhoneInput value={phone} onChange={setPhone} error={error?.field('phone')} />
            <Textarea label="Комментарий" placeholder="Например: удобное время для звонка" value={comment} onChange={(e) => setComment(e.target.value)} />
            {error && !error.field('name') && !error.field('phone') && <p className="rounded-xl bg-danger-soft px-4 py-3 text-sm text-danger">{error.message}</p>}
            <Button type="submit" size="lg" loading={loading} className="mt-1">
              Отправить заявку
            </Button>
            <p className="text-center text-xs text-muted">Нажимая кнопку, вы соглашаетесь на обработку персональных данных.</p>
          </motion.form>
        )}
      </AnimatePresence>
    </Modal>
  );
}
