import styles from "./ConversationPage.module.css";
import {Link} from "react-router-dom";
import {useCallback, useEffect, useLayoutEffect, useRef, useState} from "react";
import {ChatMessage, ChatSummary, HistoryResponse} from "../../types";
import {api, ApiError, getErrorMessage} from "../../api";

type Props = {
    chatId: string;
    userId: string;
    onSessionExpired: () => void;
};

export default function ConversationPage({chatId, userId, onSessionExpired}: Props){

    const [chat, setChat] = useState<ChatSummary | null>(null);
    const [error, setError] = useState("");
    const [cursor, setCursor] = useState<number | null>(null);
    const [olderError, setolderError] = useState("");
    const [loadingOlder, setLoadingOlder] = useState(false);
    const[messages, setMessages] = useState<ChatMessage[]>([]);

    const timeline = useRef<HTMLDivElement>(null);
    const requests = useRef<AbortController | null>(null);
    const loadingOlderRef = useRef(false);
    const previousHeight = useRef<number | null>(null);
    const scrollToBottom = useRef(false);

    const chatUrl = `/api/chats/${chatId}`;
    const messagesUrl = `${chatUrl}/messages`;

    useEffect(() => {
        const controller = new AbortController(); //контроллер отмены для открытого чата
        requests.current = controller;

        Promise.all([
            api<ChatSummary>(chatUrl, {signal: controller.signal}),
            api<HistoryResponse>(messagesUrl, {signal: controller.signal}) //последние 30 соо
        ])
            .then(([openedChat, history]) => {
                if (controller.signal.aborted) return;

                scrollToBottom.current = true;
                setChat(openedChat);
                setMessages(history.items);
                setCursor(history.nexSeq);
            })
            .catch((requestError: unknown) => {
                if (controller.signal.aborted) return;

                if (requestError instanceof ApiError && requestError.status === 401){
                    onSessionExpired();
                    return;
                }
                setError(getErrorMessage(requestError));
            });

        return () => controller.abort();
    }, [chatUrl, messagesUrl, onSessionExpired]);

    const loadOlder = useCallback(async () => {
        const controller = requests.current;
        const element = timeline.current;

        if (!controller || !element || controller.signal.aborted) return;
        if (cursor === null || loadingOlderRef.current || previousHeight.current !== null) return;

        loadingOlderRef.current = true;
        setLoadingOlder(true);
        setolderError("");

        try{
            const history = await api<HistoryResponse>(
                `${messagesUrl}?beforeSeq=${cursor}`,
                {signal: controller.signal}
            );

            if (controller.signal.aborted) return;

            previousHeight.current = element.scrollHeight;
            setMessages((current) => [...history.items, ...current]);
            setCursor(history.nexSeq);
        } catch (error) {
            if (controller.signal.aborted) return;
            if (error instanceof ApiError && error.status === 401) {
                onSessionExpired();
                return;
            }
            setolderError(getErrorMessage(error));
        } finally {
            if (!controller.signal.aborted) {
                loadingOlderRef.current = false;
                setLoadingOlder(false);
            }
        }
    }, [cursor, messagesUrl, onSessionExpired]);

    useLayoutEffect(() => {
        const element = timeline.current;
        if (!element) return;

        if (scrollToBottom.current) {
            element.scrollTop = element.scrollHeight;
            scrollToBottom.current = false;
            return;
        }

        if (previousHeight.current !== null) {
            const addedHeight = element.scrollHeight - previousHeight.current;
            element.scrollTop += addedHeight;
            previousHeight.current = null;
        }
    }, [messages]);

    useEffect(() => {
        const element = timeline.current;
        if (!element || !chat || olderError) return;

        if (element.scrollHeight <= element.clientHeight) {
            void loadOlder();
        }
    }, [chat, messagesUrl, olderError, loadOlder]);

    return (
        <section className={styles.conversation} aria-label="Переписка">
            <header className={styles.header}>
                <Link to="/chats" className={styles.back}>←</Link>
                <div className={styles.name}>
                    <h2>{chat?.peer.displayName ?? "Переписка"}</h2>
                    {chat && <small>@{chat.peer.username}</small>}
                </div>
            </header>

            <div className={styles.timelineArea}>
                <div
                    className={styles.timeline}
                    ref={timeline}
                    onScroll={(event) => {
                        if (event.currentTarget.scrollTop <= 80) void loadOlder();
                    }}
                >
                    {error && <p role="alert">{error}</p>}
                    {!chat && !error && <p role="status">Загружаем переписку...</p>}
                    {chat && messages.length === 0 && <p>Сообщений пока нет.</p>}

                    {messages.map((message) => (
                        <div
                            key={message.id}
                            className={styles.message}
                            data-own={message.senderId === userId}
                        >
                            {message.text}
                        </div>
                    ))}
                </div>

                {loadingOlder && <p className={styles.notice} role="status">Загружаем сообщения...</p>}
                {olderError && <p className={styles.notice} role="alert">{olderError}</p>}

            </div>
        </section>
    )
}
