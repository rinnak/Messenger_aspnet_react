import {useEffect, useState} from "react";
import {AccountProfile} from "../../types";
import {api, ApiError, getErrorMessage} from "../../api";
import styles from "../chats/ChatPage.module.css";

type Props = {
    query: string;
    openingUserId: string | null;
    onOpenChat: (peerUserId: string) => Promise<void>;
    onSessionExpired: () => void;
};

export default function UserSearchResults({
    query, openingUserId, onOpenChat, onSessionExpired
                                          }: Props){
    const [profiles, setProfiles] = useState<AccountProfile[] | null>(null);
    const [error, setError] = useState("");

    useEffect(() => {
        const request = new AbortController();

        const timer = window.setTimeout( async () => {
            try {
                const { items } = await api<{ items: AccountProfile[] }>(
                    `/api/users/search?${new URLSearchParams({q: query})}`,
                    {signal: request.signal}
                );
                if (!request.signal.aborted) setProfiles(items);
            }
            catch (error) {
                if (request.signal.aborted) return;
                if (error instanceof ApiError && error.status === 401) {
                    onSessionExpired();
                    return;
                }
                setError(getErrorMessage(error));
            }
        }, 300);

        return () => {
            window.clearTimeout(timer);
            request.abort();
        };
    }, [query, onSessionExpired]);

    if (error) return <li role="alert">{error}</li>;
    if (profiles === null) return <li role="status">Ищем пользователей...</li>;
    if (profiles.length === 0) return <li role="status">Пользователи не найдены</li>;

    return (
        <>
            {profiles.map((profile) => (
                <li key={profile.id}>
                    <button
                        className={styles.chatRow}
                        type="button"
                        disabled={openingUserId !== null}
                        onClick={() => void onOpenChat(profile.id)}
                    >
                        <span className={styles.name}>
                            <strong>{profile.displayName}</strong>
                            <small>@{profile.username}</small>
                        </span>
                        <span className={styles.rowAction}>
                            {openingUserId === profile.id ? "Открываем..." : "Написать"}
                        </span>
                    </button>
                </li>
            ))}
        </>
    )
}