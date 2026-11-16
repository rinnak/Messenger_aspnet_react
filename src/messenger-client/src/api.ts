type ApiOptions = {
    method?: "GET" | "POST";
    body?: object;
    signal?: AbortSignal;
}

type ApiProblem = { title?: string; message?: string };

export class ApiError extends Error {
    constructor(public readonly status: number, message: string) {
        super(message);
    }
}

//ождаемая форма ответа
export async function api<T>(path: string, options: ApiOptions = {}): Promise<T> {
    const {method = "GET", body, signal} = options;
    const headers = new Headers();

    // перед запросом записи получаем csrf-токен
    if (method == "POST"){

        const tokenResponse = await fetch("/api/auth/csrf", {
            credentials: "same-origin", signal,
        });

        if (!tokenResponse.ok) {
            throw new Error("Не удалось получить CSRF-токен");
        }

        const {requestToken} = await tokenResponse.json() as { requestToken: string };
        headers.set("X-CSRF-TOKEN", requestToken);
    }

    if (body !== undefined){
        headers.set("Content-Type", "application/json");
    }

    const response = await fetch(path, {
        method,
        headers,
        signal,
        credentials: "same-origin",
        body: body === undefined ? undefined : JSON.stringify(body)
    });

    const data: unknown = response.status === 204 ? null : await response.json().catch(() => null);

    if (!response.ok){
        const problem = data as ApiProblem | null;
        throw new ApiError(response.status, problem?.message ?? problem?.title ?? "Запрос завершился с ошибкой");
    }

    if (response.status !== 204 && data === null) throw new Error("Сервер не вернул ожидаемые данные");
    return data as T;
}

export function getErrorMessage(error: unknown): string{
    return error instanceof Error ? error.message : "Попробуй ещё раз";
}