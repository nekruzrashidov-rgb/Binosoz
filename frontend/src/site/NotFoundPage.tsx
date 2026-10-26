import { motion } from 'motion/react';
import { ArrowLeft } from 'lucide-react';
import { ButtonLink } from '@/components/ui';

export function NotFoundPage() {
  return (
    <div className="container-x flex min-h-[80vh] flex-col items-center justify-center pt-20 text-center">
      <motion.p initial={{ opacity: 0, scale: 0.8 }} animate={{ opacity: 1, scale: 1 }} transition={{ type: 'spring', bounce: 0.4 }} className="display text-[140px] leading-none text-gold sm:text-[200px]">
        404
      </motion.p>
      <h1 className="display mt-2 text-4xl">Такой страницы нет</h1>
      <p className="mt-3 text-muted">Возможно, квартиру уже продали или ссылка устарела.</p>
      <ButtonLink to="/" className="mt-8" size="lg" icon={<ArrowLeft className="size-4" />}>
        На главную
      </ButtonLink>
    </div>
  );
}
