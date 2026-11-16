import {AccountProfile, ChatsResponse, ChatSummary} from "../../types";
import {NavLink, useParams} from "react-router-dom";
import {useEffect, useState} from "react";
import {api, ApiError, getErrorMessage} from "../../api";
import styles from "./ChatPage.module.css";
import ConversationPage from "/Users/rinnnaak/vuz/course_backend/src/messenger-client/src/pages/conversation/ConversationPage";

type Props = {
    user: AccountProfile;
    onLogout: () => void;
}

export default function ChatsPage({user, onLogout}: Props) {
    const {chatId} = useParams();
    const [items, setItems] = useState<ChatSummary[] | null>(null);
    const [error, setError] = useState("");
    const [leaving, setLeaving] = useState(false);
    const [search, setSearch] = useState("");


    useEffect(() => {
        const request = new AbortController();
        api<ChatsResponse>("/api/chats", {signal: request.signal})
            .then(data => setItems(data.items))
            .catch((error: unknown) => {
                if (request.signal.aborted) return;
                if (error instanceof ApiError && error.status === 401) onLogout();
                else setError(getErrorMessage(error))
            });
        return () => request.abort();
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

    const query = search.trim().replace(/^@/, "").toLocaleLowerCase();
    const visible = items?.filter(({ peer }) =>
        peer.displayName.toLocaleLowerCase().includes(query) ||
        peer.username.toLocaleLowerCase().includes(query));

    return(
        <main className={styles.workspace} data-open={Boolean(chatId)}>
            <aside className={styles.sidebar} aria-label="Список диалогов">
                <header className={styles.sidebarHeader}>
                    <div className={styles.name}>
                        <h1>Диалоги</h1>
                        <p>@{user.username}</p>
                    </div>
                    <button type="button" onClick={logout} disabled={leaving}>
                        {leaving ? "Выходим..." : "Выйти"}
                    </button>
                </header>

                <div className={styles.search}>
                    <input type="search"
                    aria-label="Поиск диалогов"
                    placeholder="Поиск диалогов"
                    value={search}
                    onChange={e => setSearch(e.target.value)}/>
                </div>

                {error && <p className={styles.error} role="alert">{error}</p>}
                {!items && !error && <p className={styles.status} role="status">Загружаем диалоги...</p>}
                {visible?.length === 0 && (
                    <p className={styles.status}>{items?.length ? "Диалоги не найдены" : "Диалогов пока нет"}</p>
                )}

                <ul className={styles.list}>
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
                            </NavLink>
                        </li>
                    ))}
                </ul>
            </aside>
            <div className={styles.detail}>
                {chatId ? (
                    <ConversationPage key={chatId} chatId={chatId} userId={user.id} onSessionExpired={onLogout}/>
                ) : (
                    <p className={styles.empty}>Выбери диалог, чтобы открыть переписку</p>
                )}
            </div>
        </main>
    )
}