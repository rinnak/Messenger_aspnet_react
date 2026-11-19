import styles from "./ConversationPage.module.css";
import {Link} from "react-router-dom";
import {useCallback, useEffect, useLayoutEffect, useRef, useState} from "react";
import {ChatMessage, ChatSummary, HistoryResponse} from "../../types";
import {api, ApiError, getErrorMessage} from "../../api";
import MessageComposer from "../../features/message-composer/MessageComposer";
import { HubConnectionBuilder } from "@microsoft/signalr";
import {formatTime} from "../../fotmatTime";
import type { HubConnection } from "@microsoft/signalr";

type Props = {
    liveConnection: { hub: HubConnection } | null;
    connectionStatus: string;
    chatId: string;
    userId: string;
    chatPreview: ChatSummary | null;
    onSessionExpired: () => void;
};

export default function ConversationPage({liveConnection, connectionStatus, chatId, userId, chatPreview, onSessionExpired}: Props){

    const [loadedChat, setLoadedChat] = useState<ChatSummary | null>(null);
    const [loadingHistory, setLoadingHistory] = useState(true);
    const chat = chatPreview ?? loadedChat;
    const needsDetails = chatPreview === null;
    const [error, setError] = useState("");
    const [cursor, setCursor] = useState<number | null>(null);
    const [olderError, setolderError] = useState("");
    const [loadingOlder, setLoadingOlder] = useState(false);
    const[messages, setMessages] = useState<ChatMessage[]>([]);
    const [liveStatus, setLiveStatus] = useState("");

    const confirmedSequence = useRef(0);
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
        setLoadingHistory(true);
        setError("");

        Promise.all([
            needsDetails
                ? api<ChatSummary>(chatUrl, {signal: controller.signal})
                : Promise.resolve(null),
            api<HistoryResponse>(messagesUrl, {signal: controller.signal}), //последние 30 соо
        ])
            .then(([openedChat, history]) => {
                if (controller.signal.aborted) return;

                scrollToBottom.current = true;
                setLoadedChat(openedChat);
                setMessages(history.items);
                confirmedSequence.current = history.items.at(-1)?.sequence ?? 0;
                setCursor(history.nexSeq);
            })
            .catch((requestError: unknown) => {
                if (controller.signal.aborted) return;

                if (requestError instanceof ApiError && requestError.status === 401){
                    onSessionExpired();
                    return;
                }
                setError(getErrorMessage(requestError));
            })
            .finally(() => {
                if (!controller.signal.aborted) setLoadingHistory(false);
            })

        return () => controller.abort();
    }, [needsDetails, chatUrl, messagesUrl, onSessionExpired]);

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
            previousHeight.current = null;
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

    const mergeMessages = useCallback((incoming: ChatMessage[], forceScroll = false) => {
        if (incoming.length === 0) return;

        const element = timeline.current;
        const nearBottom = element !== null &&
            element.scrollHeight - element.scrollTop - element.clientHeight <= 80;

        if (forceScroll || nearBottom) scrollToBottom.current = true;

        setMessages((current) => {
            const byId = new Map(current.map((message) => [message.id, message]));
            for (const message of incoming) byId.set(message.id, message);
            return [...byId.values()].sort((a, b) => a.sequence - b.sequence);
        });
    }, []);

    function handleSent(message: ChatMessage) {
        mergeMessages([message], true);
    }

    const chatReady = chat !== null && !loadingHistory && !error;

    useEffect(() => {
        if (!chatReady || !liveConnection) return;

        const connection = liveConnection.hub;
        const requests = new AbortController();

        //доугрузка сообщений
        async function catchUp(){
            let after = confirmedSequence.current; //точка с которой начинаем

            try {
                while (!requests.signal.aborted) {
                    const page = await api<HistoryResponse>(
                        `${messagesUrl}?afterSeq=${after}&limit=100`,
                        {signal: requests.signal}
                    );
                    if (requests.signal.aborted) return;

                    const last = page.items.at(-1)?.sequence;
                    if (last !== undefined){
                        confirmedSequence.current = Math.max(confirmedSequence.current, last);
                    }
                    mergeMessages(page.items);

                    if (page.nexSeq === null) break;
                    after = page.nexSeq;
                }
                setLiveStatus("");
            } catch (error) {
                if (requests.signal.aborted) return;
                if (error instanceof ApiError && error.status === 401){
                    onSessionExpired();
                    return;
                }
                setLiveStatus("Не удалось догрузить сообщения. Открой диалог заново");
            }
        }

        function handleMessageCreated(message: ChatMessage) {
            if (requests.signal.aborted || message.chatId !== chatId) return;

            if (message.sequence === confirmedSequence.current + 1){
                confirmedSequence.current = message.sequence;
            }

            mergeMessages([message]);
        }

        connection.on("MessageCreated", handleMessageCreated);
        void catchUp();

        return () => {
            requests.abort();
            connection.off("MessageCreated", handleMessageCreated);
        }
    }, [chatReady, chatId, messagesUrl, mergeMessages, onSessionExpired, liveConnection]);



    return (
        <section className={styles.conversation} aria-label="Переписка">
            <header className={styles.header}>
                <Link to="/chats" className={styles.back}>
                    <svg
                        viewBox="0 0 24 24"
                        fill="none"
                        stroke="currentColor"
                        strokeWidth="1.7"
                        strokeLinecap="round"
                        strokeLinejoin="round"
                        aria-hidden="true"
                    >
                        <path d="M19 12H5m7-7-7 7 7 7" />
                    </svg>
                </Link>
                {chat && <span className={styles.avatar}>{chat.peer.displayName[0]}</span>}
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

                    {messages.map((message) => (
                        <div
                            key={message.id}
                            className={styles.message}
                            data-own={message.senderId === userId}
                        >
                            <span className={styles.messageText}>{message.text}</span>
                            <time className={styles.messageTime} dateTime={message.createdAt}>
                                {formatTime(message.createdAt)}
                            </time>
                        </div>
                    ))}
                </div>

                {loadingHistory && <p className={styles.historyStatus}>Загружаем переписку...</p>}
                {!loadingHistory && error && (<p className={styles.historyStatus} role="alert">{error}</p>)}
                {!loadingHistory && !error && messages.length === 0 && (
                    <p className={styles.historyStatus}>Сообщений пока нет</p>
                )}
                {!loadingHistory && (olderError || liveStatus || loadingOlder) && <p className={styles.notice} role={olderError ? "alert" : "status"}>{olderError || liveStatus || "Загружаем ранние сообщения..."}</p>}

            </div>
            <MessageComposer messagesUrl={messagesUrl} onSent={handleSent} onSessionExpires={onSessionExpired} disabled={loadingHistory || !chat || Boolean(error)}/>
        </section>
    )
}
