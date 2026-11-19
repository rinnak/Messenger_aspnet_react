import {AccountProfile, ChatsResponse, ChatSummary} from "../../types";
import {NavLink, useParams, useNavigate} from "react-router-dom";
import {useEffect, useState} from "react";
import {api, ApiError, getErrorMessage} from "../../api";
import styles from "./ChatPage.module.css";
import ConversationPage from "../conversation/ConversationPage";
import UserSearchResults from "../user-search/UserSearchResults";
import {formatTime} from "../../fotmatTime";
import {HubConnectionBuilder, type HubConnection} from "@microsoft/signalr";

type Props = {
    user: AccountProfile;
    onLogout: () => void;
}

export default function ChatsPage({user, onLogout}: Props) {
    const {chatId} = useParams();
    const navigate = useNavigate();

    const [liveConnection, setLiveConnection] = useState<{hub: HubConnection} | null>(null);
    const [connectionStatus, setConnectionStatus] = useState("");
    const [items, setItems] = useState<ChatSummary[] | null>(null);
    const [error, setError] = useState("");
    const [leaving, setLeaving] = useState(false);
    const [search, setSearch] = useState("");
    const [openingUserId, setOpeningUserId] = useState<string | null>(null);

    useEffect(() => {
        let stopped = false;
        let listRequest: AbortController | undefined;

        const hub = new HubConnectionBuilder()
            .withUrl("/hubs/chat") //серверный хаб
            .withAutomaticReconnect() //восстановление соединения после обрыва
            .build();

        function refreshChats(){
            if (stopped) return;
            listRequest?.abort(); //при новом событии отменяем предыдущий запрос списка

            const request = new AbortController();
            listRequest = request;

            void api<ChatsResponse>("/api/chats", {signal: request.signal})
                .then(({items}) => {
                    if (request.signal.aborted) return;
                    setItems(items);
                    setError("");
                })
                .catch((error: unknown) => {
                    if (request.signal.aborted) return;
                    if (error instanceof ApiError && error.status === 401) onLogout();
                    else setError(getErrorMessage(error));
                });
        }

        function handleConnected(){
            if (stopped) return;
            setLiveConnection({hub});
            setConnectionStatus("");
            refreshChats();
        }

        hub.on("ChatCreated", refreshChats);
        hub.on("MessageCreated", refreshChats);
        hub.onreconnected(handleConnected);

        hub.onreconnecting(() => {
            if (stopped) return;
            setLiveConnection(null);
            setConnectionStatus("Восстанавливаем соединение...");
        });

        hub.onclose(() => {
            if (stopped) return;
            setLiveConnection(null);
            setConnectionStatus("Соединение потеряно");
        });

        refreshChats();
        void hub.start().then(handleConnected).catch(() => {
            if (!stopped) setConnectionStatus("Не удалось подключить обновление чатов")
        });

        return () => {
            stopped = true;
            listRequest?.abort();
            void hub.stop();
        }
    }, [onLogout]);

    async function logout(){
        setLeaving(true);
        setError("");

        try{
            await api<null>("/api/auth/logout", {method: "POST"});
            onLogout();
        } catch (error) {
            if (error instanceof ApiError && error.status === 401) onLogout();
            else setError(getErrorMessage(error));
        } finally {
            setLeaving(false);
        }
    }

    async function openChat(peerUserId: string){
        if (openingUserId !== null) return;

        setOpeningUserId(peerUserId);
        setError("");

        try{
            const chat = await api<ChatSummary>("/api/chats/direct", {
                method: "POST",
                body: {peerUserId}
            });

            setItems((current) => {
                const chats = current ?? [];
                return chats.some((item) => item.id === chat.id)
                    ? chats.map((item) => item.id === chat.id ? chat : item)
                    : [chat, ...chats];
            });

            setSearch("");
            navigate(`/chats/${chat.id}`);
        } catch (error) {
            if (error instanceof ApiError && error.status === 401) onLogout();
            else setError(getErrorMessage(error));
        } finally {
            setOpeningUserId(null);
        }
    }

    const query = search.trim().replace(/^@/, "").trim();
    const localQuery = query.toLocaleLowerCase();
    const visible = items?.filter(({ peer }) =>
        peer.displayName.toLocaleLowerCase().includes(localQuery) ||
        peer.username.toLocaleLowerCase().includes(localQuery));
    const chatPreview = items?.find((item) => item.id === chatId) ?? null;

    return(
        <main className={styles.workspace} data-open={Boolean(chatId)}>
            <aside className={styles.rail}>
                <span className={styles.accountAvatar}
                      title={`@${user.username}`}
                      aria-hidden="true">
                    {Array.from(user.displayName.trim())[0]?.toLocaleUpperCase() ?? "?"}
                </span>
                <div className={styles.railActions}>
                    <button
                        className={styles.logout}
                        type="button"
                        onClick={logout}
                        disabled={leaving}
                        aria-label={leaving ? "Выходим..." : "Выйти из аккаунта"}
                        title="Выйти из аккаунта"
                    >
                        <svg
                            viewBox="0 0 24 24"
                            fill="none"
                            stroke="currentColor"
                            strokeWidth="1.7"
                            strokeLinecap="round"
                            aria-hidden="true"
                        ><path d="M12 3v9M7 6a8 8 0 1 0 10 0" />
                        </svg>
                    </button>
                </div>
            </aside>
            <aside className={styles.sidebar} aria-label="Список диалогов">
                <header className={styles.sidebarHeader}>
                    <div className={styles.name}>
                        <h1>Диалоги</h1>
                    </div>
                </header>
                <div className={styles.search}>
                    <svg
                        viewBox="0 0 24 24"
                        fill="none"
                        stroke="currentColor"
                        strokeWidth="1.7"
                        strokeLinecap="round"
                        aria-hidden="true"
                    >
                        <circle cx="10.75" cy="10.75" r="6.5" />
                        <path d="m16 16 4 4" />
                    </svg>
                    <input type="search"
                    aria-label="Поиск диалогов и людей"
                    placeholder="Поиск диалогов и людей"
                           maxLength={50}
                           disabled={openingUserId !== null}
                    value={search}
                    onChange={e => setSearch(e.target.value)}/>
                </div>

                {error && <p className={styles.error} role="alert">{error}</p>}
                {connectionStatus && !chatId && (
                    <p className={styles.status} role="status">{connectionStatus}</p>
                )}
                {!items && !error && <p className={styles.status} role="status">Загружаем диалоги...</p>}

                <ul className={styles.list}>
                    {visible?.length === 0 && (
                        query ? (
                            <UserSearchResults
                                key={query}
                                query={query}
                                openingUserId={openingUserId}
                                onOpenChat={openChat}
                                onSessionExpired={onLogout}
                            />
                        ) : (
                            <li className={styles.status}>Диалогов пока нет</li>
                        )
                    )}
                    {visible?.map(chat => (
                        <li key={chat.id}>
                            <NavLink className={styles.chatRow} to={`/chats/${chat.id}`} end>
                                <span className={styles.avatar} aria-hidden="true">
                                    {Array.from(chat.peer.displayName.trim())[0]?.toLocaleUpperCase() ?? "?"}
                                </span>
                                <span className={styles.name}>
                                    <strong>{chat.peer.displayName}</strong>
                                    <small>@{chat.peer.username}</small>
                                </span>
                                {chat.lastMessageAt && (
                                    <time className={styles.rowTime} dateTime={chat.lastMessageAt}>
                                        {formatTime(chat.lastMessageAt)}
                                    </time>
                                )}
                            </NavLink>
                        </li>
                    ))}
                </ul>
            </aside>
            <div className={styles.detail}>
                {chatId ? (
                    items === null && !error ? (
                        <p role="status">Загружаем переписку...</p>
                    ) : (
                        <ConversationPage
                            liveConnection={liveConnection}
                            connectionStatus={connectionStatus}
                            key={chatId}
                            chatId={chatId}
                            chatPreview={chatPreview}
                            userId={user.id}
                            onSessionExpired={onLogout}
                        />
                    )
                ) : (
                    <p className={styles.empty}>Выбери диалог, чтобы открыть переписку</p>
                )}
            </div>
        </main>
    );
}