import LoginPage from "./pages/login/LoginPage";
import {Navigate, Route, Routes} from "react-router-dom";
import {useCallback, useEffect, useState} from "react";
import AccountForm from "./features/AccountForm";
import {api, ApiError, getErrorMessage} from "./api";
import type { AccountProfile } from "./types";
import RegisterPage from "./pages/register/RegisterPage";
import ChatsPage from "./pages/chats/ChatsPage";

export default function App(){
    const [user, setUser] = useState<AccountProfile | null>(null);
    const [loading, setLoading] = useState(true);
    const [error, setError] = useState("");
    const clearSession = useCallback(()=>setUser(null), []);

    useEffect(() => {
        const request = new AbortController();
        api<AccountProfile>("/api/auth/me", {signal: request.signal})
            .then(setUser)
            .catch((error: unknown) => {
                if (!request.signal.aborted && !(error instanceof ApiError && error.status === 401)){
                    setError(getErrorMessage(error));
                }
            })
            .finally(() => {
                if (!request.signal.aborted) setLoading(false);
            });
        return () => request.abort();
    }, []);

    if (loading) return <p role="status">Проверяем вход...</p>
    if (error) return <p role="alert">{error}</p>

    return (
        <Routes>
            <Route path="/login" element={user ? <Navigate to="/chats" replace /> : <LoginPage onLogin={setUser} />} />
            <Route path="/register" element={user ? <Navigate to="/chats" replace/> : <RegisterPage />}/>
            <Route path="/chats/:chatId?" element={
                user ? <ChatsPage user={user} onLogout={clearSession} /> : <Navigate to="/login" replace/>
                                         }/>
            <Route path="*" element={<Navigate to={user ? "/chats" : "/login"} replace/>} />
        </Routes>
    )
}