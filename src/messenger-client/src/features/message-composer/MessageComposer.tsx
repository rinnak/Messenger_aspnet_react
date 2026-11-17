import {ChatMessage} from "../../types";
import {useRef, useState, type FormEvent} from "react";
import {api, ApiError, getErrorMessage} from "../../api";
import styles from "./MessageComposer.module.css";

type Props = {
    messagesUrl: string;
    onSent: (message: ChatMessage) => void;
    onSessionExpires: () => void;
    disabled: boolean;
};

export default function MessageComposer({ messagesUrl, onSent, onSessionExpires, disabled = false }: Props) {
    const [draft, setDraft] = useState("");
    const [sending, setSending] = useState(false);
    const [error, setError] = useState("");

    const attempt = useRef<{clientMessageId: string; text: string} | null>(null);

    async function send(event: FormEvent<HTMLFormElement>){
        event.preventDefault();
        const text = draft.trim();
        if (disabled || sending || !text || text.length > 2000) return;

        const payload = attempt.current?.text === text
            ? attempt.current
            : { clientMessageId: crypto.randomUUID(), text};

        attempt.current = payload;
        setSending(true);
        setError("");

        try {
            const message = await api<ChatMessage>(messagesUrl, {
                method: "POST",
                body: payload
            });
            attempt.current = null;
            setDraft("");
            onSent(message);
        } catch (e) {
            if (e instanceof ApiError && e.status === 401) {
                onSessionExpires();
            } else {
                setError(getErrorMessage(error));
            }
        } finally {
            setSending(false);
        }
    }

    return (
        <form className={styles.form} onSubmit={send}>
            <textarea
                aria-label="Сообщение"
                placeholder="Написать сообщение"
                rows = {1}
                maxLength={2000}
                value={draft}
                disabled={disabled || sending}
                onChange={(e) => setDraft(e.target.value)}
                onKeyDown={(event) => {
                    if (event.key !== "Enter" || event.shiftKey) return;

                    if (event.nativeEvent.isComposing) return;
                    event.preventDefault();
                    event.currentTarget.form?.requestSubmit();
                }}
            />
            <button type="submit" disabled={disabled || sending || !draft.trim()}>
                {sending ? "Отправляем" : "Отправить"}
            </button>
            {error && <p className={styles.error} role="alert">{error}</p>}
        </form>
    )
}