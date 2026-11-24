namespace Sitora.WebApi.Errors;

// Каталог текстов ошибок API: русский текст (как его бросает сервис или валидатор) → стабильный машиночитаемый code,
// таджикский и английский переводы. Сервисы пишут ошибки по-русски, как раньше; ответ получает code и, при
// Accept-Language: tg или en, перевод (R-09). Текст, которого здесь нет (с подставленными значениями, редкий), остаётся
// русским, а code берётся по типу исключения (not_found, conflict, business_rule…).
// Таджикские формулировки — литературный таджикский, кириллица; перед запуском их должен вычитать носитель языка.
public static class ErrorCatalog
{
    public record Entry(string Code, string Tg, string En);

    private static readonly Dictionary<string, Entry> Exact = new(StringComparer.Ordinal);
    // Тексты с подставленным хвостом («Неизвестная роль: X»): совпадение по началу, хвост переносится как есть.
    private static readonly List<(string Prefix, Entry Entry)> Prefixes = [];

    private static void E(string ru, string code, string tg, string en) => Exact[ru] = new Entry(code, tg, en);
    private static void P(string ruPrefix, string code, string tgPrefix, string enPrefix) => Prefixes.Add((ruPrefix, new Entry(code, tgPrefix, enPrefix)));

    public enum Language { Ru, Tg, En }

    public static bool TryFind(string? ru, out Entry entry, out string tgText)
    {
        entry = null!;
        tgText = string.Empty;
        if (string.IsNullOrEmpty(ru))
            return false;
        if (Exact.TryGetValue(ru, out var found))
        {
            entry = found;
            tgText = found.Tg;
            return true;
        }
        foreach (var (prefix, e) in Prefixes)
        {
            if (ru.StartsWith(prefix, StringComparison.Ordinal))
            {
                entry = e;
                tgText = e.Tg + ru[prefix.Length..];
                return true;
            }
        }
        return false;
    }

    public static string Translate(string ru, bool tajik) => Translate(ru, tajik ? Language.Tg : Language.Ru);

    // Перевод на язык ответа; текста нет в каталоге — русский.
    public static string Translate(string ru, Language language)
    {
        if (language == Language.Ru || !TryFind(ru, out var entry, out var tg))
            return ru;
        if (language == Language.Tg)
            return tg;
        var prefix = Prefixes.FirstOrDefault(p => ReferenceEquals(p.Entry, entry)).Prefix;
        return prefix is null ? entry.En : entry.En + ru[prefix.Length..];
    }

    static ErrorCatalog()
    {
        // ---------- общие (по коду ответа) ----------
        E("Проверьте введённые данные", "validation_failed", "Маълумоти воридшударо санҷед", "Check the data you entered");
        E("Некорректный формат запроса", "malformed_request", "Формати дархост нодуруст аст", "Malformed request");
        E("Требуется вход", "unauthorized", "Ворид шудан лозим аст", "Sign-in required");
        E("Недостаточно прав", "forbidden", "Ҳуқуқи кофӣ нест", "Not enough rights");
        E("Доступ запрещён", "forbidden", "Дастрасӣ манъ аст", "Access denied");
        E("Не найдено", "not_found", "Ёфт нашуд", "Not found");
        E("Такого адреса нет", "route_not_found", "Чунин суроға вуҷуд надорад", "No such address");
        E("Объект не найден", "not_found", "Объект ёфт нашуд", "Object not found");
        E("Метод не поддерживается для этого адреса", "method_not_allowed", "Ин усул барои ин суроға дастгирӣ намешавад", "This method is not supported for this address");
        E("Метод не поддерживается", "method_not_allowed", "Усул дастгирӣ намешавад", "Method not supported");
        E("Конфликт", "conflict", "Ихтилоф", "Conflict");
        E("Слишком большой запрос", "payload_too_large", "Дархост хеле калон аст", "Request too large");
        E("Размер запроса превышает допустимый", "payload_too_large", "Андозаи дархост аз ҳадди иҷозатдодашуда зиёд аст", "The request size exceeds the limit");
        E("Неподдерживаемый тип содержимого", "unsupported_media_type", "Навъи мундариҷа дастгирӣ намешавад", "Unsupported content type");
        E("Отправьте данные в формате JSON", "unsupported_media_type", "Маълумотро дар формати JSON фиристед", "Send the data as JSON");
        E("Нарушено бизнес-правило", "business_rule", "Қоидаи корӣ вайрон шуд", "A business rule was violated");
        E("Неверный запрос", "bad_request", "Дархости нодуруст", "Bad request");
        E("Нужен тариф выше", "tariff_limit", "Тарифи баландтар лозим аст", "A higher tariff is needed");
        E("Слишком много запросов", "rate_limited", "Дархостҳо хеле зиёданд", "Too many requests");
        E("Слишком много запросов. Попробуйте позже", "rate_limited", "Дархостҳо хеле зиёданд. Баъдтар кӯшиш кунед", "Too many requests. Try again later");
        E("Сервис недоступен", "service_unavailable", "Хизматрасонӣ дастнорас аст", "Service unavailable");
        E("Демо-доступ: только просмотр и чат", "demo_read_only", "Дастрасии намоишӣ: танҳо дидан ва чат", "Demo access: viewing and chat only");
        // ---------- эко-паспорт дома и «зелёные» настройки ----------
        E("Тариф — от 0 до 100 TJS за кВт·ч", "invalid_tariff", "Тариф — аз 0 то 100 TJS барои 1 кВт·с", "Tariff — from 0 to 100 TJS per kWh");
        E("Потребление типового дома — от 0 до 2000 кВт·ч на м² в год", "invalid_baseline", "Истеъмоли бинои муқаррарӣ — аз 0 то 2000 кВт·с барои 1 м² дар як сол", "Consumption of a typical building — from 0 to 2000 kWh per m² a year");
        E("Укажите источник цифры типового дома", "source_required", "Манбаи рақами бинои муқаррариро нишон диҳед", "Give the source of the typical building figure");
        E("Источник — не длиннее 500 символов", "source_too_long", "Манбаъ — на зиёда аз 500 аломат", "Source — up to 500 characters");
        E("Коэффициент CO₂ — от 0 до 100 кг на кг бумаги", "invalid_co2_factor", "Зариби CO₂ — аз 0 то 100 кг барои 1 кг коғаз", "CO₂ factor — from 0 to 100 kg per kg of paper");
        E("Укажите источник коэффициента CO₂", "source_required", "Манбаи зариби CO₂-ро нишон диҳед", "Give the source of the CO₂ factor");
        E("Неизвестный класс энергоэффективности", "invalid_energy_class", "Синфи номаълуми самаранокии энергия", "Unknown energy efficiency class");
        E("Неизвестный тип отопления", "invalid_heating_type", "Навъи номаълуми гармидиҳӣ", "Unknown heating type");

        // ---------- удобства дома и условия рассрочки ----------
        E("Неизвестное удобство дома", "invalid_amenity", "Қулайии номаълуми бино", "Unknown building amenity");
        E("Укажите широту и долготу вместе", "location_pair_required", "Арз ва тӯли ҷуғрофиро якҷоя нишон диҳед", "Give latitude and longitude together");
        E("Широта — от -90 до 90", "invalid_latitude", "Арзи ҷуғрофӣ — аз -90 то 90", "Latitude — from -90 to 90");
        E("Долгота — от -180 до 180", "invalid_longitude", "Тӯли ҷуғрофӣ — аз -180 то 180", "Longitude — from -180 to 180");
        E("Высота этажа — от 2,4 до 6 м", "invalid_floor_height", "Баландии ошёна — аз 2,4 то 6 м", "Floor height — from 2.4 to 6 m");
        E("Первоначальный взнос — от 0 до 100%", "invalid_down_payment", "Пардохти аввалия — аз 0 то 100%", "Down payment — from 0 to 100%");
        E(Validators.Settings.SaveInstallmentSettingsRequestValidator.MonthsMessage, "invalid_installment_term",
            $"Мӯҳлати муҳлатдиҳӣ — аз 1 то {Sitora.Infrastructure.Services.PaymentScheduleCalculator.MaxMonths} моҳ",
            $"Installment term — from 1 to {Sitora.Infrastructure.Services.PaymentScheduleCalculator.MaxMonths} months");
        E("Утепление стен — не длиннее 200 символов", "eco_text_too_long", "Гармигузаронии деворҳо — на зиёда аз 200 аломат", "Wall insulation — up to 200 characters");
        E("Окна — не длиннее 200 символов", "eco_text_too_long", "Тирезаҳо — на зиёда аз 200 аломат", "Windows — up to 200 characters");
        E("Мощность солнечных панелей — от 0 до 100 000 кВт", "invalid_solar_power", "Иқтидори панелҳои офтобӣ — аз 0 то 100 000 кВт", "Solar panel power — from 0 to 100,000 kW");
        E("Площадь озеленения — от 0 до 10 000 000 м²", "invalid_green_area", "Масоҳати кабудизоркунӣ — аз 0 то 10 000 000 м²", "Green area — from 0 to 10,000,000 m²");
        E("Расчётное потребление — от 0 до 2000 кВт·ч на м² в год", "invalid_energy_use", "Истеъмоли ҳисобӣ — аз 0 то 2000 кВт·с барои 1 м² дар як сол", "Estimated consumption — from 0 to 2000 kWh per m² a year");
        E("Письма с кодами не отправляются: почтовый сервис не настроен", "email_not_configured", "Мактубҳо бо код фиристода намешаванд: хизматрасонии почта танзим нашудааст", "Code e-mails are not sent: the mail service is not set up");
        E("SMS с кодами пока не отправляются. Войдите по почте", "sms_not_configured", "SMS бо код ҳоло фиристода намешавад. Тавассути почта ворид шавед", "SMS codes are not sent yet. Sign in by e-mail");
        E("Внутренняя ошибка сервера", "internal_error", "Хатои дохилии сервер", "Internal server error");
        E("Произошла ошибка. Мы уже разбираемся; если повторится, сообщите номер ошибки (traceId)", "internal_error", "Хато рух дод. Мо аллакай онро меомӯзем; агар такрор шавад, рақами хаторо (traceId) хабар диҳед", "Something went wrong. We are looking into it; if it happens again, tell us the error number (traceId)");
        E("Ошибка", "error", "Хато", "Error");

        // ---------- вход и учётные записи ----------
        E("Неверная почта или пароль", "invalid_credentials", "Почтаи электронӣ ё парол нодуруст аст", "Wrong e-mail or password");
        E("Слишком много неудачных попыток входа, попробуйте позже", "login_locked", "Кӯшишҳои нобарори воридшавӣ хеле зиёданд, баъдтар кӯшиш кунед", "Too many failed sign-in attempts, try again later");
        E("Сессия истекла, войдите заново", "session_expired", "Ҷаласа ба охир расид, аз нав ворид шавед", "The session has expired, sign in again");
        E("Неверный или устаревший код", "invalid_code", "Рамз нодуруст ё кӯҳна аст", "Wrong or expired code");
        E("Время на подтверждение входа истекло, войдите заново", "challenge_expired", "Вақти тасдиқи воридшавӣ гузашт, аз нав ворид шавед", "The time to confirm the sign-in has run out, sign in again");
        E("Используйте вход по коду", "use_code_login", "Бо рамз ворид шавед", "Use sign-in by code");
        E("Сотрудники входят по паролю", "staff_use_password", "Кормандон бо парол ворид мешаванд", "Staff sign in with a password");
        E("Приглашение недействительно или уже использовано", "invitation_invalid", "Даъватнома беэътибор аст ё аллакай истифода шудааст", "The invitation is not valid or has already been used");
        E("Если почта принадлежит сотруднику, мы отправили на неё ссылку для смены пароля", "password_reset_sent", "Агар почта ба корманд тааллуқ дошта бошад, мо ба он пайванди иваз кардани паролро фиристодем", "If the e-mail belongs to a staff member, we have sent a link to change the password");
        E("Ссылка для смены пароля недействительна или устарела. Запросите новую", "reset_link_invalid", "Пайванди иваз кардани парол беэътибор ё кӯҳна аст. Пайванди навро дархост кунед", "The password change link is not valid or has expired. Request a new one");
        E("Неверный код из приложения", "invalid_totp", "Рамзи барнома нодуруст аст", "Wrong code from the app");
        E("Организация отключена. Обратитесь к владельцу платформы", "organization_disabled", "Ташкилот хомӯш карда шудааст. Ба соҳиби платформа муроҷиат кунед", "The organization is disabled. Contact the platform owner");
        E("Учётная запись заблокирована", "account_blocked", "Ҳисоб баста шудааст", "The account is blocked");
        E("Код уже отправлен. Запросить новый можно через минуту", "code_cooldown", "Рамз аллакай фиристода шуд. Рамзи навро пас аз як дақиқа дархост кардан мумкин аст", "The code has already been sent. You can request a new one in a minute");
        E("Слишком много запросов кода. Попробуйте позже", "code_rate_limited", "Дархостҳои рамз хеле зиёданд. Баъдтар кӯшиш кунед", "Too many code requests. Try again later");
        E("2FA не включена", "two_factor_disabled", "2FA фаъол нест", "2FA is not on");
        E("2FA уже включена", "two_factor_enabled", "2FA аллакай фаъол аст", "2FA is already on");
        E("2FA уже включена. Чтобы получить новый ключ, сначала отключите её", "two_factor_enabled", "2FA аллакай фаъол аст. Барои гирифтани калиди нав аввал онро хомӯш кунед", "2FA is already on. To get a new key, turn it off first");
        E("2FA — для сотрудников: покупатели входят по коду из почты", "two_factor_staff_only", "2FA барои кормандон аст: харидорон бо рамз аз почта ворид мешаванд", "2FA is for staff: buyers sign in with a code from e-mail");
        E("В режиме «войти как владелец» 2FA не настраивается", "two_factor_impersonation", "Дар реҷаи «ворид шудан ҳамчун соҳиб» 2FA танзим намешавад", "2FA cannot be set up in «sign in as owner» mode");
        E("Сначала получите ключ: GET /auth/2fa/setup", "two_factor_setup_required", "Аввал калидро гиред: GET /auth/2fa/setup", "Get the key first: GET /auth/2fa/setup");
        E("Задайте пароль: сотрудники входят по почте и паролю", "password_required", "Парол гузоред: кормандон бо почта ва парол ворид мешаванд", "Set a password: staff sign in with e-mail and password");
        E("Покупатели входят по коду из почты — пароль им не нужен", "customer_no_password", "Харидорон бо рамз аз почта ворид мешаванд — ба онҳо парол лозим нест", "Buyers sign in with a code from e-mail — they do not need a password");
        E("Пароль — не короче 8 символов", "password_too_short", "Парол бояд на камтар аз 8 аломат бошад", "Password — at least 8 characters");
        E("Пользователь заблокирован", "user_blocked", "Корбар баста шудааст", "The user is blocked");
        E("Нельзя заблокировать самого себя", "cannot_block_self", "Худро бастан мумкин нест", "You cannot block yourself");
        E("Суперадмина заблокировать нельзя", "cannot_block_superadmin", "Суперадминро бастан мумкин нест", "The super admin cannot be blocked");
        E("Нельзя войти под создателем платформы", "cannot_impersonate_owner", "Бо номи созандаи платформа ворид шудан мумкин нест", "You cannot sign in as the platform creator");
        E("Режим «войти как владелец» не включён", "impersonation_disabled", "Реҷаи «ворид шудан ҳамчун соҳиб» фаъол нест", "«Sign in as owner» mode is not on");
        E("Выберите организацию: войдите как её владелец", "organization_choice_required", "Ташкилотро интихоб кунед: ҳамчун соҳиби он ворид шавед", "Choose an organization: sign in as its owner");
        E("Приглашение уже принято", "invitation_accepted", "Даъватнома аллакай қабул шудааст", "The invitation has already been accepted");
        E("Приглашение не найдено", "invitation_not_found", "Даъватнома ёфт нашуд", "Invitation not found");
        E("У пользователя нет почты: новый пароль некуда отправить", "user_without_email", "Корбар почта надорад: пароли навро ба ҷое фиристодан мумкин нест", "The user has no e-mail: there is nowhere to send a new password");
        E("Пользователь с такой почтой уже есть", "email_taken", "Корбар бо ин почта аллакай вуҷуд дорад", "A user with this e-mail already exists");
        E("Эта почта принадлежит сотруднику. Войдите на вкладке «Сотрудник»", "email_is_staff", "Ин почта ба корманд тааллуқ дорад. Дар варақаи «Корманд» ворид шавед", "This e-mail belongs to a staff member. Sign in on the «Staff» tab");
        E("Пользователь не найден", "user_not_found", "Корбар ёфт нашуд", "User not found");
        E("Организация или пользователь не найдены", "not_found", "Ташкилот ё корбар ёфт нашуд", "Organization or user not found");
        E("Доступно только создателю платформы", "platform_owner_only", "Танҳо барои созандаи платформа дастрас аст", "Only the platform creator can do this");
        // ---------- вход покупателя по своему 6-значному коду ----------
        P("Неверная почта или код входа. Осталось попыток: ", "invalid_pin", "Почта ё рамзи воридшавӣ нодуруст аст. Кӯшишҳои боқимонда: ", "Wrong e-mail or sign-in code. Attempts left: ");
        P("Вход по коду временно заблокирован. Минут до разблокировки: ", "pin_locked", "Воридшавӣ бо рамз муваққатан баста шуд. Дақиқаҳо то кушодашавӣ: ", "Sign-in by code is temporarily blocked. Minutes until unblocked: ");
        E("Код входа ещё не задан. Нажмите «Забыли код?» — мы пришлём письмо, и вы зададите свой код", "pin_not_set", "Рамзи воридшавӣ ҳанӯз гузошта нашудааст. «Рамзро фаромӯш кардед?»-ро пахш кунед — мо мактуб мефиристем ва шумо рамзи худро мегузоред", "No sign-in code has been set yet. Press «Forgot the code?» — we will send an e-mail and you will set your own code");
        E("Эта почта уже есть в кабинете — задайте код входа через «Забыли код?»", "pin_not_set", "Ин почта аллакай дар кабинет ҳаст — рамзи воридшавиро тавассути «Рамзро фаромӯш кардед?» гузоред", "This e-mail is already in the cabinet — set a sign-in code via «Forgot the code?»");
        E("Почта уже зарегистрирована — войдите или восстановите код", "email_taken", "Почта аллакай ба қайд гирифта шудааст — ворид шавед ё рамзро барқарор кунед", "This e-mail is already registered — sign in or restore the code");
        E("Почта не подтверждена. Введите код из письма или запросите новый", "email_not_confirmed", "Почта тасдиқ нашудааст. Рамзи мактубро ворид кунед ё рамзи навро дархост кунед", "The e-mail is not confirmed. Enter the code from the e-mail or request a new one");
        E("Укажите имя (от 2 до 200 символов)", "name_required", "Номро нишон диҳед (аз 2 то 200 аломат)", "Enter a name (2 to 200 characters)");
        E("Код входа — ровно 6 цифр", "pin_format", "Рамзи воридшавӣ — маҳз 6 рақам", "The sign-in code is exactly 6 digits");
        E("Код слишком простой: не используйте одинаковые цифры", "pin_weak", "Рамз хеле содда аст: рақамҳои якхеларо истифода набаред", "The code is too simple: do not use the same digits");
        E("Код слишком простой: не используйте цифры подряд, как 123456 или 654321", "pin_weak", "Рамз хеле содда аст: рақамҳои пай дар паиро, ба монанди 123456 ё 654321, истифода набаред", "The code is too simple: do not use digits in a row, like 123456 or 654321");
        E("Придумайте код входа", "pin_required", "Рамзи воридшавиро фикр карда бароред", "Make up a sign-in code");
        E("Введите код входа", "pin_required", "Рамзи воридшавиро ворид кунед", "Enter the sign-in code");
        E("Введите код из письма", "code_required", "Рамзи мактубро ворид кунед", "Enter the code from the e-mail");
        P("Не удалось создать учётную запись покупателя: ", "customer_account_failed", "Сохтани ҳисоби харидор муяссар нашуд: ", "Could not create the buyer account: ");
        P("Неизвестная роль: ", "unknown_role", "Нақши номаълум: ", "Unknown role: ");
        P("Роль должна быть одной из: ", "invalid_role", "Нақш бояд яке аз инҳо бошад: ", "The role must be one of: ");

        // ---------- организации, тарифы, бренд ----------
        E("Организация не найдена", "organization_not_found", "Ташкилот ёфт нашуд", "Organization not found");
        E("Укажите организацию", "organization_required", "Ташкилотро нишон диҳед", "Specify the organization");
        E("Укажите застройщика (organization)", "organization_required", "Сохтмончиро нишон диҳед (organization)", "Specify the developer (organization)");
        E("Организация отключена", "organization_disabled", "Ташкилот хомӯш карда шудааст", "The organization is disabled");
        E("Организация с таким коротким именем уже есть", "slug_taken", "Ташкилот бо чунин номи кӯтоҳ аллакай вуҷуд дорад", "An organization with this short name already exists");
        E("Это короткое имя уже занято другой организацией", "slug_taken", "Ин номи кӯтоҳ аллакай аз ҷониби ташкилоти дигар банд аст", "This short name is already taken by another organization");
        E("Этот домен уже занят другой организацией", "domain_taken", "Ин домен аллакай аз ҷониби ташкилоти дигар банд аст", "This domain is already taken by another organization");
        E("Домен — например richtower.tj, без https:// и пути", "invalid_domain", "Домен — масалан richtower.tj, бе https:// ва роҳ", "Domain — for example richtower.tj, without https:// and a path");
        E("Поддомен платформы задаётся коротким именем, а не своим доменом", "invalid_domain", "Зердомени платформа бо номи кӯтоҳ муайян мешавад, на бо домени худ", "A platform subdomain is set by the short name, not by your own domain");
        E("Название организации — от 1 до 200 символов", "invalid_name", "Номи ташкилот — аз 1 то 200 аломат", "Organization name — from 1 to 200 characters");
        E("Максимум организаций — от 1 до 100 000", "invalid_limit", "Ҳадди аксари ташкилотҳо — аз 1 то 100 000", "Maximum organizations — from 1 to 100,000");
        E("Лимит — не меньше 1; пусто — без ограничения", "invalid_limit", "Ҳад — на камтар аз 1; холӣ — бе маҳдудият", "Limit — at least 1; empty — no limit");
        E("Укажите имя владельца", "owner_name_required", "Номи соҳибро нишон диҳед", "Enter the owner's name");
        E("Укажите имя и фамилию владельца", "owner_name_required", "Ном ва насаби соҳибро нишон диҳед", "Enter the owner's first and last name");
        E("Неверный адрес почты владельца", "invalid_email", "Суроғаи почтаи соҳиб нодуруст аст", "Wrong owner e-mail address");
        E("Неверный адрес почты для связи", "invalid_email", "Суроғаи почта барои тамос нодуруст аст", "Wrong contact e-mail address");
        E("Заявка с этой почты уже на рассмотрении. Мы свяжемся с вами", "application_pending", "Дархост аз ин почта аллакай дар баррасӣ аст. Мо бо шумо тамос мегирем", "An application from this e-mail is already under review. We will contact you");
        E("Заявка уже рассмотрена", "application_reviewed", "Дархост аллакай баррасӣ шудааст", "The application has already been reviewed");
        E("Заявка не найдена", "application_not_found", "Дархост ёфт нашуд", "Application not found");
        E("Тариф не найден", "tariff_not_found", "Тариф ёфт нашуд", "Tariff not found");
        E("Тариф с таким названием уже есть", "tariff_name_taken", "Тариф бо чунин ном аллакай вуҷуд дорад", "A tariff with this name already exists");
        E("Тариф отключён: назначить его нельзя", "tariff_disabled", "Тариф хомӯш аст: онро таъин кардан мумкин нест", "The tariff is disabled: it cannot be assigned");
        E("Название тарифа — от 1 до 50 символов", "invalid_name", "Номи тариф — аз 1 то 50 аломат", "Tariff name — from 1 to 50 characters");
        E("Цена тарифа — от 0 до 1 000 000 000", "invalid_price", "Нархи тариф — аз 0 то 1 000 000 000", "Tariff price — from 0 to 1,000,000,000");
        E("Дата окончания тарифа должна быть в будущем", "invalid_date", "Санаи анҷоми тариф бояд дар оянда бошад", "The tariff end date must be in the future");
        E("Сайт застройщика не найден", "site_not_found", "Сайти сохтмончӣ ёфт нашуд", "Developer site not found");
        E("Логотип не найден", "logo_not_found", "Логотип ёфт нашуд", "Logo not found");
        E("Цвет — в формате #RRGGBB, например #1f6feb", "invalid_color", "Ранг — дар формати #RRGGBB, масалан #1f6feb", "Colour — in the #RRGGBB format, for example #1f6feb");
        E("Неверный chat id Telegram", "invalid_telegram_chat", "chat id-и Telegram нодуруст аст", "Wrong Telegram chat id");

        // ---------- каталог ----------
        E("Дом не найден", "building_not_found", "Бино ёфт нашуд", "Building not found");
        E("Этаж не найден", "floor_not_found", "Ошёна ёфт нашуд", "Floor not found");
        E("Квартира не найдена", "apartment_not_found", "Манзил ёфт нашуд", "Apartment not found");
        E("План этажа не задан", "floor_plan_not_found", "Нақшаи ошёна муайян нашудааст", "The floor plan is not set");
        E("Пустой шаблон плана", "invalid_floor_plan", "Қолаби нақша холӣ аст", "Empty plan template");
        E("Шаблон должен начинаться с <svg>", "invalid_floor_plan", "Қолаб бояд бо <svg> оғоз шавад", "The template must start with <svg>");
        E("Площадь должна быть больше нуля", "invalid_area", "Масоҳат бояд аз сифр зиёд бошад", "The area must be greater than zero");
        E("Цена за м² должна быть больше нуля", "invalid_price", "Нарх барои м² бояд аз сифр зиёд бошад", "The price per m² must be greater than zero");
        E("Квартиру в текущем статусе нельзя продать", "apartment_not_sellable", "Манзилро дар ҳолати ҳозира фурӯхтан мумкин нест", "An apartment in its current status cannot be sold");
        E("Квартира уже продана", "apartment_sold", "Манзил аллакай фурӯхта шудааст", "The apartment has already been sold");
        E("Квартиры меняются позициями на плане. Загрузите изменение в два шага: сначала на свободные позиции", "plan_slot_swap", "Манзилҳо дар нақша ҷой иваз мекунанд. Тағйиротро дар ду қадам бор кунед: аввал ба ҷойҳои холӣ", "Apartments swap positions on the plan. Upload the change in two steps: first to free positions");
        E("Импорт доступен сотрудникам со всеми домами организации или с одним домом", "import_forbidden", "Воридот барои кормандоне дастрас аст, ки ба ҳамаи биноҳои ташкилот ё ба як бино дастрасӣ доранд", "Import is available to staff with all buildings of the organization or with one building");
        E("В файле нет строк с квартирами", "import_empty", "Дар файл сатрҳо бо манзилҳо нестанд", "The file has no apartment rows");
        E("Не удалось прочитать файл Excel", "invalid_file", "Хондани файли Excel муяссар нашуд", "Could not read the Excel file");
        E("Не удалось прочитать файл. Нужен файл Excel в формате .xlsx", "invalid_file", "Хондани файл муяссар нашуд. Файли Excel дар формати .xlsx лозим аст", "Could not read the file. An Excel file in .xlsx format is needed");
        E("Нужен файл Excel в формате .xlsx", "invalid_file", "Файли Excel дар формати .xlsx лозим аст", "An Excel file in .xlsx format is needed");
        E("Файл пустой", "invalid_file", "Файл холӣ аст", "The file is empty");
        E("Пока шёл импорт, такой телефон завели вручную. Загрузите файл ещё раз", "import_conflict", "Ҳангоми воридот ин рақами телефон дастӣ илова шуд. Файлро бори дигар бор кунед", "While the import was running, this phone was added by hand. Upload the file again");
        E("Этап стройки не найден", "stage_not_found", "Марҳилаи сохтмон ёфт нашуд", "Construction stage not found");
        E("Процент готовности должен быть от 0 до 100", "invalid_progress", "Фоизи омодагӣ бояд аз 0 то 100 бошад", "The completion percentage must be from 0 to 100");

        // ---------- заявки, клиенты, брони ----------
        E("Заявка принята. Мы свяжемся с вами в ближайшее время.", "lead_accepted", "Дархост қабул шуд. Мо ба наздикӣ бо шумо тамос мегирем.", "Request received. We will contact you shortly.");
        E("Клиент не найден", "customer_not_found", "Мизоҷ ёфт нашуд", "Customer not found");
        E("Клиент с таким телефоном уже есть у этого застройщика", "customer_phone_taken", "Мизоҷ бо чунин телефон аллакай дар ин сохтмончӣ ҳаст", "This developer already has a customer with this phone");
        E("Клиент и квартира принадлежат разным застройщикам", "organization_mismatch", "Мизоҷ ва манзил ба сохтмончиёни гуногун тааллуқ доранд", "The customer and the apartment belong to different developers");
        E("Укажите дом, в котором заводите клиента", "building_required", "Бинои мизоҷро нишон диҳед", "Specify the building in which you add the customer");
        E("Укажите дом или квартиру, к которой относится заявка", "building_required", "Бино ё манзилеро, ки дархост ба он дахл дорад, нишон диҳед", "Specify the building or apartment the lead is about");
        E("Ответственным можно назначить только активного менеджера этого застройщика", "invalid_manager", "Масъул танҳо менеҷери фаъоли ҳамин сохтмончӣ таъин шуда метавонад", "Only an active manager of this developer can be assigned");
        E("Бронь не найдена", "booking_not_found", "Брон ёфт нашуд", "Booking not found");
        E("Бронь уже не активна", "booking_inactive", "Брон дигар фаъол нест", "The booking is no longer active");
        E("Квартира забронирована другим покупателем", "apartment_already_booked", "Манзил аз ҷониби харидори дигар брон шудааст", "The apartment is booked by another buyer");
        E("Квартира уже забронирована или недоступна", "apartment_already_booked", "Манзил аллакай брон шудааст ё дастнорас аст", "The apartment is already booked or not available");
        E("На этот номер уже есть активная бронь. Чтобы забронировать другую квартиру, свяжитесь с менеджером", "booking_conflict", "Барои ин рақам аллакай брони фаъол ҳаст. Барои брон кардани манзили дигар бо менеҷер тамос гиред", "There is already an active booking for this number. To book another apartment, contact a manager");
        E("С этого адреса уже есть несколько активных броней. Чтобы забронировать ещё, свяжитесь с менеджером", "booking_ip_limit", "Аз ин суроға аллакай якчанд брони фаъол ҳаст. Барои боз брон кардан бо менеҷер тамос гиред", "There are already several active bookings from this address. To book more, contact a manager");
        E("Бронировать на клиента может только менеджер", "manager_only", "Танҳо менеҷер метавонад барои мизоҷ брон кунад", "Only a manager can book for a customer");

        // ---------- сделки, графики, платежи ----------
        E("Сделка не найдена", "deal_not_found", "Аҳд ёфт нашуд", "Deal not found");
        E("Сделка не найдена в этом доме", "deal_not_found", "Аҳд дар ин бино ёфт нашуд", "Deal not found in this building");
        E("По этой квартире уже есть открытая сделка", "deal_exists", "Барои ин манзил аллакай аҳди кушода ҳаст", "There is already an open deal for this apartment");
        E("Сделка закрыта, этап менять нельзя", "deal_closed", "Аҳд баста шудааст, марҳиларо иваз кардан мумкин нест", "The deal is closed, the stage cannot be changed");
        E("Сделка отменена", "deal_cancelled", "Аҳд бекор шудааст", "The deal is cancelled");
        E("Сделка уже на этом этапе", "deal_same_stage", "Аҳд аллакай дар ҳамин марҳила аст", "The deal is already at this stage");
        E("Новую сделку нельзя создать сразу завершённой или отменённой", "invalid_stage", "Аҳди навро якбора анҷомёфта ё бекоршуда сохтан мумкин нест", "A new deal cannot be created completed or cancelled");
        E("Для возврата сделки на прошлый этап или отмены укажите причину", "reason_required", "Барои баргардонидани аҳд ба марҳилаи пешина ё бекор кардан сабабро нишон диҳед", "Give a reason to move the deal back or cancel it");
        E("Скидка должна быть не меньше нуля и меньше цены", "invalid_discount", "Тахфиф бояд на камтар аз сифр ва камтар аз нарх бошад", "The discount must be at least zero and less than the price");
        E("Укажите причину скидки", "discount_reason_required", "Сабаби тахфифро нишон диҳед", "Give the reason for the discount");
        E("Нет курса USD. Бухгалтер должен ввести курс, прежде чем оформлять сделку в долларах", "rate_missing", "Қурби USD нест. Пеш аз бастани аҳд бо доллар баҳисобгир бояд қурбро ворид кунад", "No USD rate. The accountant must enter the rate before a deal in dollars");
        E("График платежей ещё не составлен", "schedule_missing", "Ҷадвали пардохтҳо ҳанӯз тартиб дода нашудааст", "The payment schedule has not been made yet");
        E("Сначала составьте график платежей", "schedule_missing", "Аввал ҷадвали пардохтҳоро тартиб диҳед", "Make the payment schedule first");
        E("По сделке уже есть платежи, график менять нельзя", "schedule_locked", "Барои аҳд аллакай пардохтҳо ҳастанд, ҷадвалро иваз кардан мумкин нест", "The deal already has payments, the schedule cannot be changed");
        E("Первоначальный взнос должен быть от 0 до 100% (не включая 100)", "invalid_down_payment", "Пардохти аввал бояд аз 0 то 100% бошад (100 дохил нест)", "The down payment must be from 0 to 100% (100 not included)");
        E("Срок слишком большой для такой суммы", "invalid_term", "Мӯҳлат барои чунин маблағ хеле калон аст", "The term is too long for this amount");
        E("Сумма договора должна быть больше нуля", "invalid_amount", "Маблағи шартнома бояд аз сифр зиёд бошад", "The contract amount must be greater than zero");
        E("Сумма оплаты должна быть больше нуля", "invalid_amount", "Маблағи пардохт бояд аз сифр зиёд бошад", "The payment amount must be greater than zero");
        E("По сделке всё оплачено", "deal_paid", "Аҳд пурра пардохт шудааст", "Everything is paid for this deal");
        E("Дата оплаты не может быть в будущем", "invalid_date", "Санаи пардохт наметавонад дар оянда бошад", "The payment date cannot be in the future");
        E("Дата операции не может быть в будущем", "invalid_date", "Санаи амалиёт наметавонад дар оянда бошад", "The operation date cannot be in the future");
        E("Дата окончания периода раньше даты начала", "invalid_period", "Санаи анҷоми давра пеш аз санаи оғоз аст", "The period end date is before the start date");
        E("Платёж не найден", "payment_not_found", "Пардохт ёфт нашуд", "Payment not found");
        E("Платёж уже сторнирован", "payment_already_reversed", "Пардохт аллакай сторно шудааст", "The payment has already been reversed");
        E("Сторно нельзя сторнировать", "reversal_of_reversal", "Сторноро сторно кардан мумкин нест", "A reversal cannot be reversed");
        E("Платёж уже изменился, повторите запрос", "concurrency_conflict", "Пардохт аллакай тағйир ёфт, дархостро такрор кунед", "The payment has already changed, repeat the request");
        E("Операция уже сторнирована", "operation_already_reversed", "Амалиёт аллакай сторно шудааст", "The operation has already been reversed");
        E("Операция не найдена", "operation_not_found", "Амалиёт ёфт нашуд", "Operation not found");
        E("Онлайн-оплата не найдена", "online_payment_not_found", "Пардохти онлайн ёфт нашуд", "Online payment not found");
        E("Платёж не найден в платёжном шлюзе", "gateway_payment_not_found", "Пардохт дар дарвозаи пардохт ёфт нашуд", "Payment not found in the payment gateway");
        E("Платёж уже обработан банком", "payment_processed", "Пардохт аллакай аз ҷониби бонк коркард шудааст", "The payment has already been processed by the bank");
        E("Имитация оплаты доступна только с тестовым шлюзом", "not_found", "Тақлиди пардохт танҳо бо дарвозаи озмоишӣ дастрас аст", "Payment simulation is available only with the test gateway");
        E("Платёжный шлюз не найден", "gateway_not_found", "Дарвозаи пардохт ёфт нашуд", "Payment gateway not found");
        E("Курс ещё не задан", "rate_missing", "Қурб ҳанӯз муайян нашудааст", "The rate has not been set yet");
        E("Курс задаётся для иностранной валюты, сомони — базовая валюта", "invalid_currency", "Қурб барои асъори хориҷӣ муайян мешавад, сомонӣ — асъори асосӣ", "The rate is set for a foreign currency, the somoni is the base currency");
        E("Курс можно задать не дальше чем на завтра", "invalid_date", "Қурбро на дертар аз фардо муайян кардан мумкин аст", "The rate can be set no further than tomorrow");

        // ---------- документы, файлы, уведомления, отзывы, шаблоны, AI ----------
        E("Документ не найден", "document_not_found", "Ҳуҷҷат ёфт нашуд", "Document not found");
        E("Файл не найден", "file_not_found", "Файл ёфт нашуд", "File not found");
        E("Ссылка недействительна или устарела", "link_invalid", "Пайванд беэътибор ё кӯҳна аст", "The link is not valid or has expired");
        E("Документ должен быть изображением JPG, PNG, GIF, WebP или PDF", "invalid_file", "Ҳуҷҷат бояд тасвири JPG, PNG, GIF, WebP ё PDF бошад", "The document must be a JPG, PNG, GIF, WebP image or a PDF");
        E("Содержимое файла не соответствует его расширению", "invalid_file", "Мундариҷаи файл ба пасвандаш мувофиқ нест", "The file contents do not match its extension");
        E("Не удалось прочитать документ", "invalid_file", "Хондани ҳуҷҷат муяссар нашуд", "Could not read the document");
        E("Уведомление не найдено", "notification_not_found", "Огоҳинома ёфт нашуд", "Notification not found");
        E("Введите заголовок уведомления", "title_required", "Сарлавҳаи огоҳиномаро ворид кунед", "Enter the notification title");
        E("Введите текст уведомления", "text_required", "Матни огоҳиномаро ворид кунед", "Enter the notification text");
        E("Тип уведомления — информация, предупреждение или ошибка", "invalid_type", "Навъи огоҳинома — маълумот, огоҳӣ ё хато", "Notification type — info, warning or error");
        E("Отзыв не найден", "review_not_found", "Шарҳ ёфт нашуд", "Review not found");
        E("Вы уже оставили отзыв об этом доме", "review_exists", "Шумо аллакай дар бораи ин бино шарҳ гузоштаед", "You have already left a review of this building");
        E("Вы уже оставили отзыв об этом доме. Его можно изменить, пока он не опубликован", "review_exists", "Шумо аллакай дар бораи ин бино шарҳ гузоштаед. Онро то нашр иваз кардан мумкин аст", "You have already left a review of this building. It can be changed until it is published");
        E("Опубликованный отзыв изменить нельзя", "review_published", "Шарҳи нашршударо иваз кардан мумкин нест", "A published review cannot be changed");
        E("Оставить отзыв может только покупатель со сделкой в этом доме", "review_forbidden", "Шарҳро танҳо харидоре гузошта метавонад, ки дар ин бино аҳд дорад", "Only a buyer with a deal in this building can leave a review");
        E("Оценка — от 1 до 5", "invalid_rating", "Баҳо — аз 1 то 5", "Rating — from 1 to 5");
        E("Ответ — от 1 до 2000 символов", "invalid_text", "Ҷавоб — аз 1 то 2000 аломат", "Reply — from 1 to 2000 characters");
        E("Шаблон не найден", "template_not_found", "Қолаб ёфт нашуд", "Template not found");
        E("Шаблон с таким названием уже есть", "template_name_taken", "Қолаб бо чунин ном аллакай вуҷуд дорад", "A template with this name already exists");
        E("Название шаблона — от 1 до 120 символов", "invalid_name", "Номи қолаб — аз 1 то 120 аломат", "Template name — from 1 to 120 characters");
        E("Текст шаблона — от 1 до 1000 символов", "invalid_text", "Матни қолаб — аз 1 то 1000 аломат", "Template text — from 1 to 1000 characters");
        E("Тип шаблона — платёж, бронь или приветствие", "invalid_type", "Навъи қолаб — пардохт, брон ё табрик", "Template type — payment, booking or greeting");
        E("Название — от 1 до 200 символов", "invalid_name", "Ном — аз 1 то 200 аломат", "Name — from 1 to 200 characters");
        E("Напишите вопрос", "question_required", "Саволро нависед", "Write a question");
        E("AI не прислал ответ, попробуйте ещё раз", "ai_no_answer", "AI ҷавоб нафиристод, бори дигар кӯшиш кунед", "The AI did not send an answer, try again");
        E("AI помощник не настроен", "ai_not_configured", "Ёрдамчии AI танзим нашудааст", "The AI assistant is not set up");
        E("Помощник сейчас занят, напишите менеджеру", "ai_public_busy", "Ёрдамчӣ ҳоло банд аст, ба менеҷер нависед", "The assistant is busy now, write to a manager");

        // ---------- уведомления покупателю: Telegram, напоминания о платежах, устройства ----------
        E("Бот Telegram не настроен: уведомления в Telegram пока недоступны", "telegram_not_configured", "Боти Telegram танзим нашудааст: огоҳиҳо дар Telegram ҳоло дастрас нестанд", "The Telegram bot is not set up: Telegram notifications are not available yet");
        E("Напоминание по этому платежу уже отправляли сегодня. Следующее — через сутки после прошлого", "reminder_too_soon", "Ёдраскунӣ барои ин пардохт имрӯз аллакай фиристода шуд. Навбатӣ — пас аз як шабонарӯз", "A reminder for this payment was already sent today. The next one — a day after the last");
        E("Этот платёж уже оплачен или его нет в графике", "reminder_item_paid", "Ин пардохт аллакай пардохт шудааст ё дар ҷадвал нест", "This payment is already paid or is not in the schedule");
        E("У покупателя нет входа в кабинет: напоминание некуда отправить", "reminder_no_cabinet", "Харидор ба кабинет ворид нашудааст: ёдраскуниро фиристодан ҷое нест", "The buyer has no cabinet sign-in: there is nowhere to send the reminder");
        E("Вход не найден или уже закрыт", "session_not_found", "Воридшавӣ ёфт нашуд ё аллакай баста шудааст", "Session not found or already closed");
        E("На этот номер нет Telegram — войдите по почте", "telegram_no_account", "Дар ин рақам Telegram нест — бо почта ворид шавед", "This number has no Telegram — sign in by e-mail");
        E("Коды в Telegram на сегодня закончились. Войдите по почте", "telegram_gateway_limit", "Рамзҳо дар Telegram барои имрӯз тамом шуданд. Бо почта ворид шавед", "Telegram codes have run out for today. Sign in by e-mail");
        E("Telegram не принял код. Попробуйте позже или войдите по почте", "telegram_gateway_failed", "Telegram рамзро қабул накард. Баъдтар кӯшиш кунед ё бо почта ворид шавед", "Telegram did not accept the code. Try later or sign in by e-mail");
        E("Вопрос — не длиннее 500 символов", "ai_question_too_long", "Савол — на дарозтар аз 500 аломат", "Question — up to 500 characters");
        E("AI помощник временно недоступен, попробуйте позже", "ai_unavailable", "Ёрдамчии AI муваққатан дастнорас аст, баъдтар кӯшиш кунед", "The AI assistant is temporarily unavailable, try later");
        E("Проверка документов ИИ не настроена", "ai_not_configured", "Санҷиши ҳуҷҷатҳо бо ИИ танзим нашудааст", "AI document check is not set up");
        E("Проверка документов ИИ временно недоступна, попробуйте позже", "ai_unavailable", "Санҷиши ҳуҷҷатҳо бо ИИ муваққатан дастнорас аст, баъдтар кӯшиш кунед", "AI document check is temporarily unavailable, try later");
        E("Отчёт не найден", "report_not_found", "Ҳисобот ёфт нашуд", "Report not found");
        E("Админу объекта назначается ровно один дом", "object_admin_one_building", "Ба админи объект маҳз як бино таъин мешавад", "A building admin is given exactly one building");
        E("Владельцу доступны все дома организации", "owner_all_buildings", "Ба соҳиб ҳамаи биноҳои ташкилот дастрасанд", "The owner has access to all buildings of the organization");
        E("Админ объекта назначает сотрудников только в свой дом", "object_admin_scope", "Админи объект кормандонро танҳо ба бинои худ таъин мекунад", "A building admin assigns staff only to their own building");
        E("Админ объекта управляет только сотрудниками своего дома", "object_admin_scope", "Админи объект танҳо кормандони бинои худро идора мекунад", "A building admin manages only the staff of their own building");
        E("Неверный адрес почты", "invalid_email", "Суроғаи почта нодуруст аст", "Wrong e-mail address");
        E("Неверный номер телефона", "invalid_phone", "Рақами телефон нодуруст аст", "Wrong phone number");
        E("Укажите имя и фамилию (вместе до 200 символов)", "name_required", "Ном ва насабро нишон диҳед (якҷоя то 200 аломат)", "Enter first and last name (up to 200 characters together)");

        // ---------- валидаторы (FluentValidation) ----------
        E("Заполните обязательное поле", "validation_failed", "Майдони ҳатмиро пур кунед", "Fill in the required field");
        P("Заполните поле ", "validation_failed", "Майдонро пур кунед: ", "Fill in the field ");
        E("Укажите имя", "name_required", "Номро нишон диҳед", "Enter a name");
        E("Имя — от 2 до 200 символов", "invalid_name", "Ном — аз 2 то 200 аломат", "Name — from 2 to 200 characters");
        E("Неверный номер телефона, пример: +992 90 123 45 67", "invalid_phone", "Рақами телефон нодуруст аст, масалан: +992 90 123 45 67", "Wrong phone number, example: +992 90 123 45 67");
        E("Укажите почту", "email_required", "Почтаро нишон диҳед", "Enter an e-mail");
        E("Неверный формат почты", "invalid_email", "Формати почта нодуруст аст", "Wrong e-mail format");
        E("Укажите пароль", "password_required", "Паролро нишон диҳед", "Enter a password");
        E("Код состоит из 6 цифр", "invalid_code", "Рамз аз 6 рақам иборат аст", "The code is 6 digits");
        E("Укажите код из SMS", "code_required", "Рамзро аз SMS нишон диҳед", "Enter the code from the SMS");
        E("Укажите refresh-токен", "refresh_token_required", "Refresh-токенро нишон диҳед", "Give the refresh token");
        E("Комментарий — не длиннее 1000 символов", "comment_too_long", "Шарҳ — на дарозтар аз 1000 аломат", "Comment — up to 1000 characters");
        E("Укажите ФИО", "full_name_required", "Ному насабро нишон диҳед", "Enter the full name");
        E("Укажите адрес", "address_required", "Суроғаро нишон диҳед", "Enter the address");
        E("Укажите дом", "building_required", "Биноро нишон диҳед", "Specify the building");
        E("Укажите этаж", "floor_required", "Ошёнаро нишон диҳед", "Specify the floor");
        E("Укажите квартиру", "apartment_required", "Манзилро нишон диҳед", "Specify the apartment");
        E("Укажите клиента", "customer_required", "Мизоҷро нишон диҳед", "Specify the customer");
        E("Укажите роль", "role_required", "Нақшро нишон диҳед", "Specify the role");
        E("Укажите короткое имя организации", "slug_required", "Номи кӯтоҳи ташкилотро нишон диҳед", "Enter the short name of the organization");
        E("Укажите название на русском", "name_ru_required", "Номро бо забони русӣ нишон диҳед", "Enter the name in Russian");
        E("Укажите название на таджикском", "name_tg_required", "Номро бо забони тоҷикӣ нишон диҳед", "Enter the name in Tajik");
        E("Укажите название этапа", "stage_name_required", "Номи марҳиларо нишон диҳед", "Enter the stage name");
        E("Описание на русском не длиннее 4000 символов", "description_too_long", "Тавсиф бо забони русӣ на дарозтар аз 4000 аломат", "Description in Russian — up to 4000 characters");
        E("Описание на таджикском не длиннее 4000 символов", "description_too_long", "Тавсиф бо забони тоҷикӣ на дарозтар аз 4000 аломат", "Description in Tajik — up to 4000 characters");
        E("Этажность должна быть от 1 до 100", "invalid_floors_count", "Шумораи ошёнаҳо бояд аз 1 то 100 бошад", "The number of floors must be from 1 to 100");
        E("Номер этажа должен быть от 0 до 100", "invalid_floor_number", "Рақами ошёна бояд аз 0 то 100 бошад", "The floor number must be from 0 to 100");
        E("Номер квартиры должен быть от 1 до 99999", "invalid_apartment_number", "Рақами манзил бояд аз 1 то 99999 бошад", "The apartment number must be from 1 to 99999");
        E("Комнат должно быть от 0 (студия) до 10", "invalid_rooms", "Шумораи ҳуҷраҳо бояд аз 0 (студия) то 10 бошад", "Rooms must be from 0 (studio) to 10");
        E("Год сдачи — от 2000 до 2100", "invalid_year", "Соли супоридан — аз 2000 то 2100", "Completion year — from 2000 to 2100");
        E("Квартир в доме — от 1 до 5000", "invalid_apartments_count", "Манзилҳо дар бино — аз 1 то 5000", "Apartments in the building — from 1 to 5000");
        E("Ссылка на 3D-модель должна быть полным адресом http(s)", "invalid_url", "Пайванд ба модели 3D бояд суроғаи пурраи http(s) бошад", "The 3D model link must be a full http(s) address");
        E("Название компании — от 2 до 200 символов", "invalid_name", "Номи ширкат — аз 2 то 200 аломат", "Company name — from 2 to 200 characters");
        E("Напишите сообщение", "message_required", "Паёмро нависед", "Write a message");
        E("Скидка не может быть отрицательной", "invalid_discount", "Тахфиф манфӣ буда наметавонад", "The discount cannot be negative");
        E("Для отмены сделки укажите причину", "reason_required", "Барои бекор кардани аҳд сабабро нишон диҳед", "Give the reason to cancel the deal");
        E("Укажите причину отказа", "reason_required", "Сабаби радкуниро нишон диҳед", "Give the reason for the refusal");
        E("Укажите причину отмены брони", "reason_required", "Сабаби бекор кардани бронро нишон диҳед", "Give the reason to cancel the booking");
        E("Укажите причину сторно", "reason_required", "Сабаби сторноро нишон диҳед", "Give the reason for the reversal");
        E("Укажите основание операции", "reason_required", "Асоси амалиётро нишон диҳед", "Give the basis of the operation");
        E("Сумма должна быть больше нуля", "invalid_amount", "Маблағ бояд аз сифр зиёд бошад", "The amount must be greater than zero");
        E("Сумма платежа должна быть больше нуля", "invalid_amount", "Маблағи пардохт бояд аз сифр зиёд бошад", "The payment amount must be greater than zero");
        E("Сумма — не больше двух знаков после запятой", "invalid_amount", "Маблағ — на зиёда аз ду рақам пас аз вергул", "Amount — no more than two digits after the point");
        E("Слишком большая сумма", "invalid_amount", "Маблағ хеле калон аст", "The amount is too large");
        E("Курс задаётся только для USD", "invalid_currency", "Қурб танҳо барои USD муайян мешавад", "The rate is set only for USD");
        E("Укажите дату курса", "date_required", "Санаи қурбро нишон диҳед", "Enter the rate date");
        E("Укажите дату начала графика", "date_required", "Санаи оғози ҷадвалро нишон диҳед", "Enter the schedule start date");
        E("Укажите дату операции", "date_required", "Санаи амалиётро нишон диҳед", "Enter the operation date");
        E("Укажите дату оплаты", "date_required", "Санаи пардохтро нишон диҳед", "Enter the payment date");
    }
}
