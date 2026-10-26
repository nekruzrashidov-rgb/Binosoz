import { isRouteErrorResponse, useRouteError } from 'react-router';
import { ButtonLink } from '@/components/ui';

export function RouteError() {
  const error = useRouteError();
  const notFound = isRouteErrorResponse(error) && error.status === 404;
  // После обновления сайта старые части кода могут не загрузиться — достаточно перезагрузить страницу.
  const chunk = error instanceof Error && /dynamically imported module|Failed to fetch/i.test(error.message);
  return (
    <div className="flex min-h-screen flex-col items-center justify-center bg-paper px-6 text-center">
      <p className="eyebrow">{notFound ? '404' : 'Ошибка'}</p>
      <h1 className="display mt-3 text-4xl">{notFound ? 'Страница не найдена' : 'Что-то пошло не так'}</h1>
      <p className="mt-3 max-w-md text-muted">
        {chunk ? 'Сайт обновился. Перезагрузите страницу.' : 'Попробуйте обновить страницу или вернуться на главную.'}
      </p>
      <div className="mt-8 flex gap-3">
        <button onClick={() => window.location.reload()} className="h-10 rounded-full border border-line bg-white px-5 text-sm font-medium hover:bg-paper">
          Обновить
        </button>
        <ButtonLink to="/">На главную</ButtonLink>
      </div>
    </div>
  );
}
